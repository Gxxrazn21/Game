using Dorsal.Core.Domain;
using Dorsal.Core.Match;
using Dorsal.Core.Simulation;
using Xunit;

namespace Dorsal.Core.Tests;

public class MatchEngineTests
{
    private static MatchEngine NewEngine(ulong seed = 1, MatchConfig? config = null)
    {
        var world = TestWorlds.Small(seed);
        var (home, away) = TestWorlds.Teams(world);
        return new MatchEngine(home, away, config ?? TestWorlds.FastConfig(), seed);
    }

    [Fact]
    public void FullMatchCompletesWithConsistentScoreAndClock()
    {
        var engine = NewEngine(3);
        var report = engine.RunToEnd();

        Assert.True(report.Finished);
        Assert.Equal(MatchPhase.FullTime, engine.Phase);
        Assert.True(engine.ClockMinute >= 90f);
        Assert.Contains(report.Events, e => e.Type == MatchEventType.HalfTime);
        Assert.Contains(report.Events, e => e.Type == MatchEventType.FullTime);

        int homeGoalEvents = report.Events.Count(e => (e.Type == MatchEventType.Goal || e.Type == MatchEventType.OwnGoal) && e.Side == TeamSide.Home);
        int awayGoalEvents = report.Events.Count(e => (e.Type == MatchEventType.Goal || e.Type == MatchEventType.OwnGoal) && e.Side == TeamSide.Away);
        Assert.Equal(report.HomeGoals, homeGoalEvents);
        Assert.Equal(report.AwayGoals, awayGoalEvents);
    }

    [Theory]
    [InlineData(50, 120f)]
    [InlineData(20, 270f)]
    [InlineData(10, 300f)]
    public void EngineWorksAtDifferentTickRatesAndDurations(int tickRate, float halfSeconds)
    {
        var engine = NewEngine(12, new MatchConfig { TickRate = tickRate, HalfDurationSeconds = halfSeconds });
        var r = engine.RunToEnd();
        Assert.True(r.Finished);
        Assert.True(engine.ClockMinute >= 90f);
        int expectedTicks = (int)(2 * halfSeconds * tickRate);
        Assert.InRange(engine.Tick, expectedTicks, (int)(expectedTicks * 1.25f));
        Assert.True(r.PlayerStats.Sum(s => s.PassesAttempted) > 30);
    }

    [Fact]
    public void SameSeedReproducesTheSameMatch()
    {
        var a = NewEngine(9).RunToEnd();
        var b = NewEngine(9).RunToEnd();
        Assert.Equal(a.HomeGoals, b.HomeGoals);
        Assert.Equal(a.AwayGoals, b.AwayGoals);
        Assert.Equal(a.Events.Select(e => e.ToString()), b.Events.Select(e => e.ToString()));
        Assert.Equal(a.PlayerStats.Select(s => s.Rating), b.PlayerStats.Select(s => s.Rating));
    }

    [Fact]
    public void DifferentSeedsProduceDifferentMatches()
    {
        var world = TestWorlds.Small(1);
        var (home, away) = TestWorlds.Teams(world);
        var signatures = Enumerable.Range(1, 4)
            .Select(s => new MatchEngine(home, away, TestWorlds.FastConfig(), (ulong)s).RunToEnd())
            .Select(r => string.Join(",", r.Events.Select(e => e.ToString())))
            .Distinct()
            .Count();
        Assert.True(signatures > 1);
    }

    [Fact]
    public void PlayersAndBallStayInsideThePlayableArea()
    {
        var engine = NewEngine(5);
        while (!engine.IsFinished)
        {
            engine.Step();
            foreach (var a in engine.Agents.Where(a => a.OnPitch))
            {
                Assert.InRange(a.Position.X, -Pitch.HalfLength - 3.1f, Pitch.HalfLength + 3.1f);
                Assert.InRange(a.Position.Y, -Pitch.HalfWidth - 3.1f, Pitch.HalfWidth + 3.1f);
                Assert.False(float.IsNaN(a.Position.X) || float.IsNaN(a.Position.Y));
                Assert.InRange(a.Energy, 0f, 1f);
            }
            Assert.False(float.IsNaN(engine.Ball.Position.X));
            Assert.InRange(engine.Ball.Position.X, -Pitch.HalfLength - 15f, Pitch.HalfLength + 15f);
            Assert.True(engine.Ball.Position.Z >= 0f);
            Assert.True(engine.Home.OnPitch.Count <= 11 && engine.Away.OnPitch.Count <= 11);
        }
    }

    [Fact]
    public void StatisticsAreInternallyConsistent()
    {
        for (ulong seed = 1; seed <= 4; seed++)
        {
            var r = NewEngine(seed).RunToEnd();
            foreach (var s in r.PlayerStats)
            {
                Assert.True(s.PassesCompleted <= s.PassesAttempted, $"pases completados > intentados ({s.PlayerId})");
                Assert.True(s.ShotsOnTarget <= s.Shots);
                Assert.InRange(s.MinutesPlayed, 0, 90);
                if (s.Rating.HasValue) Assert.InRange(s.Rating.Value, 1f, 10f);
            }
            int ownGoals = r.Events.Count(e => e.Type == MatchEventType.OwnGoal);
            Assert.Equal(r.HomeGoals + r.AwayGoals, r.PlayerStats.Sum(s => s.Goals) + ownGoals);
            Assert.True(r.PlayerStats.Sum(s => s.Assists) <= r.PlayerStats.Sum(s => s.Goals));
            Assert.Equal(r.HomeShots, r.PlayerStats.Where(s => s.Side == TeamSide.Home).Sum(s => s.Shots));
            Assert.InRange(r.HomePossession, 0.2f, 0.8f);
            // Los titulares que acaban el partido juegan 90 minutos.
            Assert.Contains(r.PlayerStats, s => s.Started && s.MinutesPlayed == 90);
        }
    }

    [Fact]
    public void EventsAreChronological()
    {
        var r = NewEngine(6).RunToEnd();
        var keyed = r.Events.Select(e => (e.Half, e.Minute)).ToList();
        for (int i = 1; i < keyed.Count; i++)
            Assert.True(keyed[i].CompareTo(keyed[i - 1]) >= 0, $"evento fuera de orden en {i}");
    }

    [Fact]
    public void SubstitutionsRespectLimitAndRules()
    {
        for (ulong seed = 1; seed <= 3; seed++)
        {
            var engine = NewEngine(seed, new MatchConfig { HalfDurationSeconds = 180f, MaxSubstitutions = 3 });
            var r = engine.RunToEnd();
            Assert.True(engine.Home.SubstitutionsUsed <= 3 && engine.Away.SubstitutionsUsed <= 3);
            var subs = r.Events.Where(e => e.Type == MatchEventType.Substitution).ToList();
            // Quien entra no había jugado y quien sale no vuelve a entrar.
            Assert.Equal(subs.Count, subs.Select(e => e.PlayerId).Distinct().Count());
            Assert.Empty(subs.Select(e => e.PlayerId).Intersect(subs.Select(e => e.OtherPlayerId)));
        }
    }

    [Fact]
    public void RequestSubstitutionValidatesInput()
    {
        var engine = NewEngine(2);
        var homeStarter = engine.Home.Agents.First(a => a.OnPitch && !a.IsGoalkeeper);
        var homeBench = engine.Home.Agents.First(a => !a.OnPitch);
        var awayBench = engine.Away.Agents.First(a => !a.OnPitch);
        Assert.False(engine.RequestSubstitution(homeStarter.PlayerId, awayBench.PlayerId)); // equipos distintos
        Assert.False(engine.RequestSubstitution(homeBench.PlayerId, homeStarter.PlayerId)); // al revés
        Assert.True(engine.RequestSubstitution(homeStarter.PlayerId, homeBench.PlayerId));
        Assert.False(engine.RequestSubstitution(homeStarter.PlayerId, homeBench.PlayerId)); // duplicado

        // Se ejecuta en el siguiente balón parado.
        while (!engine.IsFinished && !homeBench.OnPitch) engine.Step();
        Assert.True(homeBench.OnPitch);
        Assert.False(homeStarter.OnPitch);
        Assert.False(engine.RequestSubstitution(homeBench.PlayerId, homeStarter.PlayerId)); // el sustituido no vuelve
    }

    [Fact]
    public void HumanControlledPlayerFollowsJoystick()
    {
        var engine = NewEngine(4);
        var me = engine.Home.Agents.First(a => a.OnPitch && a.Role == Position.CM);
        engine.ControlledPlayerId = me.PlayerId;
        // Esperar a que empiece el juego.
        while (engine.Phase != MatchPhase.Playing) engine.Step();
        var start = me.Position;
        var input = new PlayerInput { Move = new Vec2(0f, 1f), Sprint = true };
        for (int i = 0; i < 40 && engine.Phase == MatchPhase.Playing; i++) engine.Step(input);
        Assert.True(me.Position.Y - start.Y > 4f, $"desplazamiento lateral {me.Position.Y - start.Y:F1} m");
    }

    [Fact]
    public void HumanCanPassAndShoot()
    {
        var engine = NewEngine(8);
        // Dar el balón al delantero local controlado tras el saque.
        var me = engine.Home.Agents.First(a => a.OnPitch && a.Role == Position.ST);
        engine.ControlledPlayerId = me.PlayerId;
        int guard = 0;
        while (engine.Ball.Owner != me && guard++ < 20000 && !engine.IsFinished) engine.Step();
        Assert.Equal(me, engine.Ball.Owner);
        int shotsBefore = me.Stats.Shots;
        engine.Step(new PlayerInput { Action = InputAction.Shoot, Power = 1f });
        Assert.Equal(shotsBefore + 1, me.Stats.Shots);
        Assert.Null(engine.Ball.Owner);
    }

    [Fact]
    public void MatchWorksWithoutAnyHumanPlayerOnPitch()
    {
        // Vista desde el banquillo: el jugador controlado es suplente; el partido sigue por IA.
        var engine = NewEngine(10);
        var benchPlayer = engine.Home.Agents.First(a => !a.OnPitch);
        engine.ControlledPlayerId = benchPlayer.PlayerId;
        Assert.Null(engine.ControlledAgent);
        while (!engine.IsFinished) engine.Step(new PlayerInput { Move = new Vec2(1, 0) });
        Assert.True(engine.IsFinished);
    }

    [Fact]
    public void InvalidSetupsAreRejected()
    {
        var world = TestWorlds.Small(1);
        var (home, away) = TestWorlds.Teams(world);
        home.Starters.RemoveAt(10);
        Assert.Throws<ArgumentException>(() => new MatchEngine(home, away));
        var (h2, a2) = TestWorlds.Teams(world);
        h2.Bench.Add(h2.Starters[3]);
        Assert.Throws<ArgumentException>(() => new MatchEngine(h2, a2));
        Assert.Throws<ArgumentException>(() => new MatchEngine(a2, a2, new MatchConfig { HalfDurationSeconds = 5f }));
    }
}

public class AttributeImpactTests
{
    [Fact]
    public void StrongerTeamWinsClearlyMoreOften()
    {
        int nextId = 1;
        var strong = TestWorlds.UniformTeam(1, 78, ref nextId, 100);
        var weak = TestWorlds.UniformTeam(2, 58, ref nextId, 200);
        int strongPoints = 0, weakPoints = 0, strongGoals = 0, weakGoals = 0;
        for (int i = 0; i < 20; i++)
        {
            // Alternar campo para neutralizar la ventaja de local.
            bool strongHome = i % 2 == 0;
            var r = new MatchEngine(strongHome ? strong : weak, strongHome ? weak : strong, TestWorlds.FastConfig(), (ulong)(1000 + i)).RunToEnd();
            int sg = strongHome ? r.HomeGoals : r.AwayGoals;
            int wg = strongHome ? r.AwayGoals : r.HomeGoals;
            strongGoals += sg;
            weakGoals += wg;
            if (sg > wg) strongPoints += 3; else if (sg == wg) { strongPoints++; weakPoints++; } else weakPoints += 3;
        }
        Assert.True(strongPoints > weakPoints * 2, $"puntos fuerte {strongPoints} vs débil {weakPoints}");
        Assert.True(strongGoals > weakGoals * 1.5, $"goles fuerte {strongGoals} vs débil {weakGoals}");
    }

    [Fact]
    public void BetterFinishingConvertsMoreShots()
    {
        int nextId = 1;
        var opponent = TestWorlds.UniformTeam(3, 68, ref nextId, 300);
        var sharp = TestWorlds.UniformTeam(1, 68, ref nextId, 400, tweakAttackers: a =>
        {
            a[AttributeId.Finishing] = 95; a[AttributeId.Composure] = 95; a[AttributeId.LongShots] = 90;
        });
        var blunt = TestWorlds.UniformTeam(2, 68, ref nextId, 400, tweakAttackers: a =>
        {
            a[AttributeId.Finishing] = 25; a[AttributeId.Composure] = 25; a[AttributeId.LongShots] = 25;
        });

        (int shots, int onTarget) Totals(MatchTeamSetup team)
        {
            int shots = 0, on = 0;
            for (int i = 0; i < 14; i++)
            {
                var engine = new MatchEngine(team, opponent, TestWorlds.FastConfig(), (ulong)(500 + i));
                engine.RunToEnd();
                var attackers = engine.Home.Agents.Where(a => Positions.LineOf(a.Role) == PositionLine.Attack);
                shots += attackers.Sum(a => a.Stats.Shots);
                on += attackers.Sum(a => a.Stats.ShotsOnTarget);
            }
            return (shots, on);
        }

        var s = Totals(sharp);
        var b = Totals(blunt);
        double sharpAcc = (double)s.onTarget / Math.Max(1, s.shots);
        double bluntAcc = (double)b.onTarget / Math.Max(1, b.shots);
        Assert.True(s.shots >= 10 && b.shots >= 10, $"muestra insuficiente: {s.shots} / {b.shots}");
        Assert.True(sharpAcc > bluntAcc + 0.1, $"precisión buena {sharpAcc:P0} vs mala {bluntAcc:P0}");
    }

    [Fact]
    public void FasterPlayersCoverMoreGroundWhenSprinting()
    {
        int nextId = 1;
        var fast = TestWorlds.UniformTeam(1, 70, ref nextId, 600);
        var slow = TestWorlds.UniformTeam(2, 70, ref nextId, 600);
        foreach (var p in fast.Starters) { p.Attributes[AttributeId.SprintSpeed] = 95; p.Attributes[AttributeId.Acceleration] = 95; }
        foreach (var p in slow.Starters) { p.Attributes[AttributeId.SprintSpeed] = 35; p.Attributes[AttributeId.Acceleration] = 35; }

        var engine = new MatchEngine(fast, slow, TestWorlds.FastConfig(), 77);
        var f = engine.Home.Agents.First(a => a.OnPitch && a.Role == Position.CM);
        var s = engine.Away.Agents.First(a => a.OnPitch && a.Role == Position.CM);
        while (engine.Phase != MatchPhase.Playing) engine.Step();
        engine.ControlledPlayerId = f.PlayerId;
        var f0 = f.Position;
        for (int i = 0; i < 60; i++) engine.Step(new PlayerInput { Move = new Vec2(0, -1), Sprint = true });
        float fastDist = Vec2.Distance(f0, f.Position);

        var engine2 = new MatchEngine(fast, slow, TestWorlds.FastConfig(), 77);
        var s2 = engine2.Away.Agents.First(a => a.PlayerId == s.PlayerId);
        while (engine2.Phase != MatchPhase.Playing) engine2.Step();
        engine2.ControlledPlayerId = s2.PlayerId;
        var s0 = s2.Position;
        for (int i = 0; i < 60; i++) engine2.Step(new PlayerInput { Move = new Vec2(0, 1), Sprint = true });
        float slowDist = Vec2.Distance(s0, s2.Position);

        Assert.True(fastDist > slowDist * 1.15f, $"rápido {fastDist:F1} m vs lento {slowDist:F1} m");
    }
}

public class RatingTests
{
    [Fact]
    public void GoalsRaiseAndRedCardsLowerTheRating()
    {
        var baseStats = new PlayerMatchStats { Position = Position.ST, MinutesPlayed = 90, PassesAttempted = 20, PassesCompleted = 15 };
        float b = RatingCalculator.Compute(baseStats, 1, 1)!.Value;
        var scorer = new PlayerMatchStats { Position = Position.ST, MinutesPlayed = 90, PassesAttempted = 20, PassesCompleted = 15, Goals = 2, Shots = 3, ShotsOnTarget = 2 };
        var sentOff = new PlayerMatchStats { Position = Position.ST, MinutesPlayed = 90, PassesAttempted = 20, PassesCompleted = 15, RedCard = true };
        Assert.True(RatingCalculator.Compute(scorer, 1, 1)!.Value > b + 1f);
        Assert.True(RatingCalculator.Compute(sentOff, 1, 1)!.Value < b - 1f);
    }

    [Fact]
    public void FewMinutesGiveNoRatingAndValuesAreClamped()
    {
        Assert.Null(RatingCalculator.Compute(new PlayerMatchStats { MinutesPlayed = 5 }, 0, 0));
        var crazy = new PlayerMatchStats { Position = Position.ST, MinutesPlayed = 90, Goals = 12 };
        Assert.Equal(10f, RatingCalculator.Compute(crazy, 12, 0));
        var awful = new PlayerMatchStats { Position = Position.GK, MinutesPlayed = 90, GoalsConceded = 15, RedCard = true };
        Assert.Equal(3f, RatingCalculator.Compute(awful, 0, 15));
    }

    [Fact]
    public void GoalkeeperCleanSheetAndSavesAreRewarded()
    {
        var keeper = new PlayerMatchStats { Position = Position.GK, MinutesPlayed = 90, Saves = 5 };
        var beaten = new PlayerMatchStats { Position = Position.GK, MinutesPlayed = 90, Saves = 1, GoalsConceded = 3 };
        Assert.True(RatingCalculator.Compute(keeper, 0, 0) > RatingCalculator.Compute(beaten, 0, 3));
    }

    [Fact]
    public void ScorersRateAboveTheirTeamAverageInRealMatches()
    {
        int checkedScorers = 0, above = 0;
        for (ulong seed = 1; seed <= 8; seed++)
        {
            var world = TestWorlds.Small(seed);
            var (home, away) = TestWorlds.Teams(world, 0, 5);
            var r = new MatchEngine(home, away, TestWorlds.FastConfig(), seed).RunToEnd();
            foreach (var side in new[] { TeamSide.Home, TeamSide.Away })
            {
                var team = r.PlayerStats.Where(s => s.Side == side && s.Rating.HasValue).ToList();
                double avg = team.Average(s => s.Rating!.Value);
                foreach (var s in team.Where(s => s.Goals > 0))
                {
                    checkedScorers++;
                    if (s.Rating!.Value > avg) above++;
                }
            }
        }
        Assert.True(checkedScorers >= 4, "muestra insuficiente de goleadores");
        Assert.True(above >= checkedScorers * 0.8, $"{above}/{checkedScorers} goleadores por encima de la media");
    }
}

public class LineupSelectorTests
{
    [Fact]
    public void SelectsElevenUniquePlayersWithGoalkeeperFirst()
    {
        var world = TestWorlds.Small(2);
        var club = world.Clubs.Values.First();
        var setup = LineupSelector.Select(club, world.SquadOf(club.Id));
        Assert.Equal(11, setup.Starters.Count);
        Assert.Equal(Position.GK, setup.Starters[0].Position);
        Assert.Equal(7, setup.Bench.Count);
        Assert.Contains(setup.Bench, p => p.Position == Position.GK);
        var all = setup.Starters.Concat(setup.Bench).Select(p => p.Id).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
    }

    [Fact]
    public void UnavailablePlayersAreExcludedAndBestFitIsChosen()
    {
        var world = TestWorlds.Small(2);
        var club = world.Clubs.Values.First();
        var squad = world.SquadOf(club.Id).ToList();
        var bestGk = squad.Where(p => p.Position == Position.GK).OrderByDescending(p => p.Overall).First();
        var setup = LineupSelector.Select(club, squad, isAvailable: p => p.Id != bestGk.Id);
        Assert.DoesNotContain(setup.Starters.Concat(setup.Bench), p => p.Id == bestGk.Id);
        Assert.Equal(Position.GK, setup.Starters[0].Position);
    }
}
