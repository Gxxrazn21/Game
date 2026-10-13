using System;
using System.Collections.Generic;
using System.Linq;
using Dorsal.Core.Domain;
using Dorsal.Core.Match;

namespace Dorsal.Core.Simulation
{
    /// <summary>
    /// Selección automática de once y banquillo. Versión básica (M1): encaje posicional × media × estado físico.
    /// El entrenador IA del modo carrera (M4) añadirá forma, confianza y necesidades tácticas.
    /// </summary>
    public static class LineupSelector
    {
        public static MatchTeamSetup Select(Club club, IEnumerable<Player> squad, int benchSize = 7, Func<Player, bool>? isAvailable = null)
        {
            var available = squad.Where(p => isAvailable == null || isAvailable(p)).OrderBy(p => p.Id).ToList();
            var formation = Formation.Get(club.Tactics.Formation);
            var starters = new Player?[formation.Slots.Count];
            var used = new HashSet<int>();

            // Primero el portero, después el resto por orden de puesto.
            var order = Enumerable.Range(0, formation.Slots.Count).OrderBy(i => formation.Slots[i].Position == Position.GK ? 0 : 1).ThenBy(i => i);
            foreach (int i in order)
            {
                var slot = formation.Slots[i].Position;
                Player? best = null;
                float bestScore = float.MinValue;
                foreach (var p in available)
                {
                    if (used.Contains(p.Id)) continue;
                    float fitness = p.Condition.Fitness / 100f;
                    float score = p.OverallInSlot(slot) * (0.75f + 0.25f * fitness);
                    if (score > bestScore) { bestScore = score; best = p; }
                }
                if (best == null) throw new InvalidOperationException($"El club {club.Name} no tiene jugadores suficientes.");
                starters[i] = best;
                used.Add(best.Id);
            }

            var bench = new List<Player>();
            var gkBench = available.Where(p => !used.Contains(p.Id) && p.Position == Position.GK).OrderByDescending(p => p.Overall).FirstOrDefault();
            if (gkBench != null && benchSize > 0) { bench.Add(gkBench); used.Add(gkBench.Id); }
            foreach (var p in available.Where(p => !used.Contains(p.Id) && p.Position != Position.GK).OrderByDescending(p => p.Overall).ThenBy(p => p.Id))
            {
                if (bench.Count >= benchSize) break;
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
    }
}
