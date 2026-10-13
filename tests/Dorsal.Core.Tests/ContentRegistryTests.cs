using Dorsal.Core.Content;
using Dorsal.Core.World;
using Xunit;

namespace Dorsal.Core.Tests;

public class ContentRegistryTests
{
    private static readonly LicenseContext Spain2027 = new LicenseContext(new DateTime(2027, 3, 1), "ES", "android");

    private static RightsGrant Grant(RightType type, bool verified = true, string territory = "WORLD", string platform = "*",
                                     DateTime? from = null, DateTime? until = null) => new RightsGrant
    {
        RightType = type,
        Licensor = "Titular de ejemplo",
        AgreementRef = "CONTRATO-0001",
        Territories = new List<string> { territory },
        Platforms = new List<string> { platform },
        ValidFrom = from ?? new DateTime(2026, 1, 1),
        ValidUntil = until ?? new DateTime(2028, 12, 31),
        VerifiedBy = verified ? "Asesoría legal" : "",
        VerifiedOn = verified ? new DateTime(2026, 2, 1) : null,
    };

    private static ContentPackManifest LicensedLeague(params RightsGrant[] grants) => new ContentPackManifest
    {
        Id = "liga-licenciada-ejemplo",
        Kind = PackKind.Licensed,
        ContentTypes = new List<ContentType> { ContentType.RealClubIdentity, ContentType.RealKits, ContentType.RealPlayerNames },
        RightsGrants = grants.ToList(),
        FallbackPackId = ContentRegistry.BasePackId,
    };

    [Fact]
    public void BaseFictionalPackIsAlwaysActive()
    {
        var reg = new ContentRegistry();
        reg.Register(ContentRegistry.BaseFictionalPack());
        var eval = reg.EvaluateAll(Spain2027).Single();
        Assert.True(eval.IsActive);
        Assert.Empty(eval.Reasons);
    }

    [Fact]
    public void LicensedPackWithoutGrantsIsInactive()
    {
        var eval = new ContentRegistry().Evaluate(LicensedLeague(), Spain2027);
        Assert.False(eval.IsActive);
        Assert.Contains(eval.Reasons, r => r.Contains("ClubMarks"));
        Assert.Contains(eval.Reasons, r => r.Contains("KitDesigns"));
        Assert.Contains(eval.Reasons, r => r.Contains("PlayerNames"));
    }

    [Fact]
    public void SportsDataGrantDoesNotCoverImageOrMarks()
    {
        // Tener un proveedor de datos no concede escudos, camisetas ni nombres de jugadores.
        var eval = new ContentRegistry().Evaluate(LicensedLeague(Grant(RightType.SportsData)), Spain2027);
        Assert.False(eval.IsActive);
    }

    [Fact]
    public void FullyVerifiedGrantsActivatePack()
    {
        var pack = LicensedLeague(Grant(RightType.ClubMarks), Grant(RightType.KitDesigns), Grant(RightType.PlayerNames));
        Assert.True(new ContentRegistry().Evaluate(pack, Spain2027).IsActive);
    }

    [Fact]
    public void UnverifiedGrantDoesNotActivate()
    {
        var pack = LicensedLeague(Grant(RightType.ClubMarks), Grant(RightType.KitDesigns), Grant(RightType.PlayerNames, verified: false));
        var eval = new ContentRegistry().Evaluate(pack, Spain2027);
        Assert.False(eval.IsActive);
        Assert.Contains(eval.Reasons, r => r.Contains("no está verificada"));
    }

    [Fact]
    public void ExpiredGrantDoesNotActivate()
    {
        var pack = LicensedLeague(Grant(RightType.ClubMarks, until: new DateTime(2026, 12, 31)), Grant(RightType.KitDesigns), Grant(RightType.PlayerNames));
        var eval = new ContentRegistry().Evaluate(pack, Spain2027);
        Assert.False(eval.IsActive);
        Assert.Contains(eval.Reasons, r => r.Contains("vigente"));
    }

    [Fact]
    public void TerritoryAndPlatformAreEnforced()
    {
        var reg = new ContentRegistry();
        var ukOnly = LicensedLeague(Grant(RightType.ClubMarks, territory: "GB"), Grant(RightType.KitDesigns), Grant(RightType.PlayerNames));
        Assert.False(reg.Evaluate(ukOnly, Spain2027).IsActive);
        Assert.True(reg.Evaluate(ukOnly, new LicenseContext(new DateTime(2027, 3, 1), "GB", "android")).IsActive);

        var iosOnly = LicensedLeague(Grant(RightType.ClubMarks, platform: "ios"), Grant(RightType.KitDesigns), Grant(RightType.PlayerNames));
        Assert.False(reg.Evaluate(iosOnly, Spain2027).IsActive);
    }

    [Fact]
    public void OriginalPackCannotSmuggleRealContent()
    {
        var pack = new ContentPackManifest
        {
            Id = "falso-original",
            Kind = PackKind.Original,
            ContentTypes = new List<ContentType> { ContentType.FictionalWorld, ContentType.RealClubIdentity },
        };
        Assert.False(new ContentRegistry().Evaluate(pack, Spain2027).IsActive);
    }

    [Fact]
    public void DuplicatePackIdsAreRejected()
    {
        var reg = new ContentRegistry();
        reg.Register(ContentRegistry.BaseFictionalPack());
        Assert.Throws<InvalidOperationException>(() => reg.Register(ContentRegistry.BaseFictionalPack()));
    }

    [Fact]
    public void GameIsPlayableWithZeroLicensedPacks()
    {
        // Configuración A: el mundo completo sale del paquete base ficticio, sin datos externos.
        var reg = new ContentRegistry();
        reg.Register(ContentRegistry.BaseFictionalPack());
        reg.Register(LicensedLeague()); // presente pero sin derechos → desactivado
        var active = reg.ActivePacks(Spain2027).Select(p => p.Id).ToList();
        Assert.Equal(new[] { ContentRegistry.BasePackId }, active);

        var world = WorldGenerator.Generate(new WorldGenerationOptions { Seed = 1 });
        Assert.All(world.Players.Values, p => Assert.Contains(p.SourcePackId, active));
        Assert.All(world.Clubs.Values, c => Assert.Contains(c.SourcePackId, active));
        Assert.All(world.Competitions, c => Assert.Contains(c.SourcePackId, active));

        var (home, away) = TestWorlds.Teams(world);
        var report = new Dorsal.Core.Match.MatchEngine(home, away, TestWorlds.FastConfig(), 1).RunToEnd();
        Assert.True(report.Finished);
    }
}
