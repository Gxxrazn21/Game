using Dorsal.Core.Domain;
using Dorsal.Core.Simulation;
using Newtonsoft.Json;
using Xunit;

namespace Dorsal.Core.Tests;

public class FixtureGeneratorTests
{
    private static Competition League(int clubs, int rounds = 2) => new Competition
    {
        Id = 1,
        ClubIds = Enumerable.Range(1, clubs).ToList(),
        Rules = new CompetitionRules { Rounds = rounds, DaysBetweenRounds = 7 },
    };

    [Theory]
    [InlineData(12)]
    [InlineData(20)]
    [InlineData(4)]
    public void DoubleRoundRobinHasCorrectStructure(int n)
    {
        var comp = League(n);
        int id = 1;
        var fx = FixtureGenerator.Generate(comp, new DateTime(2026, 8, 15), () => id++);

        Assert.Equal(n * (n - 1), fx.Count);
        Assert.Equal(2 * (n - 1), fx.Max(f => f.Round));

        // Cada club juega exactamente una vez por jornada.
        foreach (var round in fx.GroupBy(f => f.Round))
        {
            var clubs = round.SelectMany(f => new[] { f.HomeClubId, f.AwayClubId }).ToList();
            Assert.Equal(clubs.Count, clubs.Distinct().Count());
            Assert.Equal(n, clubs.Count);
        }

        // Cada pareja se enfrenta dos veces, una en cada campo.
        for (int a = 1; a <= n; a++)
            for (int b = a + 1; b <= n; b++)
            {
                Assert.Single(fx, f => f.HomeClubId == a && f.AwayClubId == b);
                Assert.Single(fx, f => f.HomeClubId == b && f.AwayClubId == a);
            }

        // Ids únicos y fechas según la jornada.
        Assert.Equal(fx.Count, fx.Select(f => f.Id).Distinct().Count());
        Assert.All(fx, f => Assert.Equal(new DateTime(2026, 8, 15).AddDays((f.Round - 1) * 7), f.Date));
    }

    [Fact]
    public void OddNumberOfClubsGetsByes()
    {
        var comp = League(5);
        int id = 1;
        var fx = FixtureGenerator.Generate(comp, new DateTime(2026, 8, 15), () => id++);
        Assert.Equal(5 * 4, fx.Count);
        foreach (var round in fx.GroupBy(f => f.Round)) Assert.Equal(2, round.Count());
    }

    [Fact]
    public void HomeGamesAreBalanced()
    {
        var comp = League(12);
        int id = 1;
        var fx = FixtureGenerator.Generate(comp, new DateTime(2026, 8, 15), () => id++);
        foreach (var club in comp.ClubIds) Assert.Equal(11, fx.Count(f => f.HomeClubId == club));
    }
}

public class LeagueTableTests
{
    private static Fixture Played(int home, int away, int hg, int ag) => new Fixture
    {
        CompetitionId = 1, HomeClubId = home, AwayClubId = away, Result = new FixtureResult { HomeGoals = hg, AwayGoals = ag },
    };

    [Fact]
    public void PointsAndOrderFollowRules()
    {
        var comp = new Competition { Id = 1, ClubIds = new List<int> { 1, 2, 3 }, Rules = new CompetitionRules() };
        var fx = new List<Fixture> { Played(1, 2, 2, 0), Played(2, 3, 1, 1), Played(3, 1, 0, 0) };
        var table = LeagueTable.Compute(comp, fx);
        Assert.Equal(new[] { 1, 3, 2 }, table.Select(r => r.ClubId));
        Assert.Equal(4, table[0].Points);
        Assert.Equal(2, table[1].Points);
        Assert.Equal(1, table[2].Points);
        Assert.Equal(2, table[0].GoalDifference);
    }

    [Fact]
    public void CustomPointsAreRespected()
    {
        var rules = new CompetitionRules { PointsForWin = 2, PointsForDraw = 1 };
        var comp = new Competition { Id = 1, ClubIds = new List<int> { 1, 2 }, Rules = rules };
        var table = LeagueTable.Compute(comp, new[] { Played(1, 2, 3, 1) });
        Assert.Equal(2, table[0].Points);
    }

    [Fact]
    public void HeadToHeadTiebreakerCanOverrideGoalDifference()
    {
        // 1 y 2 empatan a puntos; 2 tiene mejor diferencia de goles, pero 1 ganó el enfrentamiento directo.
        var fx = new List<Fixture> { Played(1, 2, 1, 0), Played(2, 3, 5, 0), Played(3, 1, 1, 0) };
        var byGoalDiff = new Competition { Id = 1, ClubIds = new List<int> { 1, 2, 3 }, Rules = new CompetitionRules() };
        var byH2H = new Competition
        {
            Id = 1, ClubIds = new List<int> { 1, 2, 3 },
            Rules = new CompetitionRules { Tiebreakers = new List<Tiebreaker> { Tiebreaker.HeadToHeadPoints, Tiebreaker.GoalDifference } },
        };
        var t1 = LeagueTable.Compute(byGoalDiff, fx).Select(r => r.ClubId).ToList();
        var t2 = LeagueTable.Compute(byH2H, fx).Select(r => r.ClubId).ToList();
        Assert.True(t1.IndexOf(2) < t1.IndexOf(1));
        Assert.True(t2.IndexOf(1) < t2.IndexOf(2));
    }
}

public class RulesLoaderTests
{
    [Fact]
    public void LoadsRulesFromJson()
    {
        const string json = @"{
            ""Id"": ""liga-test"",
            ""Rounds"": 2,
            ""PointsForWin"": 3,
            ""PointsForDraw"": 1,
            ""Tiebreakers"": [""HeadToHeadPoints"", ""GoalDifference""],
            ""PromotionSlots"": 2,
            ""RelegationSlots"": 3,
            ""MaxSubstitutions"": 5
        }";
        var r = RulesLoader.FromJson(json);
        Assert.Equal("liga-test", r.Id);
        Assert.Equal(3, r.RelegationSlots);
        // La lista se reemplaza, no se concatena con los valores por defecto.
        Assert.Equal(new[] { Tiebreaker.HeadToHeadPoints, Tiebreaker.GoalDifference }, r.Tiebreakers);
    }

    [Fact]
    public void RoundTripsThroughJson()
    {
        var r = new CompetitionRules { Id = "x", PromotionSlots = 1, RelegationSlots = 2, PointsForWin = 2 };
        var back = RulesLoader.FromJson(RulesLoader.ToJson(r));
        Assert.Equal(r.Id, back.Id);
        Assert.Equal(r.PointsForWin, back.PointsForWin);
        Assert.Equal(r.Tiebreakers, back.Tiebreakers);
    }

    [Fact]
    public void InvalidRulesAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => RulesLoader.FromJson(@"{ ""PointsForWin"": 1, ""PointsForDraw"": 1 }"));
        Assert.Throws<InvalidOperationException>(() => RulesLoader.FromJson(@"{ ""MaxSubstitutions"": 9, ""BenchSize"": 7 }"));
    }

    [Fact]
    public void UnknownFieldsAreRejected()
    {
        Assert.ThrowsAny<JsonException>(() => RulesLoader.FromJson(@"{ ""PuntosPorVictoria"": 3 }"));
    }
}
