using System;
using System.Linq;
using Dorsal.Core.Domain;
using Dorsal.Core.Random;

namespace Dorsal.Core.World
{
    /// <summary>Crea jugadores ficticios coherentes con su posición, nivel y físico.</summary>
    public static class PlayerFactory
    {
        /// <summary>
        /// Genera atributos cuya media en <paramref name="position"/> se aproxima a <paramref name="targetOverall"/>.
        /// Los atributos clave de la posición quedan por encima de la media y los irrelevantes por debajo.
        /// </summary>
        public static Attributes GenerateAttributes(Position position, int targetOverall, DeterministicRandom rng,
                                                    int heightCm = 180, Build build = Build.Normal)
        {
            var a = new Attributes();
            var weights = PositionRatings.WeightsFor(position).ToDictionary(w => w.attr, w => w.w);
            bool isGk = position == Position.GK;

            foreach (AttributeId id in Enum.GetValues(typeof(AttributeId)))
            {
                bool gkAttr = Attributes.GroupOf(id) == AttributeGroup.Goalkeeping;
                double offset;
                if (weights.TryGetValue(id, out float w)) offset = w >= 0.1f ? 5 : 1;
                else if (gkAttr && !isGk) offset = -targetOverall + 12; // Jugadores de campo: 10–20 en portería.
                else if (isGk && !gkAttr) offset = -18;
                else offset = -7;
                a[id] = (int)Math.Round(targetOverall + offset + rng.Gaussian(0, 5));
            }

            ApplyPhysique(a, heightCm, build);

            // Ajuste iterativo de los atributos ponderados para clavar la media objetivo.
            for (int i = 0; i < 6; i++)
            {
                int diff = targetOverall - PositionRatings.Overall(a, position);
                if (diff == 0) break;
                foreach (var id in weights.Keys) a[id] = a[id] + diff;
            }
            return a;
        }

        /// <summary>La altura y la complexión desplazan atributos físicos (más alto: salto y cabeza; más bajo: agilidad).</summary>
        public static void ApplyPhysique(Attributes a, int heightCm, Build build)
        {
            int h = (heightCm - 180) / 4; // ±6 aprox. en el rango 155–205 cm.
            a[AttributeId.Jumping] += h;
            a[AttributeId.Heading] += h;
            a[AttributeId.Strength] += h / 2;
            a[AttributeId.Agility] -= h;
            a[AttributeId.Balance] -= h;
            a[AttributeId.Acceleration] -= h / 2;
            switch (build)
            {
                case Build.Lean:
                    a[AttributeId.Acceleration] += 3; a[AttributeId.Agility] += 2; a[AttributeId.Strength] -= 4;
                    break;
                case Build.Stocky:
                    a[AttributeId.Strength] += 5; a[AttributeId.Balance] += 2; a[AttributeId.Acceleration] -= 3; a[AttributeId.Agility] -= 2;
                    break;
            }
        }

        public static int HeightFor(Position position, DeterministicRandom rng)
        {
            double mean = position switch
            {
                Position.GK => 189,
                Position.CB => 187,
                Position.ST => 183,
                Position.CDM => 182,
                Position.LW or Position.RW or Position.CAM => 175,
                _ => 179,
            };
            return (int)Math.Round(Math.Max(160, Math.Min(204, rng.Gaussian(mean, 5.5))));
        }

        /// <summary>Potencial según edad: los jóvenes tienen más margen, los veteranos ninguno.</summary>
        public static int PotentialFor(int overall, int age, DeterministicRandom rng)
        {
            double margin = age <= 18 ? rng.Range(8, 26) :
                            age <= 21 ? rng.Range(5, 18) :
                            age <= 24 ? rng.Range(2, 10) :
                            age <= 27 ? rng.Range(0, 4) : 0;
            return Math.Min(95, overall + (int)Math.Round(margin));
        }
    }
}
