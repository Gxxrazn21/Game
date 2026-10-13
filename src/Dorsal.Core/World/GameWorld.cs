using System;
using System.Collections.Generic;
using System.Linq;
using Dorsal.Core.Domain;

namespace Dorsal.Core.World
{
    /// <summary>Estado completo del mundo de juego (clubes, jugadores, competiciones, calendario).</summary>
    public sealed class GameWorld
    {
        public Dictionary<int, Player> Players { get; set; } = new Dictionary<int, Player>();
        public Dictionary<int, Club> Clubs { get; set; } = new Dictionary<int, Club>();
        public List<Competition> Competitions { get; set; } = new List<Competition>();
        public List<Fixture> Fixtures { get; set; } = new List<Fixture>();
        public DateTime CurrentDate { get; set; }
        public int SeasonYear { get; set; }
        public int NextPlayerId { get; set; } = 1;
        public int NextClubId { get; set; } = 1;
        public int NextCompetitionId { get; set; } = 1;
        public int NextFixtureId { get; set; } = 1;

        public Player AddPlayer(Player p)
        {
            p.Id = NextPlayerId++;
            Players.Add(p.Id, p);
            return p;
        }

        public Club AddClub(Club c)
        {
            c.Id = NextClubId++;
            Clubs.Add(c.Id, c);
            return c;
        }

        public Competition AddCompetition(Competition c)
        {
            c.Id = NextCompetitionId++;
            Competitions.Add(c);
            return c;
        }

        public IEnumerable<Player> SquadOf(int clubId) =>
            Clubs[clubId].SquadPlayerIds.Select(id => Players[id]);

        /// <summary>Incorpora a un jugador a una plantilla manteniendo la coherencia jugador↔club.</summary>
        public void AssignToClub(int playerId, int clubId)
        {
            var p = Players[playerId];
            if (p.ClubId.HasValue) Clubs[p.ClubId.Value].SquadPlayerIds.Remove(playerId);
            Clubs[clubId].SquadPlayerIds.Add(playerId);
            p.ClubId = clubId;
        }

        /// <summary>Comprueba invariantes del mundo. Devuelve la lista de violaciones (vacía si todo es correcto).</summary>
        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            var seen = new Dictionary<int, int>();
            foreach (var club in Clubs.Values)
            {
                foreach (var pid in club.SquadPlayerIds)
                {
                    if (!Players.TryGetValue(pid, out var p))
                    {
                        errors.Add($"El club {club.Id} referencia al jugador inexistente {pid}.");
                        continue;
                    }
                    if (seen.TryGetValue(pid, out var other))
                        errors.Add($"El jugador {pid} está en las plantillas de {other} y {club.Id}.");
                    else
                        seen[pid] = club.Id;
                    if (p.ClubId != club.Id)
                        errors.Add($"El jugador {pid} figura en el club {club.Id} pero su ClubId es {p.ClubId}.");
                }
            }
            foreach (var p in Players.Values)
            {
                if (p.ClubId.HasValue && !seen.ContainsKey(p.Id))
                    errors.Add($"El jugador {p.Id} dice pertenecer al club {p.ClubId} pero no figura en su plantilla.");
            }
            foreach (var comp in Competitions)
            {
                if (comp.ClubIds.Distinct().Count() != comp.ClubIds.Count)
                    errors.Add($"La competición {comp.Id} tiene clubes duplicados.");
                foreach (var cid in comp.ClubIds)
                    if (!Clubs.ContainsKey(cid)) errors.Add($"La competición {comp.Id} referencia al club inexistente {cid}.");
            }
            return errors;
        }
    }
}
