using System;
using System.Collections.Generic;
using System.Linq;
using Dorsal.Core.Domain;
using Dorsal.Core.Random;

namespace Dorsal.Core.Career
{
    public enum TrainingFocus
    {
        Balanced,
        Pace,
        Shooting,
        Passing,
        Dribbling,
        Defending,
        Physical,
        Goalkeeping,
    }

    public enum TrainingIntensity
    {
        Low,
        Normal,
        High,
    }

    public sealed class TrainingResult
    {
        public float Grade { get; set; }
        public float FitnessCost { get; set; }
        public bool Injured { get; set; }
        public Dictionary<AttributeId, int> AttributeGains { get; set; } = new Dictionary<AttributeId, int>();
    }

    /// <summary>
    /// Evolución de atributos. La mejora depende de edad, potencial, minutos, entrenamiento y profesionalidad.
    /// Nunca supera el potencial: el margen restante reduce el crecimiento y lo anula al alcanzarlo.
    /// </summary>
    public static class ProgressionSystem
    {
        /// <summary>Factor de crecimiento por edad (negativo = declive).</summary>
        public static float AgeFactor(int age)
        {
            if (age <= 18) return 1.0f;
            if (age <= 21) return 0.85f;
            if (age <= 24) return 0.55f;
            if (age <= 27) return 0.25f;
            if (age <= 29) return 0.05f;
            if (age <= 31) return -0.2f;
            if (age <= 33) return -0.45f;
            return -0.8f;
        }

        /// <summary>
        /// Aplica una semana de evolución. <paramref name="minutesShare"/> es la fracción de minutos jugados recientemente (0–1).
        /// <paramref name="trainingMultiplier"/> refleja la intensidad del entrenamiento (1 = normal).
        /// Devuelve los atributos que cambiaron.
        /// </summary>
        public static Dictionary<AttributeId, int> ApplyWeek(Player p, DateTime date, float minutesShare, float trainingMultiplier,
                                                            TrainingFocus focus, float bonusPoints, DeterministicRandom rng)
        {
            var changes = new Dictionary<AttributeId, int>();
            int age = p.AgeOn(date);
            float ageF = AgeFactor(age);
            float points;
            if (ageF >= 0f)
            {
                int ovr = p.Overall;
                float margin = Math.Max(0, p.Potential - ovr);
                float marginF = (float)Math.Min(1.0, Math.Sqrt(margin / 10.0));
                float prof = 0.55f + p.Personality.Professionalism / 22f;
                float play = 0.55f + 0.65f * Clamp01(minutesShare);
                points = ageF * prof * play * trainingMultiplier * marginF * 1.05f + bonusPoints * marginF;
            }
            else
            {
                // Declive: lo frenan la profesionalidad y el entrenamiento, pero no se evita.
                float prof = 1.2f - p.Personality.Professionalism / 40f;
                points = ageF * prof * (1.15f - 0.15f * trainingMultiplier) * 0.8f;
            }

            p.ProgressAccumulator += points;
            int guard = 0;
            while (p.ProgressAccumulator >= 1f && guard++ < 10)
            {
                if (p.Overall >= p.Potential) { p.ProgressAccumulator = 0f; break; }
                var attr = PickGrowthAttribute(p, focus, rng);
                if (p.Attributes[attr] >= Attributes.Max) { p.ProgressAccumulator -= 1f; continue; }
                p.Attributes[attr] = p.Attributes[attr] + 1;
                changes[attr] = changes.TryGetValue(attr, out var c) ? c + 1 : 1;
                p.ProgressAccumulator -= 1f;
            }
            while (p.ProgressAccumulator <= -1f && guard++ < 20)
            {
                var attr = PickDeclineAttribute(p, age, rng);
                p.Attributes[attr] = p.Attributes[attr] - 1;
                changes[attr] = changes.TryGetValue(attr, out var c) ? c - 1 : -1;
                p.ProgressAccumulator += 1f;
            }
            return changes;
        }

        private static AttributeId PickGrowthAttribute(Player p, TrainingFocus focus, DeterministicRandom rng)
        {
            // Pesos: atributos clave de la posición + refuerzo del grupo entrenado.
            var weights = new List<(AttributeId attr, float w)>();
            foreach (var (attr, w) in PositionRatings.WeightsFor(p.Position)) weights.Add((attr, w * 3f));
            if (focus != TrainingFocus.Balanced)
            {
                foreach (AttributeId id in Enum.GetValues(typeof(AttributeId)))
                    if (MatchesFocus(id, focus)) weights.Add((id, 0.35f));
            }
            else
            {
                foreach (AttributeId id in Enum.GetValues(typeof(AttributeId)))
                    if (Attributes.GroupOf(id) != AttributeGroup.Goalkeeping || p.Position == Position.GK) weights.Add((id, 0.04f));
            }
            float total = weights.Sum(x => x.w);
            double r = rng.NextDouble() * total;
            foreach (var (attr, w) in weights)
            {
                r -= w;
                if (r <= 0) return attr;
            }
            return weights[weights.Count - 1].attr;
        }

        private static AttributeId PickDeclineAttribute(Player p, int age, DeterministicRandom rng)
        {
            // Primero se pierde el físico; con más edad, también la técnica.
            AttributeId[] physical = { AttributeId.Acceleration, AttributeId.SprintSpeed, AttributeId.Stamina, AttributeId.Agility, AttributeId.Jumping, AttributeId.Balance, AttributeId.Reactions };
            if (p.Position == Position.GK)
                physical = new[] { AttributeId.GkReflexes, AttributeId.GkDiving, AttributeId.Reactions, AttributeId.Agility, AttributeId.Jumping };
            if (age >= 33 && rng.Chance(0.35))
            {
                var keys = PositionRatings.WeightsFor(p.Position);
                return keys[rng.Range(0, keys.Count)].attr;
            }
            return physical[rng.Range(0, physical.Length)];
        }

        public static bool MatchesFocus(AttributeId id, TrainingFocus focus)
        {
            var g = Attributes.GroupOf(id);
            return focus switch
            {
                TrainingFocus.Pace => g == AttributeGroup.Pace || id == AttributeId.Agility,
                TrainingFocus.Shooting => g == AttributeGroup.Shooting || id == AttributeId.Heading,
                TrainingFocus.Passing => g == AttributeGroup.Passing,
                TrainingFocus.Dribbling => g == AttributeGroup.Dribbling,
                TrainingFocus.Defending => g == AttributeGroup.Defending || id == AttributeId.Heading,
                TrainingFocus.Physical => g == AttributeGroup.Physical || id == AttributeId.Balance,
                TrainingFocus.Goalkeeping => g == AttributeGroup.Goalkeeping || id == AttributeId.Reactions,
                _ => true,
            };
        }

        /// <summary>Forma reciente (−10…+10) a partir de las últimas notas.</summary>
        public static float FormFromRatings(IReadOnlyList<float> ratings)
        {
            if (ratings.Count == 0) return 0f;
            var last = ratings.Skip(Math.Max(0, ratings.Count - 5)).ToList();
            return Math.Max(-10f, Math.Min(10f, (last.Average() - 6.5f) * 4f));
        }

        public static void PushBounded<T>(List<T> list, T value, int max)
        {
            list.Add(value);
            while (list.Count > max) list.RemoveAt(0);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }

    /// <summary>Sesiones de entrenamiento del futbolista del usuario.</summary>
    public static class TrainingSystem
    {
        public static float Multiplier(TrainingIntensity i) => i switch
        {
            TrainingIntensity.Low => 0.8f,
            TrainingIntensity.High => 1.3f,
            _ => 1f,
        };

        /// <summary>
        /// Una sesión: nota de entrenamiento (la ve el entrenador), coste físico y riesgo de lesión.
        /// La progresión de atributos se aplica semanalmente en <see cref="ProgressionSystem"/>.
        /// </summary>
        public static TrainingResult Session(Player p, TrainingFocus focus, TrainingIntensity intensity, DeterministicRandom rng)
        {
            float prof = p.Personality.Professionalism / 20f;
            float effort = intensity == TrainingIntensity.High ? 0.6f : intensity == TrainingIntensity.Low ? -0.4f : 0f;
            float fatiguePenalty = Math.Max(0f, (70f - p.Condition.Fitness) / 30f);
            float grade = (float)rng.Gaussian(6.4 + 1.2 * prof + effort - fatiguePenalty, 0.7);
            grade = Math.Max(3f, Math.Min(10f, grade));
            float cost = intensity switch { TrainingIntensity.Low => 3f, TrainingIntensity.High => 11f, _ => 6f };
            float injuryRisk = intensity == TrainingIntensity.High ? 0.012f : intensity == TrainingIntensity.Normal ? 0.004f : 0.001f;
            injuryRisk *= 1f + fatiguePenalty * 2f;
            return new TrainingResult
            {
                Grade = (float)Math.Round(grade, 1),
                FitnessCost = cost,
                Injured = rng.Chance(injuryRisk),
            };
        }
    }
}
