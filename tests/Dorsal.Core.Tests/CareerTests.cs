using Dorsal.Core.Career;
using Dorsal.Core.Domain;
using Dorsal.Core.Random;
using Dorsal.Core.World;
using Xunit;

namespace Dorsal.Core.Tests;

internal static class CareerHelpers
{
    public static PlayerCreationRequest Request(Position pos = Position.ST, string style = "") => new PlayerCreationRequest
    {
        FirstName = "Mario",
        LastName = "Ensayo",
        Nationality = "ESP",
        BirthDate = new DateTime(2009, 5, 10),
        HeightCm = pos == Position.GK ? 190 : 179,
        Position = pos,
        PlayStyleId = style,
        Personality = PersonalityArchetype.Professional,
    };

    public static CareerSession Started(ulong seed, Position pos = Position.ST)
    {
        var c = CareerSession.NewCareer(Request(pos), seed);
        c.AcceptOffer(c.PendingOffers.First().Id);
        return c;
    }

    /// <summary>Juega una jornada con simulación rápida y acepta ofertas con una política sencilla.</summary>
    public static void PlayRound(CareerSession c)
    {
        if (c.Phase == CareerPhase.FreeAgent && c.PendingOffers.Any())
        {
            c.AcceptOffer(c.PendingOffers.First().Id);
            return;
        }
        c.AdvanceToNextMatchDay();
        c.SimulateUserMatch(quick: true);
        if (c.Phase != CareerPhase.InSeason) return;
        foreach (var o in c.PendingOffers.ToList())
        {
            if (o.Status != OfferStatus.Pending) continue;
            if (o.Kind == OfferKind.Renewal || o.Kind == OfferKind.Loan ||
                (o.Kind == OfferKind.Transfer && c.World.Clubs[o.ClubId].Level > c.UserClub!.Level))
                c.AcceptOffer(o.Id);
        }
    }
}

public class PlayerCreationTests
{
    private static readonly DateTime Start = new DateTime(2026, 8, 1);

    [Fact]
    public void ValidRequestCreatesUserPlayer()
    {
        var p = PlayerCreator.Create(CareerHelpers.Request(Position.ST, "dc-rematador"), Start, new DeterministicRandom(1));
        Assert.True(p.IsUserPlayer);
        Assert.Equal("Mario", p.FirstName);
        Assert.Equal(Position.ST, p.Position);
        Assert.Equal("dc-rematador", p.PlayStyle);
        Assert.InRange(p.Overall, PlayerCreator.StartingOverallMin - 1, PlayerCreator.StartingOverallMax + 1);
        Assert.True(p.Potential > p.Overall + 15);
        Assert.Equal(17, p.AgeOn(Start));
        // El estilo "rematador" deja la definición por encima del regate.
        Assert.True(p.Attributes[AttributeId.Finishing] > p.Attributes[AttributeId.Dribbling]);
    }

    [Theory]
    [InlineData("A", "Ensayo", 2009, Position.ST, 179, "")]          // nombre corto
    [InlineData("Mario", "Ens4yo", 2009, Position.ST, 179, "")]      // carácter no válido
    [InlineData("Mario", "Ensayo", 2000, Position.ST, 179, "")]      // demasiado mayor
    [InlineData("Mario", "Ensayo", 2009, Position.ST, 230, "")]      // altura
    [InlineData("Mario", "Ensayo", 2009, Position.CB, 185, "dc-movil")] // estilo de otra posición
    public void InvalidRequestsAreRejected(string first, string last, int year, Position pos, int height, string style)
    {
        var r = new PlayerCreationRequest { FirstName = first, LastName = last, BirthDate = new DateTime(year, 1, 1), Position = pos, HeightCm = height, PlayStyleId = style, Nationality = "ESP" };
        Assert.NotEmpty(PlayerCreator.Validate(r, Start));
        Assert.Throws<ArgumentException>(() => PlayerCreator.Create(r, Start, new DeterministicRandom(1)));
    }

    [Fact]
    public void GoalkeeperCannotHaveOutfieldSecondaryPositions()
    {
        var r = CareerHelpers.Request(Position.GK);
        r.SecondaryPositions.Add(Position.CB);
        Assert.NotEmpty(PlayerCreator.Validate(r, Start));
        var r2 = CareerHelpers.Request(Position.ST);
        r2.SecondaryPositions.AddRange(new[] { Position.LW, Position.RW, Position.CAM });
        Assert.NotEmpty(PlayerCreator.Validate(r2, Start));
    }

    [Fact]
    public void PotentialRangeNarrowsWithAge()
    {
        var p = PlayerCreator.Create(CareerHelpers.Request(), Start, new DeterministicRandom(3));
        var young = PlayerCreator.VisiblePotentialRange(p, Start);
        var older = PlayerCreator.VisiblePotentialRange(p, Start.AddYears(10));
        Assert.True(young.max - young.min > older.max - older.min);
        Assert.InRange(p.Potential, young.min, young.max);
    }
}

public class CareerFlowTests
{
    [Fact]
    public void NewCareerStartsWithOffersAndTwoDivisions()
    {
        var c = CareerSession.NewCareer(CareerHelpers.Request(), 5);
        Assert.Equal(CareerPhase.ChoosingClub, c.Phase);
        Assert.Null(c.User.ClubId);
        Assert.Equal(3, c.PendingOffers.Count());
        Assert.Equal(2, c.World.Competitions.Count);
        Assert.All(c.World.Competitions, comp => Assert.Equal(12, comp.ClubIds.Count));
        Assert.Empty(c.World.Validate());
        Assert.Throws<InvalidOperationException>(() => c.AdvanceToNextMatchDay());
    }

    [Fact]
    public void AcceptingInitialOfferSignsContractAndSetsObjectives()
    {
        var c = CareerSession.NewCareer(CareerHelpers.Request(), 6);
        var offer = c.PendingOffers.First();
        c.AcceptOffer(offer.Id);
        Assert.Equal(CareerPhase.InSeason, c.Phase);
        Assert.Equal(offer.ClubId, c.User.ClubId);
        Assert.Contains(c.User.Id, c.World.Clubs[offer.ClubId].SquadPlayerIds);
        Assert.NotNull(c.UserContract);
        Assert.Equal(offer.ClubId, c.UserContract!.ClubId);
        Assert.NotEmpty(c.State.Objectives);
        Assert.All(c.PendingOffers, o => Assert.NotEqual(OfferKind.InitialContract, o.Kind));
        Assert.Empty(c.World.Validate());
    }

    [Fact]
    public void TrialsDependOnLevelAndCannotBeRepeated()
    {
        int weakOk = 0, strongOk = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var c = CareerSession.NewCareer(CareerHelpers.Request(), seed);
            var weak = c.World.Clubs.Values.OrderBy(x => x.Level).First();
            var strong = c.World.Clubs.Values.OrderByDescending(x => x.Level).First();
            if (c.RequestTrial(weak.Id).Success) weakOk++;
            if (c.RequestTrial(strong.Id).Success) strongOk++;
            Assert.Throws<InvalidOperationException>(() => c.RequestTrial(weak.Id));
        }
        Assert.True(weakOk > strongOk + 5, $"pruebas superadas: débil {weakOk}, fuerte {strongOk}");
    }

    [Fact]
    public void MatchDayFlowProducesRoleObjectiveAndSummary()
    {
        var c = CareerHelpers.Started(7);
        var info = c.AdvanceToNextMatchDay();
        Assert.Equal(CareerPhase.MatchDay, c.Phase);
        Assert.NotNull(info.Fixture);
        Assert.NotEqual(MatchRole.NoClub, info.Role);
        var setups = c.UserMatchSetups();
        Assert.NotNull(setups);
        Assert.Equal(11, setups!.Value.home.Starters.Count);
        Assert.Throws<InvalidOperationException>(() => c.AdvanceToNextMatchDay());

        var summary = c.SimulateUserMatch(); // motor completo sin gráficos
        Assert.Equal(CareerPhase.InSeason, c.Phase);
        Assert.NotNull(summary.Report);
        Assert.True(info.Fixture!.IsPlayed);
        Assert.Equal(1, c.State.RoundsPlayed);
        // Toda la jornada queda jugada, no solo el partido del usuario.
        Assert.All(c.World.Fixtures.Where(f => f.Round == 1), f => Assert.True(f.IsPlayed));
        if (info.Role == MatchRole.Starter) Assert.NotNull(summary.UserStats);
    }

    [Fact]
    public void ReportForTheWrongFixtureIsRejected()
    {
        var c = CareerHelpers.Started(8);
        c.AdvanceToNextMatchDay();
        var setups = c.UserMatchSetups()!.Value;
        var wrong = Dorsal.Core.Career.QuickMatchSimulator.Simulate(setups.away, setups.home, new DeterministicRandom(1));
        Assert.Throws<ArgumentException>(() => c.CompleteMatchDay(wrong));
    }

    [Fact]
    public void FullSeasonIsConsistent()
    {
        var c = CareerHelpers.Started(9);
        int rounds = c.TotalRounds;
        Assert.Equal(22, rounds);
        for (int r = 0; r < rounds; r++)
        {
            CareerHelpers.PlayRound(c);
            Assert.Empty(c.World.Validate());
        }
        Assert.Equal(CareerPhase.SeasonEnded, c.Phase);
        Assert.All(c.World.Fixtures, f => Assert.True(f.IsPlayed));
        var summary = c.LastSeasonSummary!;
        foreach (var comp in c.World.Competitions)
        {
            var table = summary.Tables[comp.Id];
            Assert.All(table, row => Assert.Equal(22, row.Played));
            Assert.Equal(table.Sum(r => r.GoalsFor), table.Sum(r => r.GoalsAgainst));
            Assert.Equal(table.Sum(r => r.Won), table.Sum(r => r.Lost));
        }
        Assert.Equal(2, summary.Promoted.Count);
        Assert.Equal(2, summary.Relegated.Count);
        Assert.NotEmpty(summary.Awards);
        Assert.Single(c.State.History);
        Assert.True(c.State.Objectives.All(o => o.Evaluated));
        Assert.InRange(c.State.History[0].Appearances, 0, 22);
    }

    [Fact]
    public void TenSeasonsKeepTheWorldConsistent()
    {
        var c = CareerHelpers.Started(10);
        int seasons = 0;
        var birthYear = c.User.BirthDate.Year;
        while (seasons < 10 && c.Phase != CareerPhase.Retired)
        {
            int guard = 0;
            while (c.Phase == CareerPhase.InSeason || c.Phase == CareerPhase.FreeAgent)
            {
                CareerHelpers.PlayRound(c);
                Assert.True(guard++ < 60, "la temporada no termina");
            }
            Assert.Equal(CareerPhase.SeasonEnded, c.Phase);
            seasons++;
            c.StartNextSeason();
            var errors = c.World.Validate();
            Assert.True(errors.Count == 0, string.Join(" | ", errors.Take(3)));
            Assert.All(c.World.Competitions, comp => Assert.Equal(12, comp.ClubIds.Count));
            Assert.All(c.World.Clubs.Values, club => Assert.InRange(club.SquadPlayerIds.Count, 20, TransferAI.MaxSquad + 1));
            Assert.All(c.World.Clubs.Values, club => Assert.True(c.World.SquadOf(club.Id).Count(p => p.Position == Position.GK) >= 2));
            Assert.All(c.World.Contracts.Values, ct => Assert.True(ct.EndDate > c.World.CurrentDate, "contrato caducado tras la pretemporada"));
        }
        Assert.Equal(10, c.State.History.Count);
        Assert.Equal(birthYear, c.User.BirthDate.Year);
        Assert.True(c.User.Overall > 60, $"el jugador apenas progresó: {c.User.Overall}");
        // El mundo no crece sin control (retirados podados).
        Assert.InRange(c.World.Players.Count, 24 * 20, 24 * 32);
    }

    [Fact]
    public void FreeAgentCanSignAndCareerCanEndInRetirement()
    {
        var c = CareerHelpers.Started(12);
        // Forzar el final del contrato y llegar a final de temporada.
        c.UserContract!.EndDate = WorldGenerator.SeasonEnd(c.World.SeasonYear);
        while (c.Phase == CareerPhase.InSeason) { c.AdvanceToNextMatchDay(); c.SimulateUserMatch(quick: true); }
        foreach (var o in c.PendingOffers.Where(o => o.Kind == OfferKind.Renewal).ToList()) c.RejectOffer(o.Id);
        if (c.Phase == CareerPhase.SeasonEnded) c.StartNextSeason();
        if (c.User.ClubId == null)
        {
            Assert.Equal(CareerPhase.FreeAgent, c.Phase);
            if (!c.PendingOffers.Any())
            {
                var weakest = c.World.Clubs.Values.OrderBy(x => x.Level).First();
                c.AddOffer(OfferKind.FreeAgent, weakest, 0, SquadRole.Rotation, 2, 30);
            }
            c.AcceptOffer(c.PendingOffers.First().Id);
            Assert.Equal(CareerPhase.InSeason, c.Phase);
        }
        Assert.NotNull(c.User.ClubId);
        Assert.Throws<InvalidOperationException>(() => c.Retire()); // demasiado joven
        c.User.BirthDate = c.User.BirthDate.AddYears(-20);
        c.Retire();
        Assert.Equal(CareerPhase.Retired, c.Phase);
        Assert.True(c.User.Retired);
        Assert.Null(c.User.ClubId);
        Assert.Empty(c.World.Validate());
    }
}

public class ProgressionTests
{
    private static Player Young(int ovr, int potential, int prof = 15)
    {
        var rng = new DeterministicRandom(4);
        return new Player
        {
            Position = Position.ST,
            BirthDate = new DateTime(2009, 1, 1),
            Attributes = PlayerFactory.GenerateAttributes(Position.ST, ovr, rng),
            Potential = potential,
            Personality = new Personality { Professionalism = prof },
        };
    }

    [Fact]
    public void YoungPlayersGrowButNeverPassTheirPotential()
    {
        var p = Young(55, 78);
        var rng = new DeterministicRandom(1);
        var date = new DateTime(2026, 8, 1);
        for (int w = 0; w < 400; w++) ProgressionSystem.ApplyWeek(p, date, 0.9f, 1.2f, TrainingFocus.Balanced, 0.3f, rng);
        Assert.True(p.Overall >= 72, $"media final {p.Overall}");
        Assert.True(p.Overall <= 78, $"superó el potencial: {p.Overall}");
    }

    [Fact]
    public void PlayingTimeAndTrainingSpeedUpGrowth()
    {
        var date = new DateTime(2026, 8, 1);
        var playing = Young(55, 85);
        var benched = Young(55, 85);
        var r1 = new DeterministicRandom(2);
        var r2 = new DeterministicRandom(2);
        for (int w = 0; w < 30; w++)
        {
            ProgressionSystem.ApplyWeek(playing, date, 1f, 1.3f, TrainingFocus.Balanced, 0f, r1);
            ProgressionSystem.ApplyWeek(benched, date, 0f, 0.8f, TrainingFocus.Balanced, 0f, r2);
        }
        Assert.True(playing.Overall > benched.Overall + 1, $"{playing.Overall} vs {benched.Overall}");
    }

    [Fact]
    public void VeteransDeclineStartingWithPhysicalAttributes()
    {
        var rng = new DeterministicRandom(5);
        var p = new Player
        {
            Position = Position.CM,
            BirthDate = new DateTime(1991, 1, 1),
            Attributes = PlayerFactory.GenerateAttributes(Position.CM, 75, rng),
            Potential = 75,
        };
        int pace0 = p.Attributes[AttributeId.SprintSpeed] + p.Attributes[AttributeId.Acceleration] + p.Attributes[AttributeId.Stamina];
        int ovr0 = p.Overall;
        for (int w = 0; w < 60; w++) ProgressionSystem.ApplyWeek(p, new DateTime(2026, 8, 1), 1f, 1f, TrainingFocus.Balanced, 0f, rng);
        int pace1 = p.Attributes[AttributeId.SprintSpeed] + p.Attributes[AttributeId.Acceleration] + p.Attributes[AttributeId.Stamina];
        Assert.True(pace1 < pace0 - 5, $"físico {pace0} → {pace1}");
        Assert.True(p.Overall <= ovr0);
    }

    [Fact]
    public void TrainingFocusShapesGrowth()
    {
        var date = new DateTime(2026, 8, 1);
        var shooter = Young(55, 90);
        var defender = Young(55, 90);
        var r1 = new DeterministicRandom(7);
        var r2 = new DeterministicRandom(7);
        int Shooting(Player x) => x.Attributes[AttributeId.Finishing] + x.Attributes[AttributeId.ShotPower] + x.Attributes[AttributeId.LongShots];
        int Defending(Player x) => x.Attributes[AttributeId.Tackling] + x.Attributes[AttributeId.Marking] + x.Attributes[AttributeId.DefensiveAwareness];
        int s0 = Shooting(shooter), d0 = Defending(defender);
        for (int w = 0; w < 60; w++)
        {
            ProgressionSystem.ApplyWeek(shooter, date, 1f, 1f, TrainingFocus.Shooting, 0f, r1);
            ProgressionSystem.ApplyWeek(defender, date, 1f, 1f, TrainingFocus.Defending, 0f, r2);
        }
        Assert.True(Defending(defender) - d0 > Defending(shooter) - Defending(Young(55, 90)) + 2);
        Assert.True(Shooting(shooter) - s0 > 0);
    }

    [Fact]
    public void HighIntensityTrainingTiresAndRisksMore()
    {
        var p = Young(55, 85);
        var rng = new DeterministicRandom(9);
        var low = Enumerable.Range(0, 200).Select(_ => TrainingSystem.Session(p, TrainingFocus.Balanced, TrainingIntensity.Low, rng)).ToList();
        var high = Enumerable.Range(0, 200).Select(_ => TrainingSystem.Session(p, TrainingFocus.Balanced, TrainingIntensity.High, rng)).ToList();
        Assert.True(high.Average(r => r.FitnessCost) > low.Average(r => r.FitnessCost) * 2);
        Assert.True(high.Count(r => r.Injured) >= low.Count(r => r.Injured));
        Assert.True(high.Average(r => r.Grade) > low.Average(r => r.Grade));
    }
}

public class CoachAndContractTests
{
    [Fact]
    public void CoachPicksBetterFitAndRespectsAvailability()
    {
        var c = CareerHelpers.Started(13);
        var club = c.UserClub!;
        var squad = c.World.SquadOf(club.Id).ToList();
        var ctx = new SelectionContext { Date = c.World.CurrentDate, UserPlayerId = c.User.Id, UserTrust = 50 };

        var gk = squad.Where(p => p.Position == Position.GK).OrderByDescending(p => p.Overall).First();
        var setup = CoachAI.Select(club, squad, ctx, null);
        Assert.Equal(gk.Id, setup.Starters[0].Id);

        gk.Injury = new Injury { DaysRemaining = 10, Description = "test" };
        var setup2 = CoachAI.Select(club, squad, ctx, null);
        Assert.NotEqual(gk.Id, setup2.Starters[0].Id);
        Assert.DoesNotContain(setup2.Bench, p => p.Id == gk.Id);
        Assert.Equal(MatchRole.Injured, CoachAI.RoleOf(gk, setup2));
    }

    [Fact]
    public void TrustCanTipABorderlineSelection()
    {
        var c = CareerHelpers.Started(14);
        var club = c.UserClub!;
        var squad = c.World.SquadOf(club.Id).ToList();
        // Igualar al usuario con el titular de su puesto.
        var rival = squad.Where(p => p.Position == c.User.Position && p.Id != c.User.Id).OrderByDescending(p => p.Overall).First();
        c.User.Attributes = rival.Attributes.Clone();
        c.User.Condition.Fitness = rival.Condition.Fitness = 100f;
        c.User.Condition.Form = rival.Condition.Form = 0f;
        float high = CoachAI.SelectionScore(c.User, c.User.Position, new SelectionContext { UserPlayerId = c.User.Id, UserTrust = 95 }, null);
        float low = CoachAI.SelectionScore(c.User, c.User.Position, new SelectionContext { UserPlayerId = c.User.Id, UserTrust = 5 }, null);
        float rivalScore = CoachAI.SelectionScore(rival, rival.Position, new SelectionContext { UserPlayerId = c.User.Id }, null);
        Assert.True(high > low + 10);
        Assert.True(high > rivalScore || low < rivalScore);
    }

    [Fact]
    public void GoodPerformancesRaiseTrust()
    {
        var good = new Dorsal.Core.Match.PlayerMatchStats { MinutesPlayed = 90, Rating = 8.2f };
        var bad = new Dorsal.Core.Match.PlayerMatchStats { MinutesPlayed = 90, Rating = 5.1f };
        Assert.True(CoachAI.TrustDeltaAfterMatch(MatchRole.Starter, good) > 3f);
        Assert.True(CoachAI.TrustDeltaAfterMatch(MatchRole.Starter, bad) < -3f);
        Assert.True(CoachAI.TrustDeltaAfterMatch(MatchRole.NotSelected, null) < 0f);
    }

    [Fact]
    public void NegotiationAcceptsCountersAndWithdraws()
    {
        var c = CareerHelpers.Started(15);
        var other = c.World.Clubs.Values.First(x => x.Id != c.UserClub!.Id && x.TransferBudget > 0);
        var offer = c.AddOffer(OfferKind.Transfer, other, 0, SquadRole.Rotation, 3, 14);
        long max = offer.MaxWage;

        var counter = c.Negotiate(offer.Id, max + 1);
        Assert.False(counter.Accepted);
        Assert.False(counter.Withdrawn);
        Assert.InRange(counter.CounterWage, offer.WageWeekly, max);

        var ok = c.Negotiate(offer.Id, max);
        Assert.True(ok.Accepted);
        Assert.Equal(max, offer.WageWeekly);

        var offer2 = c.AddOffer(OfferKind.Transfer, other, 0, SquadRole.Rotation, 3, 14);
        Assert.True(c.Negotiate(offer2.Id, offer2.MaxWage * 3).Withdrawn);
        Assert.Throws<InvalidOperationException>(() => c.AcceptOffer(offer2.Id));
    }

    [Fact]
    public void AcceptedTransferMovesPlayerAndMoney()
    {
        var c = CareerHelpers.Started(16);
        var from = c.UserClub!;
        var to = c.World.Clubs.Values.Where(x => x.Id != from.Id).OrderByDescending(x => x.TransferBudget).First();
        long fee = 50_000;
        long fromBudget = from.TransferBudget, toBudget = to.TransferBudget;
        var offer = c.AddOffer(OfferKind.Transfer, to, fee, SquadRole.Starter, 3, 14);
        c.AcceptOffer(offer.Id);
        Assert.Equal(to.Id, c.User.ClubId);
        Assert.Contains(c.User.Id, to.SquadPlayerIds);
        Assert.DoesNotContain(c.User.Id, from.SquadPlayerIds);
        Assert.Equal(fromBudget + fee, from.TransferBudget);
        Assert.Equal(toBudget - fee, to.TransferBudget);
        Assert.Equal(to.Id, c.UserContract!.ClubId);
        Assert.Equal(OfferStatus.Accepted, offer.Status);
        Assert.Contains(c.World.Transfers, t => t.PlayerId == c.User.Id && t.Type == TransferType.Transfer && t.Fee == fee);
        Assert.Empty(c.World.Validate());
    }

    [Fact]
    public void LoanReturnsPlayerToParentClubAtSeasonEnd()
    {
        var c = CareerHelpers.Started(17);
        var parent = c.UserClub!;
        c.UserContract!.EndDate = WorldGenerator.SeasonEnd(c.World.SeasonYear + 2);
        var loanClub = c.World.Clubs.Values.First(x => x.Id != parent.Id);
        var offer = c.AddOffer(OfferKind.Loan, loanClub, 0, SquadRole.Starter, 1, 14);
        c.AcceptOffer(offer.Id);
        Assert.Equal(loanClub.Id, c.User.ClubId);
        Assert.True(c.UserContract!.IsLoan);
        Assert.Equal(parent.Id, c.UserContract.ParentClubId);
        Assert.Empty(c.World.Validate());

        while (c.Phase == CareerPhase.InSeason) { c.AdvanceToNextMatchDay(); c.SimulateUserMatch(quick: true); }
        c.StartNextSeason();
        Assert.Equal(parent.Id, c.User.ClubId);
        Assert.False(c.UserContract!.IsLoan);
        Assert.Empty(c.World.Validate());
    }

    [Fact]
    public void MarketValueRewardsQualityYouthAndContract()
    {
        var rng = new DeterministicRandom(1);
        var date = new DateTime(2026, 8, 1);
        Player Make(int ovr, int birthYear) => new Player { Position = Position.ST, BirthDate = new DateTime(birthYear, 1, 1), Attributes = PlayerFactory.GenerateAttributes(Position.ST, ovr, rng), Potential = ovr + 5 };
        var good = Make(80, 2001);
        var poor = Make(60, 2001);
        var old = Make(80, 1991);
        var longC = new Contract { EndDate = date.AddYears(4) };
        var shortC = new Contract { EndDate = date.AddMonths(3) };
        Assert.True(Economy.MarketValue(good, date, longC) > Economy.MarketValue(poor, date, longC) * 3);
        Assert.True(Economy.MarketValue(good, date, longC) > Economy.MarketValue(old, date, longC));
        Assert.True(Economy.MarketValue(good, date, longC) > Economy.MarketValue(good, date, shortC));
    }
}
