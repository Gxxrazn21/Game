using System;
using System.Collections.Generic;
using System.Linq;
using Dorsal.Core.Domain;
using Dorsal.Core.Match;
using Dorsal.Core.Random;

namespace Dorsal.Core.Career
{
    /// <summary>
    /// Simulación estadística instantánea para los partidos en los que no participa el usuario.
    /// Produce el mismo <see cref="MatchReport"/> que el motor completo para procesarlo de forma uniforme.
    /// </summary>
    public static class QuickMatchSimulator
    {
        public sealed class Strength
        {
            public float Attack;
            public float Defence;
            public float Midfield;
            public float Goalkeeper;
        }

        public static Strength Evaluate(MatchTeamSetup team)
        {
            float attSum = 0, attW = 0, defSum = 0, defW = 0, midSum = 0, midW = 0, gk = 50;
            foreach (var p in team.Starters)
            {
                var a = p.Attributes;
                var line = Positions.LineOf(p.Position);
                if (line == PositionLine.Goalkeeper) { gk = PositionRatings.Overall(a, Position.GK); continue; }
                float attack = (a[AttributeId.Finishing] + a[AttributeId.Positioning] + a[AttributeId.Dribbling] + a[AttributeId.BallControl] + a[AttributeId.SprintSpeed]) / 5f;
                float defence = (a[AttributeId.Tackling] + a[AttributeId.Marking] + a[AttributeId.DefensiveAwareness] + a[AttributeId.Strength]) / 4f;
                float mid = (a[AttributeId.ShortPassing] + a[AttributeId.Vision] + a[AttributeId.BallControl] + a[AttributeId.Stamina]) / 4f;
                float wa = line == PositionLine.Attack ? 1f : line == PositionLine.Midfield ? 0.6f : 0.15f;
                float wd = line == PositionLine.Defence ? 1f : line == PositionLine.Midfield ? 0.6f : 0.15f;
                float fit = 0.85f + 0.15f * p.Condition.Fitness / 100f;
                attSum += attack * wa * fit; attW += wa;
                defSum += defence * wd * fit; defW += wd;
                midSum += mid * fit; midW += 1f;
            }
            return new Strength
            {
                Attack = attW > 0 ? attSum / attW : 50,
                Defence = defW > 0 ? defSum / defW : 50,
                Midfield = midW > 0 ? midSum / midW : 50,
                Goalkeeper = gk,
            };
        }

        public static MatchReport Simulate(MatchTeamSetup home, MatchTeamSetup away, DeterministicRandom rng, float homeAdvantage = 0.12f)
        {
            var sh = Evaluate(home);
            var sa = Evaluate(away);
            double lh = 1.3 * Math.Exp(0.06 * (sh.Attack + 0.3 * sh.Midfield - 0.75 * sa.Defence - 0.25 * sa.Goalkeeper - 0.3 * sa.Midfield)) * (1 + homeAdvantage);
            double la = 1.3 * Math.Exp(0.06 * (sa.Attack + 0.3 * sa.Midfield - 0.75 * sh.Defence - 0.25 * sh.Goalkeeper - 0.3 * sh.Midfield));
            lh = Math.Max(0.15, Math.Min(4.5, lh));
            la = Math.Max(0.15, Math.Min(4.5, la));
            int hg = Poisson(lh, rng), ag = Poisson(la, rng);

            var report = new MatchReport
            {
                HomeClubId = home.ClubId,
                AwayClubId = away.ClubId,
                HomeGoals = hg,
                AwayGoals = ag,
                HomePossession = Clamp(0.5f + (sh.Midfield - sa.Midfield) / 60f + (float)rng.Gaussian(0, 0.04), 0.3f, 0.7f),
                Seed = rng.NextUInt(),
                EngineVersion = "quick-" + MatchEngine.EngineVersion,
                Finished = true,
            };

            var homeStats = BuildTeamStats(home, TeamSide.Home, rng);
            var awayStats = BuildTeamStats(away, TeamSide.Away, rng);
            AssignGoals(report, home, homeStats, hg, TeamSide.Home, rng);
            AssignGoals(report, away, awayStats, ag, TeamSide.Away, rng);
            report.HomeShots = hg * 3 + Poisson(4 + lh * 2, rng);
            report.AwayShots = ag * 3 + Poisson(4 + la * 2, rng);
            report.HomeShotsOnTarget = hg + Poisson(1.5 + lh, rng);
            report.AwayShotsOnTarget = ag + Poisson(1.5 + la, rng);
            report.HomeCorners = Poisson(4.5, rng);
            report.AwayCorners = Poisson(4.0, rng);
            report.HomeFouls = Poisson(11, rng);
            report.AwayFouls = Poisson(12, rng);
            Discipline(report, homeStats, TeamSide.Home, rng);
            Discipline(report, awayStats, TeamSide.Away, rng);
            Injuries(report, homeStats, TeamSide.Home, rng);
            Injuries(report, awayStats, TeamSide.Away, rng);
            DistributeSecondaryStats(homeStats, rng);
            DistributeSecondaryStats(awayStats, rng);

            foreach (var s in homeStats) { s.GoalsConceded = IsGkOrDef(s) ? ag : 0; report.PlayerStats.Add(s); }
            foreach (var s in awayStats) { s.GoalsConceded = IsGkOrDef(s) ? hg : 0; report.PlayerStats.Add(s); }
            foreach (var s in report.PlayerStats)
            {
                int tg = s.Side == TeamSide.Home ? hg : ag;
                int og = s.Side == TeamSide.Home ? ag : hg;
                if (Positions.LineOf(s.Position) == PositionLine.Goalkeeper) s.Saves = Math.Max(0, (s.Side == TeamSide.Home ? report.AwayShotsOnTarget : report.HomeShotsOnTarget) - og);
                var r = RatingCalculator.Compute(s, tg, og);
                if (r.HasValue) s.Rating = (float)Math.Round(Clamp(r.Value + (float)rng.Gaussian(0, 0.35), 3f, 10f), 1);
            }
            report.Events.Sort((x, y) => x.Minute.CompareTo(y.Minute));
            return report;
        }

        private static bool IsGkOrDef(PlayerMatchStats s)
        {
            var l = Positions.LineOf(s.Position);
            return l == PositionLine.Goalkeeper || l == PositionLine.Defence;
        }

        private static List<PlayerMatchStats> BuildTeamStats(MatchTeamSetup team, TeamSide side, DeterministicRandom rng)
        {
            var list = new List<PlayerMatchStats>();
            var formation = Formation.Get(team.Tactics.Formation);
            for (int i = 0; i < team.Starters.Count; i++)
            {
                var p = team.Starters[i];
                list.Add(new PlayerMatchStats { PlayerId = p.Id, ClubId = team.ClubId, Side = side, Position = formation.Slots[i].Position, Started = true, MinutesPlayed = 90 });
            }
            // Tres cambios entre el 55' y el 82' (jugadores de campo).
            var outfieldBench = team.Bench.Where(b => b.Position != Position.GK).ToList();
            int subs = Math.Min(3, outfieldBench.Count);
            var replaced = new HashSet<int>();
            for (int k = 0; k < subs; k++)
            {
                var candidates = list.Where(s => s.Started && s.Position != Position.GK && !replaced.Contains(s.PlayerId)).ToList();
                if (candidates.Count == 0) break;
                var outS = candidates[rng.Range(0, candidates.Count)];
                int minute = rng.Range(55, 83);
                replaced.Add(outS.PlayerId);
                outS.MinutesPlayed = minute;
                var inP = outfieldBench[k];
                list.Add(new PlayerMatchStats { PlayerId = inP.Id, ClubId = team.ClubId, Side = side, Position = outS.Position, Started = false, MinutesPlayed = 90 - minute });
            }
            return list;
        }

        private static void AssignGoals(MatchReport report, MatchTeamSetup team, List<PlayerMatchStats> stats, int goals, TeamSide side, DeterministicRandom rng)
        {
            var byId = team.Starters.Concat(team.Bench).ToDictionary(p => p.Id);
            for (int g = 0; g < goals; g++)
            {
                int minute = rng.Range(1, 91);
                var onPitch = stats.Where(s => OnPitchAt(s, minute)).ToList();
                var scorer = Weighted(onPitch, s => ScorerWeight(s, byId[s.PlayerId]), rng);
                if (scorer == null) continue;
                scorer.Goals++;
                scorer.Shots++;
                scorer.ShotsOnTarget++;
                int assistId = 0;
                if (rng.Chance(0.72))
                {
                    var assister = Weighted(onPitch.Where(s => s != scorer).ToList(), s => AssistWeight(s, byId[s.PlayerId]), rng);
                    if (assister != null) { assister.Assists++; assister.KeyPasses++; assistId = assister.PlayerId; }
                }
                report.Events.Add(new MatchEvent(minute, MatchEventType.Goal, side, scorer.PlayerId, assistId, new Vec2(0, 0)) { Half = minute > 45 ? 2 : 1 });
            }
        }

        private static bool OnPitchAt(PlayerMatchStats s, int minute) =>
            s.Started ? minute <= s.MinutesPlayed : minute > 90 - s.MinutesPlayed;

        private static float ScorerWeight(PlayerMatchStats s, Player p)
        {
            float lineW = Positions.LineOf(s.Position) switch
            {
                PositionLine.Attack => 1f,
                PositionLine.Midfield => 0.4f,
                PositionLine.Defence => 0.1f,
                _ => 0f,
            };
            return lineW * (0.4f + p.Attributes.N(AttributeId.Finishing)) * (s.MinutesPlayed / 90f);
        }

        private static float AssistWeight(PlayerMatchStats s, Player p)
        {
            float lineW = Positions.LineOf(s.Position) switch
            {
                PositionLine.Midfield => 1f,
                PositionLine.Attack => 0.8f,
                PositionLine.Defence => 0.35f,
                _ => 0.02f,
            };
            return lineW * (0.4f + (p.Attributes.N(AttributeId.ShortPassing) + p.Attributes.N(AttributeId.Vision)) / 2f) * (s.MinutesPlayed / 90f);
        }

        private static void Discipline(MatchReport report, List<PlayerMatchStats> stats, TeamSide side, DeterministicRandom rng)
        {
            int yellows = Poisson(1.7, rng);
            for (int i = 0; i < yellows; i++)
            {
                var s = stats[rng.Range(0, stats.Count)];
                if (s.RedCard) continue;
                s.YellowCards++;
                s.FoulsCommitted++;
                int minute = rng.Range(5, 91);
                if (s.YellowCards >= 2)
                {
                    s.RedCard = true;
                    report.Events.Add(new MatchEvent(minute, MatchEventType.SecondYellow, side, s.PlayerId, 0, Vec2.Zero) { Half = minute > 45 ? 2 : 1 });
                }
                else report.Events.Add(new MatchEvent(minute, MatchEventType.YellowCard, side, s.PlayerId, 0, Vec2.Zero) { Half = minute > 45 ? 2 : 1 });
            }
            if (rng.Chance(0.05))
            {
                var s = stats[rng.Range(0, stats.Count)];
                if (!s.RedCard)
                {
                    s.RedCard = true;
                    int minute = rng.Range(10, 91);
                    report.Events.Add(new MatchEvent(minute, MatchEventType.RedCard, side, s.PlayerId, 0, Vec2.Zero) { Half = minute > 45 ? 2 : 1 });
                }
            }
        }

        private static void Injuries(MatchReport report, List<PlayerMatchStats> stats, TeamSide side, DeterministicRandom rng)
        {
            if (!rng.Chance(0.09)) return;
            var s = stats[rng.Range(0, stats.Count)];
            s.Injured = true;
            int minute = rng.Range(1, 91);
            report.Events.Add(new MatchEvent(minute, MatchEventType.Injury, side, s.PlayerId, 0, Vec2.Zero) { Half = minute > 45 ? 2 : 1 });
        }

        private static void DistributeSecondaryStats(List<PlayerMatchStats> stats, DeterministicRandom rng)
        {
            foreach (var s in stats)
            {
                float m = s.MinutesPlayed / 90f;
                var line = Positions.LineOf(s.Position);
                s.PassesAttempted = (int)Math.Round((line == PositionLine.Midfield ? 45 : line == PositionLine.Defence ? 38 : line == PositionLine.Attack ? 22 : 20) * m * rng.Range(0.75, 1.25));
                s.PassesCompleted = (int)Math.Round(s.PassesAttempted * rng.Range(0.68, 0.9));
                if (line == PositionLine.Defence || line == PositionLine.Midfield)
                {
                    s.TacklesWon = Poisson((line == PositionLine.Defence ? 2.2 : 1.6) * m, rng);
                    s.Interceptions = Poisson((line == PositionLine.Defence ? 1.8 : 1.1) * m, rng);
                }
                if (line == PositionLine.Attack || line == PositionLine.Midfield)
                {
                    int extraShots = Poisson((line == PositionLine.Attack ? 1.6 : 0.7) * m, rng);
                    s.Shots += extraShots;
                    s.ShotsOnTarget += rng.Range(0, extraShots + 1) / 2;
                    s.DribblesCompleted = Poisson((line == PositionLine.Attack ? 1.4 : 0.8) * m, rng);
                    s.KeyPasses += Poisson(0.6 * m, rng);
                }
            }
        }

        private static T? Weighted<T>(IReadOnlyList<T> items, Func<T, float> weight, DeterministicRandom rng) where T : class
        {
            float total = 0f;
            foreach (var i in items) total += Math.Max(0f, weight(i));
            if (total <= 0f) return items.Count > 0 ? items[rng.Range(0, items.Count)] : null;
            double r = rng.NextDouble() * total;
            foreach (var i in items)
            {
                r -= Math.Max(0f, weight(i));
                if (r <= 0) return i;
            }
            return items[items.Count - 1];
        }

        public static int Poisson(double lambda, DeterministicRandom rng)
        {
            double l = Math.Exp(-lambda), p = 1.0;
            int k = 0;
            do { k++; p *= rng.NextDouble(); } while (p > l && k < 30);
            return k - 1;
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
