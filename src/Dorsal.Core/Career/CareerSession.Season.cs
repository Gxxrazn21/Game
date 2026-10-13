using System;
using System.Collections.Generic;
using System.Linq;
using Dorsal.Core.Domain;
using Dorsal.Core.Simulation;
using Dorsal.Core.World;

namespace Dorsal.Core.Career
{
    public sealed class SeasonSummary
    {
        public int SeasonYear { get; set; }
        public Dictionary<int, List<TableRow>> Tables { get; set; } = new Dictionary<int, List<TableRow>>();
        public List<CareerAward> Awards { get; set; } = new List<CareerAward>();
        public List<int> Promoted { get; set; } = new List<int>();
        public List<int> Relegated { get; set; } = new List<int>();
        public SeasonRecord? UserRecord { get; set; }
    }

    public sealed partial class CareerSession
    {
        public SeasonSummary? LastSeasonSummary { get; private set; }

        /// <summary>Objetivos de temporada según el rol del jugador y el nivel del club.</summary>
        private void GenerateObjectives()
        {
            State.Objectives.Clear();
            var club = UserClub;
            if (club == null) return;
            var comp = World.LeagueOf(club.Id)!;
            int rounds = Math.Max(1, TotalRounds - State.RoundsPlayed);
            var role = UserContract?.Role ?? SquadRole.Rotation;
            float share = role switch { SquadRole.Prospect => 0.3f, SquadRole.Rotation => 0.5f, SquadRole.Starter => 0.75f, _ => 0.85f };
            int apps = Math.Max(1, (int)Math.Round(rounds * share));
            State.Objectives.Add(new CareerObjective { Kind = ObjectiveKind.Appearances, Target = apps, Description = $"Jugar al menos {apps} partidos" });

            var line = Positions.LineOf(User.Position);
            if (line == PositionLine.Goalkeeper)
            {
                int cs = Math.Max(1, (int)Math.Round(apps * 0.25f));
                State.Objectives.Add(new CareerObjective { Kind = ObjectiveKind.CleanSheets, Target = cs, Description = $"Mantener la portería a cero {cs} veces" });
            }
            else if (line != PositionLine.Defence)
            {
                float rate = line == PositionLine.Attack ? 0.4f : 0.22f;
                int gc = Math.Max(1, (int)Math.Round(apps * rate));
                State.Objectives.Add(new CareerObjective { Kind = ObjectiveKind.GoalContributions, Target = gc, Description = $"Sumar {gc} goles + asistencias" });
            }
            float avg = role == SquadRole.Star ? 7.0f : role == SquadRole.Starter ? 6.8f : 6.5f;
            State.Objectives.Add(new CareerObjective { Kind = ObjectiveKind.AverageRating, Target = avg, Description = $"Nota media de {avg:F1} o más" });

            // Posición esperada del club según su nivel en la liga.
            int expected = comp.ClubIds.Select(id => World.Clubs[id]).OrderByDescending(c => c.Level).ThenBy(c => c.Id).ToList().FindIndex(c => c.Id == club.Id) + 1;
            int target = Math.Max(1, expected + 1);
            State.Objectives.Add(new CareerObjective { Kind = ObjectiveKind.LeaguePosition, Target = target, Description = $"Terminar entre los {target} primeros de {comp.Name}" });
        }

        /// <summary>Progreso actual de un objetivo (valor conseguido hasta ahora).</summary>
        public float ObjectiveProgress(CareerObjective o)
        {
            var st = World.SeasonStats.TryGetValue(State.UserPlayerId, out var s) ? s : new PlayerSeasonStats();
            return o.Kind switch
            {
                ObjectiveKind.Appearances => st.Appearances,
                ObjectiveKind.GoalContributions => st.Goals + st.Assists,
                ObjectiveKind.CleanSheets => st.CleanSheets,
                ObjectiveKind.AverageRating => st.AverageRating,
                ObjectiveKind.LeaguePosition => User.ClubId.HasValue ? LeaguePositionOf(User.ClubId.Value) : 99,
                _ => 0f,
            };
        }

        private bool ObjectiveAchieved(CareerObjective o)
        {
            float v = ObjectiveProgress(o);
            return o.Kind == ObjectiveKind.LeaguePosition ? v > 0 && v <= o.Target : v >= o.Target;
        }

        private void EndSeason()
        {
            var summary = new SeasonSummary { SeasonYear = World.SeasonYear };
            foreach (var comp in World.Competitions.OrderBy(c => c.Tier))
                summary.Tables[comp.Id] = TableFor(comp);

            // Premios por liga.
            foreach (var comp in World.Competitions.OrderBy(c => c.Tier))
            {
                var stats = World.SeasonStats.Values.Where(s => comp.ClubIds.Contains(s.ClubId) && World.Players.ContainsKey(s.PlayerId)).ToList();
                int rounds = World.Fixtures.Where(f => f.CompetitionId == comp.Id).Select(f => f.Round).DefaultIfEmpty(1).Max();
                void Award(string name, PlayerSeasonStats? s)
                {
                    if (s == null) return;
                    var a = new CareerAward { SeasonYear = World.SeasonYear, Name = name, Competition = comp.Name };
                    summary.Awards.Add(a);
                    var p = World.Players[s.PlayerId];
                    if (p.Id == State.UserPlayerId)
                    {
                        State.Awards.Add(a);
                        User.Reputation = Math.Min(100, User.Reputation + 5);
                        AddNews($"{User.DisplayName} gana el premio {name} de {comp.Name}.", "premio");
                        Unlock($"Premio: {name}");
                    }
                }
                Award("Máximo goleador", stats.Where(s => s.Goals > 0).OrderByDescending(s => s.Goals).ThenByDescending(s => s.AverageRating).ThenBy(s => s.PlayerId).FirstOrDefault());
                Award("Mejor jugador", stats.Where(s => s.Appearances >= rounds / 2).OrderByDescending(s => s.AverageRating).ThenBy(s => s.PlayerId).FirstOrDefault());
                Award("Mejor joven", stats.Where(s => s.Appearances >= rounds / 3 && World.Players[s.PlayerId].AgeOn(World.CurrentDate) <= 21)
                                          .OrderByDescending(s => s.AverageRating).ThenBy(s => s.PlayerId).FirstOrDefault());
                Award("Mejor portero", stats.Where(s => World.Players[s.PlayerId].Position == Position.GK && s.Appearances >= rounds / 2)
                                            .OrderByDescending(s => s.CleanSheets).ThenByDescending(s => s.AverageRating).ThenBy(s => s.PlayerId).FirstOrDefault());
            }

            // Registro de la temporada del usuario.
            if (User.ClubId.HasValue || World.SeasonStats.ContainsKey(State.UserPlayerId))
            {
                var st = World.SeasonStats.TryGetValue(State.UserPlayerId, out var s) ? s : new PlayerSeasonStats();
                int clubId = User.ClubId ?? st.ClubId;
                var comp = World.LeagueOf(clubId);
                int completed = 0;
                foreach (var o in State.Objectives.Where(x => !x.Evaluated))
                {
                    o.Evaluated = true;
                    o.Completed = ObjectiveAchieved(o);
                    if (o.Completed) completed++;
                }
                State.CoachTrust = Math.Max(0f, Math.Min(100f, State.CoachTrust + completed * 3f - (State.Objectives.Count - completed) * 2f));
                int pos = clubId != 0 ? LeaguePositionOf(clubId) : 0;
                var record = new SeasonRecord
                {
                    SeasonYear = World.SeasonYear,
                    ClubId = clubId,
                    ClubName = clubId != 0 ? World.Clubs[clubId].Name : "",
                    LeagueName = comp?.Name ?? "",
                    OnLoan = UserContract?.IsLoan ?? false,
                    Appearances = st.Appearances,
                    Starts = st.Starts,
                    Minutes = st.Minutes,
                    Goals = st.Goals,
                    Assists = st.Assists,
                    CleanSheets = st.CleanSheets,
                    AverageRating = (float)Math.Round(st.AverageRating, 2),
                    LeaguePosition = pos,
                    OverallAtEnd = User.Overall,
                    Awards = State.Awards.Where(a => a.SeasonYear == World.SeasonYear).Select(a => a.Name).ToList(),
                    ObjectivesCompleted = completed,
                    ObjectivesTotal = State.Objectives.Count,
                };
                State.History.Add(record);
                summary.UserRecord = record;
                if (pos == 1 && st.Appearances >= 5 && comp != null)
                {
                    Unlock($"Campeón de {comp.Name}");
                    AddNews($"¡{World.Clubs[clubId].Name} campeón de {comp.Name}!", "premio");
                }
            }

            ApplyPromotionAndRelegation(summary);
            UpdateClubsAfterSeason(summary);
            LastSeasonSummary = summary;
            State.Phase = CareerPhase.SeasonEnded;
            AddNews($"Fin de la temporada {World.SeasonYear}/{(World.SeasonYear + 1) % 100:00}.", "temporada");
        }

        private void ApplyPromotionAndRelegation(SeasonSummary summary)
        {
            var leagues = World.Competitions.Where(c => c.Rules.Type == CompetitionType.League).OrderBy(c => c.Tier).ToList();
            for (int i = 0; i + 1 < leagues.Count; i++)
            {
                var upper = leagues[i];
                var lower = leagues[i + 1];
                int n = Math.Min(upper.Rules.RelegationSlots, lower.Rules.PromotionSlots);
                if (n <= 0) continue;
                var down = summary.Tables[upper.Id].Skip(upper.ClubIds.Count - n).Select(r => r.ClubId).ToList();
                var up = summary.Tables[lower.Id].Take(n).Select(r => r.ClubId).ToList();
                foreach (var id in down) { upper.ClubIds.Remove(id); lower.ClubIds.Add(id); World.Clubs[id].LeagueId = lower.Id; }
                foreach (var id in up) { lower.ClubIds.Remove(id); upper.ClubIds.Add(id); World.Clubs[id].LeagueId = upper.Id; }
                summary.Relegated.AddRange(down);
                summary.Promoted.AddRange(up);
                foreach (var id in up)
                {
                    AddNews($"{World.Clubs[id].Name} asciende a {upper.Name}.", "temporada");
                    if (id == User.ClubId) Unlock("Ascenso");
                }
                foreach (var id in down) AddNews($"{World.Clubs[id].Name} desciende a {lower.Name}.", "temporada");
            }
        }

        private void UpdateClubsAfterSeason(SeasonSummary summary)
        {
            foreach (var comp in World.Competitions)
            {
                var table = summary.Tables[comp.Id];
                for (int i = 0; i < table.Count; i++)
                {
                    var club = World.Clubs[table[i].ClubId];
                    float rel = 1f - 2f * i / Math.Max(1, table.Count - 1); // +1 líder … −1 colista
                    club.Reputation = Math.Max(1, Math.Min(100, club.Reputation + (int)Math.Round(rel * 3)));
                    long prize = (long)((comp.Tier == 1 ? 4_000_000 : 1_200_000) * (1.5 + rel));
                    club.TransferBudget += Economy.RoundNice(prize);
                }
            }
        }

        /// <summary>Pretemporada: progresión de verano, contratos, retiradas, cantera, mercado y nuevo calendario.</summary>
        public void StartNextSeason()
        {
            RequirePhase(CareerPhase.SeasonEnded);
            int oldYear = World.SeasonYear;
            var seasonEnd = WorldGenerator.SeasonEnd(oldYear);
            World.CurrentDate = seasonEnd.AddDays(1); // 1 de julio: los contratos nuevos cuentan desde esta temporada

            // Progresión de verano (equivale a varias semanas de trabajo).
            foreach (var p in World.Players.Values.OrderBy(x => x.Id))
            {
                if (p.Retired) continue;
                var st = World.SeasonStats.TryGetValue(p.Id, out var s) ? s : null;
                float share = st == null ? 0.2f : Math.Min(1f, st.Minutes / (90f * Math.Max(1, TotalRounds)));
                for (int w = 0; w < 6; w++)
                    ProgressionSystem.ApplyWeek(p, World.CurrentDate, share, 1f, p.Position == Position.GK ? TrainingFocus.Goalkeeping : TrainingFocus.Balanced, 0f, _rng);
                p.Condition.Fitness = 100f;
                p.Injury = p.Injury != null && p.Injury.DaysRemaining > 40 ? p.Injury : null;
                p.SuspensionMatches = 0;
                p.YellowAccumulated = 0;
                p.RecentMinutes.Clear();
            }

            ResolveLoans(seasonEnd);
            ResolveExpiringContracts(seasonEnd);
            RetirePlayers();
            YouthIntake(oldYear + 1);
            TransferAI.RunWindow(World, _rng, State.UserPlayerId, 2);
            TransferAI.TrimSquads(World, State.UserPlayerId);
            YouthIntake(oldYear + 1);
            foreach (var club in World.Clubs.Values) club.Level = ClubLevelFromSquad(club);
            PruneHistory();

            // Nueva temporada.
            World.SeasonYear = oldYear + 1;
            World.SeasonStartDate = World.SeasonStartDate.AddYears(1);
            World.CurrentDate = World.SeasonStartDate.AddDays(-14);
            World.SeasonStats.Clear();
            World.Fixtures.Clear();
            foreach (var comp in World.Competitions.OrderBy(c => c.Tier))
                World.Fixtures.AddRange(FixtureGenerator.Generate(comp, World.SeasonStartDate, () => World.NextFixtureId++));
            State.RoundsPlayed = 0;
            State.TrialsUsed.Clear();
            foreach (var o in PendingOffers.Where(o => o.Kind != OfferKind.FreeAgent).ToList()) o.Status = OfferStatus.Expired;

            int age = User.AgeOn(World.CurrentDate);
            if (age >= 40)
            {
                RetireUser("Retirada a los 40 años");
                SyncRngToState();
                return;
            }
            if (!User.ClubId.HasValue)
            {
                State.Phase = CareerPhase.FreeAgent;
                if (!PendingOffers.Any()) GenerateFreeAgentOffers();
            }
            else
            {
                State.Phase = CareerPhase.InSeason;
                GenerateObjectives();
            }
            AddNews($"Arranca la temporada {World.SeasonYear}/{(World.SeasonYear + 1) % 100:00}.", "temporada");
            SyncRngToState();
        }

        private void ResolveLoans(DateTime seasonEnd)
        {
            foreach (var c in World.Contracts.Values.Where(c => c.IsLoan && c.EndDate <= seasonEnd).ToList())
            {
                int playerId = c.PlayerId;
                int parentId = c.ParentClubId!.Value;
                Contract back;
                if (playerId == State.UserPlayerId && State.ParentContract != null)
                {
                    back = State.ParentContract;
                    State.ParentContract = null;
                }
                else
                {
                    back = new Contract { PlayerId = playerId, ClubId = parentId, StartDate = World.CurrentDate, EndDate = WorldGenerator.SeasonEnd(World.SeasonYear + 1), WageWeekly = c.WageWeekly, Role = SquadRole.Rotation };
                }
                back.ClubId = parentId;
                World.TransferPlayer(playerId, parentId, 0, back, TransferType.LoanReturn);
                if (playerId == State.UserPlayerId)
                {
                    StartAtNewClub();
                    AddNews($"{User.DisplayName} vuelve de su cesión a {World.Clubs[parentId].Name}.", "mercado");
                }
            }
        }

        private void ResolveExpiringContracts(DateTime seasonEnd)
        {
            foreach (var c in World.Contracts.Values.Where(c => c.EndDate <= seasonEnd).OrderBy(c => c.PlayerId).ToList())
            {
                var p = World.Players[c.PlayerId];
                if (p.Id == State.UserPlayerId)
                {
                    World.ReleasePlayer(p.Id);
                    AddNews($"{User.DisplayName} termina contrato y queda libre.", "mercado");
                    continue;
                }
                var club = World.Clubs[c.ClubId];
                int age = p.AgeOn(World.CurrentDate);
                bool renew = age <= 31 && p.Overall >= club.Level - 9 && _rng.Chance(0.75);
                if (renew)
                {
                    c.EndDate = WorldGenerator.SeasonEnd(World.SeasonYear + (age <= 27 ? _rng.Range(2, 5) : _rng.Range(1, 3)));
                    c.Role = Economy.RoleFor(p.Overall, club.Level, age);
                    c.WageWeekly = Economy.FairWage(p, club.Level, c.Role);
                }
                else World.ReleasePlayer(p.Id);
            }
        }

        private void RetirePlayers()
        {
            int minLevel = World.Clubs.Values.Min(c => c.Level);
            foreach (var p in World.Players.Values.Where(p => !p.Retired && p.Id != State.UserPlayerId).OrderBy(p => p.Id).ToList())
            {
                int age = p.AgeOn(World.CurrentDate);
                double chance = age < 33 ? 0 : 0.2 + 0.15 * (age - 33);
                if (!p.ClubId.HasValue && age >= 28) chance = Math.Max(chance, 0.6);
                if (!p.ClubId.HasValue && p.Overall < minLevel - 10) chance = 1.0; // ningún club lo ficharía
                if (age >= 39 || _rng.Chance(chance)) World.RetirePlayer(p.Id);
            }
            // La bolsa de agentes libres se mantiene acotada.
            foreach (var p in World.FreeAgents.Where(p => p.Id != State.UserPlayerId).OrderByDescending(p => p.Overall).ThenBy(p => p.Id).Skip(60).ToList())
                World.RetirePlayer(p.Id);
        }

        private void YouthIntake(int seasonYear)
        {
            var names = new NameGenerator(_rng.Fork(), World.Players.Values.Select(p => p.FullName));
            var start = new DateTime(seasonYear, 7, 1);
            Position[] need = { Position.GK, Position.CB, Position.LB, Position.RB, Position.CDM, Position.CM, Position.CAM, Position.LM, Position.RM, Position.LW, Position.RW, Position.ST };
            foreach (var club in World.Clubs.Values.OrderBy(c => c.Id))
            {
                int guard = 0;
                while (club.SquadPlayerIds.Count < 22 && guard++ < 10)
                {
                    // Prioridad a la posición menos cubierta.
                    var pos = need.OrderBy(x => World.SquadOf(club.Id).Count(p => p.Position == x) - (x == Position.GK ? 1 : 0)).ThenBy(x => (int)x).First();
                    var p = WorldGenerator.CreateSquadPlayer(pos, club.Level - 8, start, _rng, names);
                    p.BirthDate = start.AddYears(-_rng.Range(17, 20)).AddDays(-_rng.Range(0, 365));
                    p.Potential = Math.Min(92, p.Overall + _rng.Range(8, 24));
                    World.AddPlayer(p);
                    var contract = new Contract
                    {
                        PlayerId = p.Id,
                        ClubId = club.Id,
                        StartDate = World.CurrentDate,
                        EndDate = WorldGenerator.SeasonEnd(seasonYear + 2),
                        WageWeekly = Economy.FairWage(p, club.Level, SquadRole.Prospect),
                        Role = SquadRole.Prospect,
                    };
                    World.AssignToClub(p.Id, club.Id);
                    World.Contracts[p.Id] = contract;
                    p.ShirtNumber = World.FreeShirtNumber(club.Id, null, p.Id);
                    World.Transfers.Add(new TransferRecord { PlayerId = p.Id, ToClubId = club.Id, Date = World.CurrentDate, Type = TransferType.YouthIntake });
                }
            }
        }

        /// <summary>Mantiene acotado el tamaño del guardado: elimina retirados y traspasos antiguos.</summary>
        private void PruneHistory()
        {
            foreach (var id in World.Players.Values.Where(p => p.Retired && p.Id != State.UserPlayerId).Select(p => p.Id).ToList())
            {
                World.Players.Remove(id);
                World.SeasonStats.Remove(id);
            }
            var cutoff = World.CurrentDate.AddYears(-2);
            World.Transfers.RemoveAll(t => t.Date < cutoff && t.PlayerId != State.UserPlayerId);
        }

        private int ClubLevelFromSquad(Club club)
        {
            var best = World.SquadOf(club.Id).OrderByDescending(p => p.Overall).Take(11).Select(p => p.Overall).ToList();
            if (best.Count == 0) return club.Level;
            // Se mueve hacia la calidad real de la plantilla, sin saltos bruscos.
            int squadLevel = (int)Math.Round(best.Average());
            return (int)Math.Round(club.Level * 0.5 + squadLevel * 0.5);
        }

        /// <summary>Retirada voluntaria (desde los 33 años).</summary>
        public void Retire()
        {
            RequirePhase(CareerPhase.InSeason, CareerPhase.SeasonEnded, CareerPhase.FreeAgent);
            int age = User.AgeOn(World.CurrentDate);
            if (age < 33) throw new InvalidOperationException("La retirada voluntaria está disponible desde los 33 años.");
            RetireUser("Retirada voluntaria");
            SyncRngToState();
        }

        private void RetireUser(string reason)
        {
            World.RetirePlayer(State.UserPlayerId);
            State.Phase = CareerPhase.Retired;
            AddNews($"{User.FullName} cuelga las botas ({reason}). {State.TotalAppearances} partidos, {State.TotalGoals} goles.", "carrera");
            Unlock("Leyenda retirada");
        }
    }
}
