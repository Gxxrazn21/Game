using Dorsal.Core.Domain;
using Dorsal.Core.Random;
using Dorsal.Core.World;
using Xunit;

namespace Dorsal.Core.Tests;

public class DeterministicRandomTests
{
    [Fact]
    public void SameSeedProducesSameSequence()
    {
        var a = new DeterministicRandom(123);
        var b = new DeterministicRandom(123);
        for (int i = 0; i < 1000; i++) Assert.Equal(a.NextUInt(), b.NextUInt());
    }

    [Fact]
    public void DifferentSeedsDiverge()
    {
        var a = new DeterministicRandom(1);
        var b = new DeterministicRandom(2);
        int equal = Enumerable.Range(0, 100).Count(_ => a.NextUInt() == b.NextUInt());
        Assert.True(equal < 3);
    }

    [Fact]
    public void StateCanBeSavedAndRestored()
    {
        var a = new DeterministicRandom(99);
        for (int i = 0; i < 37; i++) a.NextDouble();
        var state = a.GetState();
        var expected = Enumerable.Range(0, 50).Select(_ => a.NextUInt()).ToList();
        var b = DeterministicRandom.FromState(state);
        Assert.Equal(expected, Enumerable.Range(0, 50).Select(_ => b.NextUInt()).ToList());
    }

    [Fact]
    public void RangeStaysWithinBoundsAndCoversThem()
    {
        var r = new DeterministicRandom(5);
        var seen = new HashSet<int>();
        for (int i = 0; i < 10000; i++)
        {
            int v = r.Range(3, 9);
            Assert.InRange(v, 3, 8);
            seen.Add(v);
        }
        Assert.Equal(6, seen.Count);
        for (int i = 0; i < 10000; i++) Assert.InRange(r.NextDouble(), 0.0, 0.9999999);
    }

    [Fact]
    public void GaussianHasExpectedMoments()
    {
        var r = new DeterministicRandom(7);
        var xs = Enumerable.Range(0, 20000).Select(_ => r.NextGaussian()).ToList();
        double mean = xs.Average();
        double sd = Math.Sqrt(xs.Select(x => (x - mean) * (x - mean)).Average());
        Assert.InRange(mean, -0.03, 0.03);
        Assert.InRange(sd, 0.97, 1.03);
    }

    [Fact]
    public void ZeroStateIsRejected()
    {
        Assert.Throws<ArgumentException>(() => DeterministicRandom.FromState(new uint[] { 0, 0, 0, 0 }));
    }
}

public class AttributeAndRatingTests
{
    [Fact]
    public void AttributesAreClampedToValidRange()
    {
        var a = new Attributes();
        a[AttributeId.Finishing] = 150;
        a[AttributeId.Tackling] = -5;
        Assert.Equal(Attributes.Max, a[AttributeId.Finishing]);
        Assert.Equal(Attributes.Min, a[AttributeId.Tackling]);
    }

    [Theory]
    [InlineData(Position.ST, 55)]
    [InlineData(Position.CB, 70)]
    [InlineData(Position.GK, 80)]
    [InlineData(Position.CM, 64)]
    public void GeneratedAttributesHitTargetOverall(Position pos, int target)
    {
        var rng = new DeterministicRandom(11);
        for (int i = 0; i < 20; i++)
        {
            var a = PlayerFactory.GenerateAttributes(pos, target, rng);
            Assert.InRange(PositionRatings.Overall(a, pos), target - 1, target + 1);
        }
    }

    [Fact]
    public void OutfieldPlayersAreBadGoalkeepersAndViceVersa()
    {
        var rng = new DeterministicRandom(3);
        var st = PlayerFactory.GenerateAttributes(Position.ST, 75, rng);
        var gk = PlayerFactory.GenerateAttributes(Position.GK, 75, rng);
        Assert.True(PositionRatings.Overall(st, Position.GK) < 40);
        Assert.True(PositionRatings.Overall(gk, Position.ST) < 65);
    }

    [Fact]
    public void PlayingOutOfPositionLowersEffectiveOverall()
    {
        var rng = new DeterministicRandom(4);
        var p = new Player { Position = Position.CB, Attributes = PlayerFactory.GenerateAttributes(Position.CB, 72, rng) };
        Assert.True(p.OverallInSlot(Position.CB) > p.OverallInSlot(Position.ST));
        Assert.True(p.OverallInSlot(Position.CB) > p.OverallInSlot(Position.CDM));
    }

    [Fact]
    public void AgeIsComputedFromBirthDate()
    {
        var p = new Player { BirthDate = new DateTime(2008, 6, 15) };
        Assert.Equal(18, p.AgeOn(new DateTime(2026, 6, 15)));
        Assert.Equal(17, p.AgeOn(new DateTime(2026, 6, 14)));
    }
}

public class WorldGeneratorTests
{
    [Fact]
    public void GenerationIsDeterministic()
    {
        var a = WorldGenerator.Generate(new WorldGenerationOptions { Seed = 77 });
        var b = WorldGenerator.Generate(new WorldGenerationOptions { Seed = 77 });
        Assert.Equal(a.Players.Values.Select(p => p.FullName), b.Players.Values.Select(p => p.FullName));
        Assert.Equal(a.Clubs.Values.Select(c => c.Name), b.Clubs.Values.Select(c => c.Name));
        Assert.Equal(a.Players.Values.Select(p => p.Overall), b.Players.Values.Select(p => p.Overall));
    }

    [Fact]
    public void WorldSatisfiesInvariants()
    {
        var w = WorldGenerator.Generate(new WorldGenerationOptions { Seed = 8, ClubCount = 14 });
        Assert.Empty(w.Validate());
        Assert.Equal(14, w.Clubs.Count);
        Assert.All(w.Clubs.Values, c => Assert.Equal(23, c.SquadPlayerIds.Count));
        Assert.All(w.Clubs.Values, c => Assert.True(w.SquadOf(c.Id).Count(p => p.Position == Position.GK) >= 2));
        // Dorsales únicos por club.
        Assert.All(w.Clubs.Values, c => Assert.Equal(23, w.SquadOf(c.Id).Select(p => p.ShirtNumber).Distinct().Count()));
        // Nombres completos únicos en todo el mundo.
        Assert.Equal(w.Players.Count, w.Players.Values.Select(p => p.FullName).Distinct().Count());
    }

    [Fact]
    public void ValidatorDetectsDuplicatedPlayers()
    {
        var w = TestWorlds.Small();
        var clubs = w.Clubs.Values.OrderBy(c => c.Id).ToList();
        int pid = clubs[0].SquadPlayerIds[0];
        clubs[1].SquadPlayerIds.Add(pid); // corrupción deliberada
        Assert.NotEmpty(w.Validate());
    }

    [Fact]
    public void TransferKeepsSquadsContractsAndBudgetsConsistent()
    {
        var w = TestWorlds.Small();
        var clubs = w.Clubs.Values.OrderBy(c => c.Id).ToList();
        int pid = clubs[0].SquadPlayerIds[0];
        long sellerBudget = clubs[0].TransferBudget, buyerBudget = clubs[1].TransferBudget;
        var contract = new Contract { PlayerId = pid, ClubId = clubs[1].Id, StartDate = w.CurrentDate, EndDate = w.CurrentDate.AddYears(2), WageWeekly = 1000 };
        w.TransferPlayer(pid, clubs[1].Id, 1000, contract, TransferType.Transfer);
        Assert.Empty(w.Validate());
        Assert.Equal(clubs[1].Id, w.Players[pid].ClubId);
        Assert.DoesNotContain(pid, clubs[0].SquadPlayerIds);
        Assert.Equal(sellerBudget + 1000, clubs[0].TransferBudget);
        Assert.Equal(buyerBudget - 1000, clubs[1].TransferBudget);
        Assert.Equal(clubs[1].Id, w.Contracts[pid].ClubId);
        Assert.Single(w.Transfers);
    }

    [Fact]
    public void ImpossibleTransfersAreRejectedWithoutSideEffects()
    {
        var w = TestWorlds.Small();
        var clubs = w.Clubs.Values.OrderBy(c => c.Id).ToList();
        int pid = clubs[0].SquadPlayerIds[0];
        var tooExpensive = new Contract { PlayerId = pid, ClubId = clubs[1].Id, StartDate = w.CurrentDate, EndDate = w.CurrentDate.AddYears(1) };
        Assert.Throws<InvalidOperationException>(() => w.TransferPlayer(pid, clubs[1].Id, clubs[1].TransferBudget + 1, tooExpensive, TransferType.Transfer));
        var wrongClub = new Contract { PlayerId = pid, ClubId = clubs[2].Id, StartDate = w.CurrentDate, EndDate = w.CurrentDate.AddYears(1) };
        Assert.Throws<InvalidOperationException>(() => w.TransferPlayer(pid, clubs[1].Id, 0, wrongClub, TransferType.Transfer));
        Assert.Equal(clubs[0].Id, w.Players[pid].ClubId);
        Assert.Empty(w.Validate());
        Assert.Empty(w.Transfers);
    }

    [Fact]
    public void ReservedNamesAreNeverGenerated()
    {
        var probe = new NameGenerator(new DeterministicRandom(1));
        var reserved = Enumerable.Range(0, 200).Select(_ => probe.PlayerName("ESP")).Select(n => n.first + " " + n.last).ToList();
        var gen = new NameGenerator(new DeterministicRandom(1), reserved);
        for (int i = 0; i < 300; i++)
        {
            var (f, l) = gen.PlayerName("ESP");
            Assert.DoesNotContain(f + " " + l, reserved);
        }
    }

    [Fact]
    public void YoungPlayersHaveMoreGrowthMarginThanVeterans()
    {
        var w = WorldGenerator.Generate(new WorldGenerationOptions { Seed = 3, ClubCount = 16 });
        var date = w.CurrentDate;
        var young = w.Players.Values.Where(p => p.AgeOn(date) <= 20).Select(p => p.Potential - p.Overall).DefaultIfEmpty(0).Average();
        var old = w.Players.Values.Where(p => p.AgeOn(date) >= 29).Select(p => p.Potential - p.Overall).DefaultIfEmpty(0).Average();
        Assert.True(young > old + 5, $"margen joven {young:F1} vs veterano {old:F1}");
        Assert.All(w.Players.Values, p => Assert.True(p.Potential >= p.Overall));
    }
}
