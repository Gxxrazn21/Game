using System;
using System.Collections.Generic;
using System.Linq;

namespace Dorsal.Core.Content
{
    /// <summary>
    /// Tipos de derechos que puede necesitar un paquete de contenido.
    /// Se distinguen a propósito: un proveedor de datos NO concede derechos de imagen ni marcas.
    /// </summary>
    public enum RightType
    {
        /// <summary>Contenido creado por el estudio (ficticio). No requiere terceros.</summary>
        Original,
        /// <summary>Derecho a incorporar datos estadísticos de un proveedor dentro del juego.</summary>
        SportsData,
        /// <summary>Nombres reales de futbolistas (datos personales y derechos de nombre).</summary>
        PlayerNames,
        /// <summary>Imagen y semejanza de futbolistas (caras, retratos 3D).</summary>
        PlayerLikeness,
        /// <summary>Fotografías.</summary>
        Photos,
        /// <summary>Nombres, escudos y marcas de clubes.</summary>
        ClubMarks,
        /// <summary>Nombres y logotipos de competiciones o federaciones.</summary>
        CompetitionMarks,
        /// <summary>Diseños de equipaciones.</summary>
        KitDesigns,
        /// <summary>Marcas de patrocinadores y fabricantes.</summary>
        SponsorMarks,
        /// <summary>Nombres y diseños identificables de estadios.</summary>
        StadiumMarks,
    }

    /// <summary>Qué tipo de contenido aporta un paquete y, por tanto, qué derechos exige.</summary>
    public enum ContentType
    {
        FictionalWorld,
        RealCompetitionData,
        RealPlayerNames,
        RealPlayerFaces,
        RealPlayerPhotos,
        RealClubIdentity,
        RealCompetitionIdentity,
        RealKits,
        SponsorLogos,
        RealStadiums,
    }

    public enum PackKind
    {
        Original,
        Licensed,
    }

    /// <summary>Concesión de derechos documentada en un acuerdo verificable.</summary>
    public sealed class RightsGrant
    {
        public RightType RightType { get; set; }
        public string Licensor { get; set; } = "";

        /// <summary>Referencia al contrato (no el contrato en sí).</summary>
        public string AgreementRef { get; set; } = "";

        /// <summary>Códigos ISO 3166-1 alfa-2 o "WORLD".</summary>
        public List<string> Territories { get; set; } = new List<string>();

        /// <summary>"android", "ios" o "*".</summary>
        public List<string> Platforms { get; set; } = new List<string>();

        public DateTime ValidFrom { get; set; }
        public DateTime ValidUntil { get; set; }

        /// <summary>Persona o rol que verificó el acuerdo (p. ej. asesoría legal). Vacío = no verificado.</summary>
        public string VerifiedBy { get; set; } = "";
        public DateTime? VerifiedOn { get; set; }

        public bool IsVerified => !string.IsNullOrWhiteSpace(VerifiedBy) && VerifiedOn.HasValue && !string.IsNullOrWhiteSpace(AgreementRef);

        public bool CoversTerritory(string territory) =>
            Territories.Any(t => string.Equals(t, "WORLD", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(t, territory, StringComparison.OrdinalIgnoreCase));

        public bool CoversPlatform(string platform) =>
            Platforms.Any(p => p == "*" || string.Equals(p, platform, StringComparison.OrdinalIgnoreCase));

        public bool IsValidOn(DateTime date) => date >= ValidFrom && date <= ValidUntil;
    }

    public sealed class ContentPackManifest
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Version { get; set; } = "1.0.0";
        public PackKind Kind { get; set; }
        public List<ContentType> ContentTypes { get; set; } = new List<ContentType>();
        public List<RightsGrant> RightsGrants { get; set; } = new List<RightsGrant>();

        /// <summary>Paquete al que recurrir si este se desactiva (normalmente el base ficticio).</summary>
        public string FallbackPackId { get; set; } = "";
    }

    /// <summary>Contexto en el que se decide si un paquete puede activarse.</summary>
    public sealed class LicenseContext
    {
        public LicenseContext(DateTime date, string territory, string platform)
        {
            Date = date;
            Territory = territory;
            Platform = platform;
        }

        public DateTime Date { get; }
        public string Territory { get; }
        public string Platform { get; }
    }

    public sealed class PackEvaluation
    {
        public PackEvaluation(ContentPackManifest pack, bool isActive, IReadOnlyList<string> reasons)
        {
            Pack = pack;
            IsActive = isActive;
            Reasons = reasons;
        }

        public ContentPackManifest Pack { get; }
        public bool IsActive { get; }

        /// <summary>Motivos de desactivación (vacío si está activo).</summary>
        public IReadOnlyList<string> Reasons { get; }
    }

    /// <summary>
    /// Decide qué paquetes de contenido pueden usarse. Regla central:
    /// un paquete licenciado solo se activa si CADA tipo de contenido está cubierto por concesiones
    /// verificadas, vigentes y que incluyan el territorio y la plataforma. Ante la duda, se desactiva.
    /// </summary>
    public sealed class ContentRegistry
    {
        private readonly List<ContentPackManifest> _packs = new List<ContentPackManifest>();

        /// <summary>Derechos que exige cada tipo de contenido.</summary>
        public static IReadOnlyList<RightType> RequiredRights(ContentType type)
        {
            switch (type)
            {
                case ContentType.FictionalWorld: return new[] { RightType.Original };
                case ContentType.RealCompetitionData: return new[] { RightType.SportsData };
                // Nombres reales: derecho de nombre y, en la práctica, datos del proveedor.
                case ContentType.RealPlayerNames: return new[] { RightType.PlayerNames };
                case ContentType.RealPlayerFaces: return new[] { RightType.PlayerLikeness };
                case ContentType.RealPlayerPhotos: return new[] { RightType.Photos, RightType.PlayerLikeness };
                case ContentType.RealClubIdentity: return new[] { RightType.ClubMarks };
                case ContentType.RealCompetitionIdentity: return new[] { RightType.CompetitionMarks };
                case ContentType.RealKits: return new[] { RightType.KitDesigns, RightType.ClubMarks };
                case ContentType.SponsorLogos: return new[] { RightType.SponsorMarks };
                case ContentType.RealStadiums: return new[] { RightType.StadiumMarks };
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        public IReadOnlyList<ContentPackManifest> Packs => _packs;

        public void Register(ContentPackManifest pack)
        {
            if (string.IsNullOrWhiteSpace(pack.Id)) throw new ArgumentException("El paquete necesita Id.");
            if (_packs.Any(p => p.Id == pack.Id)) throw new InvalidOperationException($"Paquete duplicado: {pack.Id}");
            _packs.Add(pack);
        }

        public PackEvaluation Evaluate(ContentPackManifest pack, LicenseContext ctx)
        {
            var reasons = new List<string>();
            if (pack.ContentTypes.Count == 0) reasons.Add("El paquete no declara tipos de contenido.");

            foreach (var type in pack.ContentTypes)
            {
                if (pack.Kind == PackKind.Original && type == ContentType.FictionalWorld) continue;
                if (pack.Kind == PackKind.Original)
                {
                    reasons.Add($"Un paquete Original no puede incluir contenido real ({type}).");
                    continue;
                }

                foreach (var right in RequiredRights(type))
                {
                    if (right == RightType.Original) continue;
                    var grants = pack.RightsGrants.Where(g => g.RightType == right).ToList();
                    if (grants.Count == 0)
                    {
                        reasons.Add($"{type}: falta concesión de {right}.");
                        continue;
                    }
                    bool ok = grants.Any(g => g.IsVerified && g.IsValidOn(ctx.Date) &&
                                              g.CoversTerritory(ctx.Territory) && g.CoversPlatform(ctx.Platform));
                    if (!ok) reasons.Add(DescribeFailure(type, right, grants, ctx));
                }
            }

            return new PackEvaluation(pack, reasons.Count == 0, reasons);
        }

        public IReadOnlyList<PackEvaluation> EvaluateAll(LicenseContext ctx) => _packs.Select(p => Evaluate(p, ctx)).ToList();

        public IReadOnlyList<ContentPackManifest> ActivePacks(LicenseContext ctx) =>
            EvaluateAll(ctx).Where(e => e.IsActive).Select(e => e.Pack).ToList();

        private static string DescribeFailure(ContentType type, RightType right, List<RightsGrant> grants, LicenseContext ctx)
        {
            if (!grants.Any(g => g.IsVerified)) return $"{type}: la concesión de {right} no está verificada.";
            if (!grants.Any(g => g.IsVerified && g.IsValidOn(ctx.Date))) return $"{type}: la concesión de {right} no está vigente el {ctx.Date:yyyy-MM-dd}.";
            if (!grants.Any(g => g.IsVerified && g.CoversTerritory(ctx.Territory))) return $"{type}: la concesión de {right} no cubre el territorio {ctx.Territory}.";
            return $"{type}: la concesión de {right} no cubre la plataforma {ctx.Platform}.";
        }

        /// <summary>Manifiesto del paquete base ficticio, siempre disponible.</summary>
        public static ContentPackManifest BaseFictionalPack() => new ContentPackManifest
        {
            Id = BasePackId,
            Name = "Mundo DORSAL (ficticio)",
            Version = "0.1.0",
            Kind = PackKind.Original,
            ContentTypes = new List<ContentType> { ContentType.FictionalWorld },
        };

        public const string BasePackId = "dorsal.base";
    }
}
