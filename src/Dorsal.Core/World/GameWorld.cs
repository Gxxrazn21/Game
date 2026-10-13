using System;
using System.Collections.Generic;
using System.Linq;
using Dorsal.Core.Domain;

namespace Dorsal.Core.World
{
    /// <summary>Estado completo del mundo de juego (clubes, jugadores, competiciones, calendario, contratos).</summary>
    public sealed class GameWorld
    {
        public Dictionary<int, Player> Players { get; set; } = new Dictionary<int, Player>();
        public Dictionary<int, Club> Clubs { get; set; } = new Dictionary<int, Club>();
        public List<Competition> Competitions { get; set; } = new List<Competition>();
        public List<Fixture> Fixtures { get; set; } = new List<Fixture>();

        /// <summary>Contrato vigente de cada jugador con club (clave: id de jugador).</summary>
        public Dictionary<int, Contract> Contracts { get; set; } = new Dictionary<int, Contract>();

        public List<TransferRecord> Transfers { get; set; } = new List<TransferRecord>();

        /// <summary>Estadísticas de la temporada en curso (clave: id de jugador).</summary>
        public Dictionary<int, PlayerSeasonStats> SeasonStats { get; set; } = new Dictionary<int, PlayerSeasonStats>();

        public DateTime CurrentDate { get; set; }
        public DateTime SeasonStartDate { get; set; }
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

        public Competition? LeagueOf(int clubId) => Competitions.FirstOrDefault(c => c.ClubIds.Contains(clubId));

        public IEnumerable<Player> FreeAgents => Players.Values.Where(p => !p.ClubId.HasValue && !p.Retired);

        /// <summary>Incorpora a un jugador a una plantilla manteniendo la coherencia jugador↔club (sin tocar contratos).</summary>
        public void AssignToClub(int playerId, int clubId)
        {
            var p = Players[playerId];
            if (p.ClubId.HasValue) Clubs[p.ClubId.Value].SquadPlayerIds.Remove(playerId);
            if (!Clubs[clubId].SquadPlayerIds.Contains(playerId)) Clubs[clubId].SquadPlayerIds.Add(playerId);
            p.ClubId = clubId;
        }

        /// <summary>
        /// Traspaso atómico: mueve al jugador, sustituye su contrato, ajusta presupuestos y lo registra.
        /// Lanza una excepción (sin modificar nada) si la operación es imposible.
        /// </summary>
        public void TransferPlayer(int playerId, int toClubId, long fee, Contract newContract, TransferType type)
        {
            if (!Players.TryGetValue(playerId, out var p)) throw new InvalidOperationException($"Jugador inexistente {playerId}.");
            if (p.Retired) throw new InvalidOperationException($"El jugador {playerId} está retirado.");
            if (!Clubs.TryGetValue(toClubId, out var to)) throw new InvalidOperationException($"Club inexistente {toClubId}.");
            if (p.ClubId == toClubId && type != TransferType.LoanReturn) throw new InvalidOperationException("El jugador ya pertenece a ese club.");
            if (newContract.PlayerId != playerId || newContract.ClubId != toClubId) throw new InvalidOperationException("Contrato incoherente con el traspaso.");
            if (fee < 0) throw new InvalidOperationException("La cantidad del traspaso no puede ser negativa.");
            if (fee > 0 && to.TransferBudget < fee) throw new InvalidOperationException($"{to.Name} no tiene presupuesto para {fee}.");

            int? from = p.ClubId;
            if (from.HasValue && fee > 0) Clubs[from.Value].TransferBudget += fee;
            to.TransferBudget -= fee;
            AssignToClub(playerId, toClubId);
            Contracts[playerId] = newContract;
            p.ShirtNumber = FreeShirtNumber(toClubId, p.ShirtNumber, playerId);
            Transfers.Add(new TransferRecord { PlayerId = playerId, FromClubId = from, ToClubId = toClubId, Date = CurrentDate, Fee = fee, Type = type });
        }

        /// <summary>Deja libre a un jugador (fin de contrato o rescisión).</summary>
        public void ReleasePlayer(int playerId)
        {
            var p = Players[playerId];
            if (!p.ClubId.HasValue) return;
            int from = p.ClubId.Value;
            Clubs[from].SquadPlayerIds.Remove(playerId);
            p.ClubId = null;
            Contracts.Remove(playerId);
            Transfers.Add(new TransferRecord { PlayerId = playerId, FromClubId = from, Date = CurrentDate, Type = TransferType.Release });
        }

        public void RetirePlayer(int playerId)
        {
            var p = Players[playerId];
            int? from = p.ClubId;
            if (from.HasValue) Clubs[from.Value].SquadPlayerIds.Remove(playerId);
            p.ClubId = null;
            p.Retired = true;
            Contracts.Remove(playerId);
            Transfers.Add(new TransferRecord { PlayerId = playerId, FromClubId = from, Date = CurrentDate, Type = TransferType.Retirement });
        }

        public int FreeShirtNumber(int clubId, int? preferred, int playerId)
        {
            var used = new HashSet<int>(SquadOf(clubId).Where(x => x.Id != playerId && x.ShirtNumber.HasValue).Select(x => x.ShirtNumber!.Value));
            if (preferred.HasValue && preferred.Value > 0 && !used.Contains(preferred.Value)) return preferred.Value;
            for (int n = 2; n < 100; n++) if (!used.Contains(n)) return n;
            return 99;
        }

        public PlayerSeasonStats StatsFor(int playerId)
        {
            if (!SeasonStats.TryGetValue(playerId, out var s))
            {
                s = new PlayerSeasonStats { PlayerId = playerId, ClubId = Players[playerId].ClubId ?? 0 };
                SeasonStats[playerId] = s;
            }
            return s;
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
                    if (p.Retired) errors.Add($"El jugador retirado {pid} sigue en la plantilla de {club.Id}.");
                }
                var numbers = club.SquadPlayerIds.Where(Players.ContainsKey).Select(id => Players[id].ShirtNumber).Where(n => n.HasValue).ToList();
                if (numbers.Distinct().Count() != numbers.Count) errors.Add($"El club {club.Id} tiene dorsales repetidos.");
            }
            foreach (var p in Players.Values)
            {
                if (p.ClubId.HasValue && !seen.ContainsKey(p.Id))
                    errors.Add($"El jugador {p.Id} dice pertenecer al club {p.ClubId} pero no figura en su plantilla.");
                bool hasContract = Contracts.TryGetValue(p.Id, out var c);
                // Los mundos sin contratos (pruebas del motor) no se validan en este aspecto.
                if (Contracts.Count > 0)
                {
                    if (p.ClubId.HasValue && !hasContract) errors.Add($"El jugador {p.Id} tiene club pero no contrato.");
                    if (!p.ClubId.HasValue && hasContract) errors.Add($"El jugador libre {p.Id} conserva un contrato.");
                }
                if (hasContract && c!.ClubId != p.ClubId) errors.Add($"El contrato de {p.Id} es con {c.ClubId} pero juega en {p.ClubId}.");
                if (hasContract && c!.IsLoan && (!c.ParentClubId.HasValue || !Clubs.ContainsKey(c.ParentClubId.Value)))
                    errors.Add($"La cesión de {p.Id} no tiene club propietario válido.");
                if (hasContract && c!.EndDate < c.StartDate) errors.Add($"El contrato de {p.Id} termina antes de empezar.");
            }
            foreach (var comp in Competitions)
            {
                if (comp.ClubIds.Distinct().Count() != comp.ClubIds.Count)
                    errors.Add($"La competición {comp.Id} tiene clubes duplicados.");
                foreach (var cid in comp.ClubIds)
                    if (!Clubs.ContainsKey(cid)) errors.Add($"La competición {comp.Id} referencia al club inexistente {cid}.");
            }
            var leagueMembership = Competitions.Where(c => c.Rules.Type == CompetitionType.League).SelectMany(c => c.ClubIds).ToList();
            if (leagueMembership.Distinct().Count() != leagueMembership.Count) errors.Add("Un club está inscrito en dos ligas.");
            foreach (var club in Clubs.Values)
            {
                if (club.TransferBudget < 0) errors.Add($"El club {club.Id} tiene presupuesto negativo.");
                if (club.LeagueId.HasValue && !Competitions.Any(c => c.Id == club.LeagueId && c.ClubIds.Contains(club.Id)))
                    errors.Add($"El club {club.Id} apunta a la liga {club.LeagueId} pero no está inscrito.");
            }
            return errors;
        }
    }
}
