using System;
using System.Collections.Generic;
using System.Linq;
using Dorsal.Core.Domain;
using Dorsal.Core.Match;
using Dorsal.Core.Random;
using Dorsal.Core.World;

namespace Dorsal.Core.Career
{
    public enum MatchRole
    {
        Starter,
        Substitute,
        NotSelected,
        Injured,
        Suspended,
        NoClub,
    }

    /// <summary>Contexto que el entrenador tiene en cuenta además de la media.</summary>
    public sealed class SelectionContext
    {
        public DateTime Date { get; set; }

        /// <summary>Confianza del entrenador en el jugador del usuario (0–100). Solo afecta a ese jugador.</summary>
        public int? UserPlayerId { get; set; }
        public float UserTrust { get; set; } = 50f;
        public Func<int, Contract?>? ContractOf { get; set; }
        public int BenchSize { get; set; } = 7;
    }

    /// <summary>
    /// Entrenador IA: elige once y banquillo por puesto. La titularidad no depende solo de la media:
    /// pesan el encaje en el puesto, la forma, el estado físico, la confianza y el rol prometido en el contrato.
    /// </summary>
    public static class CoachAI
    {
        public static float SelectionScore(Player p, Position slot, SelectionContext ctx, DeterministicRandom? rng)
        {
            float ovr = p.OverallInSlot(slot);
            float fitness = p.Condition.Fitness / 100f;
            float score = ovr * (0.72f + 0.28f * fitness) + p.Condition.Form * 0.45f;
            var contract = ctx.ContractOf?.Invoke(p.Id);
            if (contract != null)
                score += contract.Role switch { SquadRole.Star => 2.5f, SquadRole.Starter => 1.2f, SquadRole.Prospect => -1.5f, _ => 0f };
            if (ctx.UserPlayerId == p.Id) score += (ctx.UserTrust - 50f) * 0.14f;
            if (rng != null) score += (float)rng.Gaussian(0, 0.8);
            return score;
        }

        public static MatchTeamSetup Select(Club club, IEnumerable<Player> squad, SelectionContext ctx, DeterministicRandom? rng)
        {
            var available = squad.Where(p => p.IsAvailable).OrderBy(p => p.Id).ToList();
            var formation = Formation.Get(club.Tactics.Formation);
            var starters = new Player?[formation.Slots.Count];
            var used = new HashSet<int>();

            // Puestos en orden de importancia: portero, ejes, después el resto.
            var order = Enumerable.Range(0, formation.Slots.Count)
                .OrderBy(i => formation.Slots[i].Position == Position.GK ? 0 : formation.Slots[i].Position == Position.ST || formation.Slots[i].Position == Position.CB ? 1 : 2)
                .ThenBy(i => i).ToList();
            var noise = available.ToDictionary(p => p.Id, p => rng == null ? 0f : (float)rng.Gaussian(0, 0.8));
            foreach (int i in order)
            {
                var slot = formation.Slots[i].Position;
                Player? best = null;
                float bestScore = float.MinValue;
                foreach (var p in available)
                {
                    if (used.Contains(p.Id)) continue;
                    if ((slot == Position.GK) != (p.Position == Position.GK) && available.Count(x => !used.Contains(x.Id) && (x.Position == Position.GK) == (slot == Position.GK)) > 0) continue;
                    float s = SelectionScore(p, slot, ctx, null) + noise[p.Id];
                    if (s > bestScore) { bestScore = s; best = p; }
                }
                // Si faltan jugadores disponibles se recurre a lesionados leves (no debería ocurrir con plantillas válidas).
                best ??= squad.Where(p => !used.Contains(p.Id) && !p.Retired).OrderByDescending(p => p.OverallInSlot(slot)).FirstOrDefault();
                if (best == null) throw new InvalidOperationException($"{club.Name} no tiene jugadores suficientes.");
                starters[i] = best;
                used.Add(best.Id);
            }

            var bench = new List<Player>();
            var gk = available.Where(p => !used.Contains(p.Id) && p.Position == Position.GK).OrderByDescending(p => p.Overall).FirstOrDefault();
            if (gk != null && ctx.BenchSize > 0) { bench.Add(gk); used.Add(gk.Id); }
            foreach (var p in available.Where(p => !used.Contains(p.Id) && p.Position != Position.GK)
                                       .OrderByDescending(p => SelectionScore(p, p.Position, ctx, null) + noise[p.Id]).ThenBy(p => p.Id))
            {
                if (bench.Count >= ctx.BenchSize) break;
                bench.Add(p);
                used.Add(p.Id);
            }

            return new MatchTeamSetup
            {
                ClubId = club.Id,
                Name = club.Name,
                PrimaryColor = club.PrimaryColor,
                SecondaryColor = club.SecondaryColor,
                Tactics = club.Tactics.Clone(),
                Starters = starters.Select(p => p!).ToList(),
                Bench = bench,
            };
        }

        public static MatchRole RoleOf(Player p, MatchTeamSetup? setup)
        {
            if (!p.ClubId.HasValue) return MatchRole.NoClub;
            if (p.Injury != null) return MatchRole.Injured;
            if (p.SuspensionMatches > 0) return MatchRole.Suspended;
            if (setup == null) return MatchRole.NotSelected;
            if (setup.Starters.Any(x => x.Id == p.Id)) return MatchRole.Starter;
            if (setup.Bench.Any(x => x.Id == p.Id)) return MatchRole.Substitute;
            return MatchRole.NotSelected;
        }

        /// <summary>Cambio de confianza tras un partido según la nota y lo esperado para su rol.</summary>
        public static float TrustDeltaAfterMatch(MatchRole role, PlayerMatchStats? stats)
        {
            if (stats == null || stats.MinutesPlayed == 0)
                return role == MatchRole.NotSelected ? -0.3f : 0f;
            float delta = 0f;
            if (stats.Rating.HasValue && stats.MinutesPlayed >= 15)
            {
                float expected = role == MatchRole.Starter ? 6.6f : 6.4f;
                delta = Math.Max(-6f, Math.Min(8f, (stats.Rating.Value - expected) * (role == MatchRole.Starter ? 5f : 4f)));
            }
            if (stats.RedCard) delta -= 5f;
            delta -= stats.YellowCards * 0.5f;
            return delta;
        }

        /// <summary>Objetivo previo al partido que el entrenador comunica al jugador.</summary>
        public static string PreMatchObjective(Player p, MatchRole role)
        {
            if (role != MatchRole.Starter && role != MatchRole.Substitute) return "";
            var line = Positions.LineOf(p.Position);
            if (role == MatchRole.Substitute) return "Si entras, aporta energía: nota mínima 6,5.";
            return line switch
            {
                PositionLine.Goalkeeper => "Mantén la portería a cero o haz al menos 3 paradas.",
                PositionLine.Defence => "Gana duelos: 3 entradas o intercepciones y nota 6,8.",
                PositionLine.Midfield => "Mueve el balón: 80 % de pases completados y 1 pase clave.",
                _ => "Amenaza la portería: 2 tiros a puerta o una participación en gol.",
            };
        }

        public static bool ObjectiveMet(Player p, MatchRole role, PlayerMatchStats? s)
        {
            if (s == null || s.MinutesPlayed == 0) return false;
            if (role == MatchRole.Substitute) return (s.Rating ?? 0f) >= 6.5f;
            return Positions.LineOf(p.Position) switch
            {
                PositionLine.Goalkeeper => s.GoalsConceded == 0 || s.Saves >= 3,
                PositionLine.Defence => s.TacklesWon + s.Interceptions >= 3 && (s.Rating ?? 0f) >= 6.8f,
                PositionLine.Midfield => s.PassesAttempted >= 5 && s.PassAccuracy >= 0.8f && s.KeyPasses >= 1,
                _ => s.ShotsOnTarget >= 2 || s.Goals + s.Assists >= 1,
            };
        }

        public static string TacticalInstruction(Player p, Tactics t)
        {
            var line = Positions.LineOf(p.Position);
            if (line == PositionLine.Goalkeeper) return t.DefensiveLine > 60 ? "Línea alta: sal a cortar balones a la espalda de la defensa." : "Quédate cerca de la línea y ordena a la defensa.";
            if (t.Pressing > 65) return "Presión alta: aprieta al rival en cuanto pierda el balón.";
            if (t.Tempo > 65) return "Juego directo: busca el espacio a la espalda rápidamente.";
            if (t.Width > 65 && line != PositionLine.Defence) return "Amplitud: ábrete a la banda para estirar al rival.";
            return line == PositionLine.Defence ? "Mantén la línea y no te precipites en las entradas." : "Paciencia con el balón: asegura el pase.";
        }
    }
}
