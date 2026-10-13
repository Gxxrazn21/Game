using System;
using System.Collections.Generic;
using Dorsal.Core.Domain;

namespace Dorsal.Core.Simulation
{
    /// <summary>Calendario de liga todos contra todos (método del círculo), con N vueltas alternando campo.</summary>
    public static class FixtureGenerator
    {
        public static List<Fixture> Generate(Competition competition, DateTime firstRoundDate, Func<int> nextId)
        {
            var clubs = new List<int>(competition.ClubIds);
            if (clubs.Count < 2) throw new InvalidOperationException("Se necesitan al menos 2 clubes.");
            const int Bye = -1;
            if (clubs.Count % 2 == 1) clubs.Add(Bye);

            int n = clubs.Count;
            int roundsPerLeg = n - 1;
            var fixtures = new List<Fixture>();
            var rules = competition.Rules;

            for (int leg = 0; leg < rules.Rounds; leg++)
            {
                var rot = new List<int>(clubs);
                for (int r = 0; r < roundsPerLeg; r++)
                {
                    int roundNumber = leg * roundsPerLeg + r + 1;
                    var date = firstRoundDate.AddDays((roundNumber - 1) * rules.DaysBetweenRounds);
                    for (int i = 0; i < n / 2; i++)
                    {
                        int a = rot[i];
                        int b = rot[n - 1 - i];
                        if (a == Bye || b == Bye) continue;
                        // Alternar local/visitante por jornada para el equipo fijo y por vuelta para todos.
                        bool swap = (i == 0 && r % 2 == 1) ^ (leg % 2 == 1);
                        fixtures.Add(new Fixture
                        {
                            Id = nextId(),
                            CompetitionId = competition.Id,
                            Round = roundNumber,
                            Date = date,
                            HomeClubId = swap ? b : a,
                            AwayClubId = swap ? a : b,
                        });
                    }
                    // Rotación: el primero queda fijo.
                    int last = rot[n - 1];
                    rot.RemoveAt(n - 1);
                    rot.Insert(1, last);
                }
            }
            return fixtures;
        }
    }
}
