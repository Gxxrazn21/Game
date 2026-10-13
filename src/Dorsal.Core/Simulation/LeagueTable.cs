using System;
using System.Collections.Generic;
using System.Linq;
using Dorsal.Core.Domain;

namespace Dorsal.Core.Simulation
{
    public sealed class TableRow
    {
        public int ClubId { get; set; }
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int AwayGoalsFor { get; set; }
        public int Points { get; set; }
        public int GoalDifference => GoalsFor - GoalsAgainst;
    }

    /// <summary>Clasificación calculada a partir de los resultados aplicando las reglas de la competición.</summary>
    public static class LeagueTable
    {
        public static List<TableRow> Compute(Competition competition, IEnumerable<Fixture> fixtures)
        {
            var rules = competition.Rules;
            var played = fixtures.Where(f => f.CompetitionId == competition.Id && f.Result != null).ToList();
            var rows = competition.ClubIds.ToDictionary(id => id, id => new TableRow { ClubId = id });

            foreach (var f in played)
            {
                var r = f.Result!;
                Apply(rows[f.HomeClubId], r.HomeGoals, r.AwayGoals, false, rules);
                Apply(rows[f.AwayClubId], r.AwayGoals, r.HomeGoals, true, rules);
            }

            var list = rows.Values.ToList();
            list.Sort((a, b) => Compare(a, b, rules, played));
            return list;
        }

        private static void Apply(TableRow row, int gf, int ga, bool away, CompetitionRules rules)
        {
            row.Played++;
            row.GoalsFor += gf;
            row.GoalsAgainst += ga;
            if (away) row.AwayGoalsFor += gf;
            if (gf > ga) { row.Won++; row.Points += rules.PointsForWin; }
            else if (gf == ga) { row.Drawn++; row.Points += rules.PointsForDraw; }
            else { row.Lost++; row.Points += rules.PointsForLoss; }
        }

        private static int Compare(TableRow a, TableRow b, CompetitionRules rules, List<Fixture> played)
        {
            int c = b.Points.CompareTo(a.Points);
            if (c != 0) return c;
            foreach (var tb in rules.Tiebreakers)
            {
                switch (tb)
                {
                    case Tiebreaker.GoalDifference: c = b.GoalDifference.CompareTo(a.GoalDifference); break;
                    case Tiebreaker.GoalsFor: c = b.GoalsFor.CompareTo(a.GoalsFor); break;
                    case Tiebreaker.Wins: c = b.Won.CompareTo(a.Won); break;
                    case Tiebreaker.AwayGoalsFor: c = b.AwayGoalsFor.CompareTo(a.AwayGoalsFor); break;
                    case Tiebreaker.HeadToHeadPoints:
                    {
                        var (pa, pb, _, _) = HeadToHead(a.ClubId, b.ClubId, played, rules);
                        c = pb.CompareTo(pa);
                        break;
                    }
                    case Tiebreaker.HeadToHeadGoalDifference:
                    {
                        var (_, _, gda, gdb) = HeadToHead(a.ClubId, b.ClubId, played, rules);
                        c = gdb.CompareTo(gda);
                        break;
                    }
                }
                if (c != 0) return c;
            }
            // Orden estable y determinista como último recurso.
            return a.ClubId.CompareTo(b.ClubId);
        }

        private static (int pointsA, int pointsB, int gdA, int gdB) HeadToHead(int a, int b, List<Fixture> played, CompetitionRules rules)
        {
            int pa = 0, pb = 0, gda = 0, gdb = 0;
            foreach (var f in played)
            {
                int ga, gb;
                if (f.HomeClubId == a && f.AwayClubId == b) { ga = f.Result!.HomeGoals; gb = f.Result.AwayGoals; }
                else if (f.HomeClubId == b && f.AwayClubId == a) { ga = f.Result!.AwayGoals; gb = f.Result.HomeGoals; }
                else continue;
                gda += ga - gb;
                gdb += gb - ga;
                if (ga > gb) { pa += rules.PointsForWin; pb += rules.PointsForLoss; }
                else if (ga == gb) { pa += rules.PointsForDraw; pb += rules.PointsForDraw; }
                else { pb += rules.PointsForWin; pa += rules.PointsForLoss; }
            }
            return (pa, pb, gda, gdb);
        }
    }
}
