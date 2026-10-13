using System;
using System.Collections.Generic;
using System.Linq;
using Dorsal.Core.Domain;
using Dorsal.Core.Match;
using Dorsal.Core.World;

namespace Dorsal.Core.Career
{
    public sealed class MatchDayInfo
    {
        public Fixture? Fixture { get; set; }
        public Competition? Competition { get; set; }
        public MatchRole Role { get; set; }
        public string Objective { get; set; } = "";
        public string Instruction { get; set; } = "";
        public List<float> TrainingGrades { get; set; } = new List<float>();
        public List<string> Events { get; set; } = new List<string>();
    }

    public sealed class MatchDaySummary
    {
        public MatchReport? Report { get; set; }
        public PlayerMatchStats? UserStats { get; set; }
        public MatchRole Role { get; set; }
        public float TrustBefore { get; set; }
        public float TrustAfter { get; set; }
        public string Objective { get; set; } = "";
        public bool ObjectiveMet { get; set; }
        public long Earnings { get; set; }
        public int ExperienceGained { get; set; }
        public string CoachComment { get; set; } = "";
        public bool SeasonFinished { get; set; }
    }

    public sealed partial class CareerSession
    {
        private static readonly int[] TrainingWeekdays = { 2, 3, 5 }; // martes, miércoles, viernes

        /// <summary>
        /// Avanza hasta el próximo día de partido: entrenamientos, recuperación, lesiones, mercado y convocatorias.
        /// </summary>
        public MatchDayInfo AdvanceToNextMatchDay()
        {
            RequirePhase(CareerPhase.InSeason, CareerPhase.FreeAgent);
            int round = State.RoundsPlayed + 1;
            var roundFixtures = World.Fixtures.Where(f => f.Round == round && !f.IsPlayed).ToList();
            if (roundFixtures.Count == 0) throw new InvalidOperationException("No quedan jornadas en esta temporada.");
            var matchDate = roundFixtures.Min(f => f.Date);
            var info = new MatchDayInfo();
            State.WeekTrainingGrades.Clear();

            // Días hasta el partido.
            var day = World.CurrentDate;
            while (day < matchDate)
            {
                day = day.AddDays(1);
                World.CurrentDate = day;
                DailyRecovery();
                if (User.ClubId.HasValue && User.Injury == null && TrainingWeekdays.Contains((int)day.DayOfWeek) && day < matchDate)
                    TrainUser(info);
            }
            World.CurrentDate = matchDate;

            WeeklyProgression();
            RunMarketWeek(round, info);
            ExpireOffers();

            // Convocatorias de todos los clubes de la jornada.
            State.PendingLineups.Clear();
            foreach (var f in roundFixtures)
            {
                foreach (int clubId in new[] { f.HomeClubId, f.AwayClubId })
                {
                    var comp = World.LeagueOf(clubId)!;
                    var ctx = new SelectionContext
                    {
                        Date = matchDate,
                        UserPlayerId = State.UserPlayerId,
                        UserTrust = State.CoachTrust,
                        ContractOf = id => World.Contracts.TryGetValue(id, out var c) ? c : null,
                        BenchSize = comp.Rules.BenchSize,
                    };
                    var setup = CoachAI.Select(World.Clubs[clubId], World.SquadOf(clubId), ctx, _rng);
                    State.PendingLineups[clubId] = new LineupIds
                    {
                        ClubId = clubId,
                        Starters = setup.Starters.Select(p => p.Id).ToList(),
                        Bench = setup.Bench.Select(p => p.Id).ToList(),
                    };
                }
            }

            var userFixture = User.ClubId.HasValue
                ? roundFixtures.FirstOrDefault(f => f.HomeClubId == User.ClubId || f.AwayClubId == User.ClubId)
                : null;
            State.PendingFixtureId = userFixture?.Id;
            var userSetup = userFixture != null ? SetupFor(User.ClubId!.Value) : null;
            State.PendingRole = CoachAI.RoleOf(User, userSetup);
            State.PendingObjective = CoachAI.PreMatchObjective(User, State.PendingRole);
            State.PendingInstruction = UserClub != null ? CoachAI.TacticalInstruction(User, UserClub.Tactics) : "";
            State.PendingMatchSeed = ((ulong)_rng.NextUInt() << 32) | _rng.NextUInt();
            if (!User.ClubId.HasValue && _rng.Chance(0.25) && !PendingOffers.Any()) GenerateFreeAgentOffers();
            State.Phase = CareerPhase.MatchDay;

            info.Fixture = userFixture;
            info.Competition = userFixture != null ? World.Competitions.First(c => c.Id == userFixture.CompetitionId) : null;
            info.Role = State.PendingRole;
            info.Objective = State.PendingObjective;
            info.Instruction = State.PendingInstruction;
            info.TrainingGrades = State.WeekTrainingGrades.ToList();
            SyncRngToState();
            return info;
        }

        /// <summary>Alineaciones del partido del usuario (para el motor 3D o la simulación).</summary>
        public (MatchTeamSetup home, MatchTeamSetup away)? UserMatchSetups()
        {
            if (State.Phase != CareerPhase.MatchDay || !State.PendingFixtureId.HasValue) return null;
            var f = World.Fixtures.First(x => x.Id == State.PendingFixtureId.Value);
            return (SetupFor(f.HomeClubId), SetupFor(f.AwayClubId));
        }

        /// <summary>Motor de partido listo para jugar: controla al usuario si es titular.</summary>
        public MatchEngine? CreateUserMatchEngine(MatchConfig? config = null)
        {
            var setups = UserMatchSetups();
            if (setups == null) return null;
            var cfg = config ?? new MatchConfig { HalfDurationSeconds = State.Settings.HalfDurationSeconds, TickRate = State.Settings.TickRate };
            var comp = World.LeagueOf(setups.Value.home.ClubId);
            if (comp != null) cfg.MaxSubstitutions = comp.Rules.MaxSubstitutions;
            var engine = new MatchEngine(setups.Value.home, setups.Value.away, cfg, State.PendingMatchSeed);
            if (State.PendingRole == MatchRole.Starter || State.PendingRole == MatchRole.Substitute)
                engine.ControlledPlayerId = State.UserPlayerId; // si es suplente, se controla cuando entre
            return engine;
        }

        /// <summary>Simula el partido del usuario (motor completo sin gráficos, o simulación estadística si <paramref name="quick"/>).</summary>
        public MatchDaySummary SimulateUserMatch(bool quick = false)
        {
            RequirePhase(CareerPhase.MatchDay);
            MatchReport? report = null;
            var setups = UserMatchSetups();
            if (setups != null)
            {
                if (quick)
                {
                    var rng = new Random.DeterministicRandom(State.PendingMatchSeed);
                    report = QuickMatchSimulator.Simulate(setups.Value.home, setups.Value.away, rng);
                }
                else
                {
                    var engine = CreateUserMatchEngine(new MatchConfig { HalfDurationSeconds = 180f, TickRate = 20 })!;
                    engine.ControlledPlayerId = null;
                    // El entrenador mete al usuario suplente si el partido lo pide (simplificación: hacia el 65').
                    report = RunWithUserSubstitution(engine);
                }
            }
            return CompleteMatchDay(report);
        }

        private MatchReport RunWithUserSubstitution(MatchEngine engine)
        {
            bool requested = false;
            while (!engine.IsFinished)
            {
                engine.Step();
                if (!requested && State.PendingRole == MatchRole.Substitute && engine.ClockMinute >= 62f && State.CoachTrust >= 30f)
                {
                    var user = engine.Agents.FirstOrDefault(a => a.PlayerId == State.UserPlayerId);
                    if (user != null)
                    {
                        var outgoing = user.Team.OnPitch.Where(a => !a.IsGoalkeeper && a.Player.Position != Position.GK)
                                           .OrderBy(a => Math.Abs((int)a.Role - (int)User.Position)).ThenBy(a => a.Energy).FirstOrDefault();
                        if (outgoing != null) engine.RequestSubstitution(outgoing.PlayerId, user.PlayerId);
                    }
                    requested = true;
                }
            }
            return engine.BuildReport();
        }

        /// <summary>
        /// Cierra la jornada: aplica el informe del partido del usuario (si lo hay), simula el resto, actualiza todo
        /// y, si era la última jornada, cierra la temporada.
        /// </summary>
        public MatchDaySummary CompleteMatchDay(MatchReport? userReport)
        {
            RequirePhase(CareerPhase.MatchDay);
            int round = State.RoundsPlayed + 1;
            var userFixture = State.PendingFixtureId.HasValue ? World.Fixtures.First(f => f.Id == State.PendingFixtureId.Value) : null;
            if (userFixture != null && userReport != null &&
                (userReport.HomeClubId != userFixture.HomeClubId || userReport.AwayClubId != userFixture.AwayClubId))
                throw new ArgumentException("El informe no corresponde al partido pendiente.");
            if (userReport != null && !userReport.Finished) throw new ArgumentException("El partido no ha terminado.");

            var summary = new MatchDaySummary { Role = State.PendingRole, TrustBefore = State.CoachTrust, Objective = State.PendingObjective };

            foreach (var f in World.Fixtures.Where(x => x.Round == round && !x.IsPlayed).OrderBy(x => x.Id).ToList())
            {
                MatchReport report;
                if (userFixture != null && f.Id == userFixture.Id && userReport != null) report = userReport;
                else report = QuickMatchSimulator.Simulate(SetupFor(f.HomeClubId), SetupFor(f.AwayClubId), _rng);
                ApplyReport(f, report);
                if (userFixture != null && f.Id == userFixture.Id) summary.Report = report;
            }

            ProcessUserAfterMatch(summary);

            State.RoundsPlayed = round;
            State.PendingFixtureId = null;
            State.PendingLineups.Clear();
            State.Phase = User.ClubId.HasValue ? CareerPhase.InSeason : CareerPhase.FreeAgent;
            if (!World.Fixtures.Any(f => !f.IsPlayed))
            {
                EndSeason();
                summary.SeasonFinished = true;
            }
            SyncRngToState();
            return summary;
        }

        private MatchTeamSetup SetupFor(int clubId)
        {
            var club = World.Clubs[clubId];
            if (State.PendingLineups.TryGetValue(clubId, out var ids))
            {
                return new MatchTeamSetup
                {
                    ClubId = clubId,
                    Name = club.Name,
                    PrimaryColor = club.PrimaryColor,
                    SecondaryColor = club.SecondaryColor,
                    Tactics = club.Tactics.Clone(),
                    Starters = ids.Starters.Select(id => World.Players[id]).ToList(),
                    Bench = ids.Bench.Select(id => World.Players[id]).ToList(),
                };
            }
            return CoachAI.Select(club, World.SquadOf(clubId), new SelectionContext { Date = World.CurrentDate }, null);
        }

        private void ApplyReport(Fixture f, MatchReport report)
        {
            f.Result = new FixtureResult { HomeGoals = report.HomeGoals, AwayGoals = report.AwayGoals };
            var comp = World.Competitions.First(c => c.Id == f.CompetitionId);
            var played = new HashSet<int>();

            // Las sanciones vigentes se cumplen en esta jornada.
            foreach (int clubId in new[] { f.HomeClubId, f.AwayClubId })
                foreach (var p in World.SquadOf(clubId))
                    if (p.SuspensionMatches > 0) p.SuspensionMatches--;

            foreach (var s in report.PlayerStats)
            {
                if (!World.Players.TryGetValue(s.PlayerId, out var p)) continue;
                played.Add(p.Id);
                var st = World.StatsFor(p.Id);
                st.ClubId = s.ClubId;
                if (s.MinutesPlayed > 0)
                {
                    st.Appearances++;
                    if (s.Started) st.Starts++;
                    st.Minutes += s.MinutesPlayed;
                    st.Goals += s.Goals;
                    st.Assists += s.Assists;
                    if (Positions.LineOf(s.Position) == PositionLine.Goalkeeper && s.GoalsConceded == 0 && s.MinutesPlayed >= 60) st.CleanSheets++;
                }
                if (s.Rating.HasValue)
                {
                    st.RatingSum += s.Rating.Value;
                    st.RatedMatches++;
                    ProgressionSystem.PushBounded(p.RecentRatings, s.Rating.Value, 8);
                    p.Condition.Form = ProgressionSystem.FormFromRatings(p.RecentRatings);
                }
                ProgressionSystem.PushBounded(p.RecentMinutes, s.MinutesPlayed, 5);
                float stamina = p.Attributes.N(AttributeId.Stamina);
                p.Condition.Fitness = Math.Max(30f, p.Condition.Fitness - s.MinutesPlayed * 0.3f * (1.25f - 0.5f * stamina));

                st.YellowCards += s.YellowCards;
                if (s.RedCard)
                {
                    st.RedCards++;
                    p.SuspensionMatches += s.YellowCards >= 2 ? 1 : 2;
                }
                else if (s.YellowCards > 0)
                {
                    p.YellowAccumulated += s.YellowCards;
                    if (p.YellowAccumulated >= comp.Rules.YellowCardsForBan)
                    {
                        p.SuspensionMatches += 1;
                        p.YellowAccumulated = 0;
                    }
                }
                if (s.Injured && p.Injury == null) Injure(p, _rng.Range(5, 36));
            }
            foreach (int clubId in new[] { f.HomeClubId, f.AwayClubId })
                foreach (var p in World.SquadOf(clubId))
                    if (!played.Contains(p.Id)) ProgressionSystem.PushBounded(p.RecentMinutes, 0, 5);
        }

        private void ProcessUserAfterMatch(MatchDaySummary summary)
        {
            var report = summary.Report;
            var stats = report?.PlayerStats.FirstOrDefault(s => s.PlayerId == State.UserPlayerId);
            summary.UserStats = stats;
            var contract = UserContract;

            float delta = CoachAI.TrustDeltaAfterMatch(State.PendingRole, stats);
            bool objectiveMet = CoachAI.ObjectiveMet(User, State.PendingRole, stats);
            if (objectiveMet) delta += 2f;
            State.CoachTrust = Math.Max(0f, Math.Min(100f, State.CoachTrust + delta));
            summary.ObjectiveMet = objectiveMet;
            summary.TrustAfter = State.CoachTrust;

            long earnings = contract?.WageWeekly ?? 0;
            if (stats != null && stats.MinutesPlayed > 0 && contract != null)
            {
                earnings += contract.AppearanceBonus + contract.GoalBonus * stats.Goals;
                State.TotalAppearances++;
                State.TotalGoals += stats.Goals;
                State.TotalAssists += stats.Assists;
                if (State.TotalAppearances == 1) Unlock("Debut profesional");
                if (stats.Goals > 0 && State.TotalGoals == stats.Goals) Unlock("Primer gol profesional");
                if (stats.Goals >= 3) Unlock("Primer hat-trick");
                foreach (int milestone in new[] { 10, 25, 50, 100, 200 })
                    if (State.TotalGoals >= milestone && State.TotalGoals - stats.Goals < milestone) Unlock($"{milestone} goles en la carrera");
                foreach (int milestone in new[] { 50, 100, 250, 500 })
                    if (State.TotalAppearances == milestone) Unlock($"{milestone} partidos jugados");
            }
            State.Bank += earnings;
            summary.Earnings = earnings;

            if (stats?.Rating != null)
            {
                float r = stats.Rating.Value;
                int xp = (int)Math.Round(Math.Max(0f, r - 5f) * 10f * stats.MinutesPlayed / 90f) + stats.Goals * 15 + stats.Assists * 10;
                State.Experience += xp;
                summary.ExperienceGained = xp;
                // Las buenas actuaciones aceleran la progresión (sin superar el potencial).
                if (r > 6.3f) User.ProgressAccumulator += (r - 6.3f) * 0.35f;
                State.Popularity = Math.Min(100f, State.Popularity + Math.Max(-1f, (r - 6.5f) * 0.6f) + stats.Goals * 0.5f);
                User.Reputation = Math.Min(100, User.Reputation + (r >= 7.5f ? 1 : 0) + stats.Goals / 2);
                if (stats.Goals > 0) AddNews($"{User.DisplayName} marca {(stats.Goals == 1 ? "un gol" : stats.Goals + " goles")} (nota {r:F1}).", "partido");
                else if (r >= 8f) AddNews($"Gran actuación de {User.DisplayName}: nota {r:F1}.", "partido");
            }
            summary.CoachComment = CoachComment(State.PendingRole, stats, delta);

            if (report != null)
            {
                var evt = report.Events.FirstOrDefault(e => e.Type == MatchEventType.Injury && e.PlayerId == State.UserPlayerId);
                if (evt != null && User.Injury != null) AddNews($"{User.DisplayName} se lesiona: {User.Injury.Description} ({User.Injury.DaysRemaining} días).", "lesion");
            }
        }

        private static string CoachComment(MatchRole role, PlayerMatchStats? s, float delta)
        {
            if (role == MatchRole.Injured) return "Recupérate bien, te necesitamos.";
            if (role == MatchRole.Suspended) return "Cumple la sanción y vuelve con hambre.";
            if (role == MatchRole.NotSelected) return "Sigue trabajando en los entrenamientos; tu oportunidad llegará.";
            if (s == null || s.MinutesPlayed == 0) return "Hoy no hubo minutos. Mantente preparado.";
            if (delta >= 5f) return "Partidazo. Así te ganas el puesto.";
            if (delta >= 1.5f) return "Buen partido, sigue así.";
            if (delta > -1.5f) return "Correcto, pero puedes dar más.";
            return "No ha sido tu día. Tenemos que hablar de lo que espero de ti.";
        }

        // ---------------------------------------------------------------- semana

        private void DailyRecovery()
        {
            foreach (var p in World.Players.Values)
            {
                if (p.Retired) continue;
                float rec = 4f + 4f * p.Attributes.N(AttributeId.Stamina);
                p.Condition.Fitness = Math.Min(100f, p.Condition.Fitness + rec);
                if (p.Injury != null)
                {
                    p.Injury.DaysRemaining--;
                    if (p.Injury.DaysRemaining <= 0)
                    {
                        p.Injury = null;
                        p.Condition.Fitness = Math.Min(p.Condition.Fitness, 75f);
                        if (p.Id == State.UserPlayerId) AddNews($"{p.DisplayName} recibe el alta médica.", "lesion");
                    }
                }
            }
        }

        private void TrainUser(MatchDayInfo info)
        {
            var r = TrainingSystem.Session(User, State.TrainingFocus, State.TrainingIntensity, _rng);
            User.Condition.Fitness = Math.Max(20f, User.Condition.Fitness - r.FitnessCost);
            State.WeekTrainingGrades.Add(r.Grade);
            State.CoachTrust = Math.Max(0f, Math.Min(100f, State.CoachTrust + (r.Grade - 6.3f) * 0.5f));
            if (r.Injured)
            {
                Injure(User, _rng.Range(4, 18));
                info.Events.Add($"Lesión en el entrenamiento: {User.Injury!.Description}.");
                AddNews($"{User.DisplayName} se lesiona en el entrenamiento ({User.Injury.DaysRemaining} días).", "lesion");
            }
        }

        private void WeeklyProgression()
        {
            foreach (var p in World.Players.Values.OrderBy(x => x.Id))
            {
                if (p.Retired || !p.ClubId.HasValue) continue;
                float minutesShare = p.RecentMinutes.Count == 0 ? 0.3f : (float)p.RecentMinutes.Average() / 90f;
                bool isUser = p.Id == State.UserPlayerId;
                float training = isUser ? TrainingSystem.Multiplier(State.TrainingIntensity) : 1f;
                if (p.Injury != null) training *= 0.3f;
                var focus = isUser ? State.TrainingFocus : (p.Position == Position.GK ? TrainingFocus.Goalkeeping : TrainingFocus.Balanced);
                ProgressionSystem.ApplyWeek(p, World.CurrentDate, minutesShare, training, focus, 0f, _rng);
            }
        }

        private static readonly string[] InjuryNames = { "sobrecarga muscular", "esguince de tobillo", "contusión en la rodilla", "rotura fibrilar", "molestias en el aductor", "lesión en el isquiotibial" };

        private void Injure(Player p, int days)
        {
            p.Injury = new Injury
            {
                Description = InjuryNames[Math.Min(InjuryNames.Length - 1, days / 7)],
                DaysRemaining = days,
                StartDate = World.CurrentDate,
            };
        }
    }
}
