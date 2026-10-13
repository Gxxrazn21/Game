using Dorsal.Core.Career;
using Dorsal.Core.Domain;

namespace Dorsal.Core.Localization
{
    /// <summary>Textos en español para la interfaz.</summary>
    public static class Es
    {
        public static string Position(Position p) => p switch
        {
            Domain.Position.GK => "POR",
            Domain.Position.CB => "DFC",
            Domain.Position.LB => "LI",
            Domain.Position.RB => "LD",
            Domain.Position.CDM => "MCD",
            Domain.Position.CM => "MC",
            Domain.Position.CAM => "MCO",
            Domain.Position.LM => "MI",
            Domain.Position.RM => "MD",
            Domain.Position.LW => "EI",
            Domain.Position.RW => "ED",
            _ => "DC",
        };

        public static string PositionLong(Position p) => p switch
        {
            Domain.Position.GK => "Portero",
            Domain.Position.CB => "Defensa central",
            Domain.Position.LB => "Lateral izquierdo",
            Domain.Position.RB => "Lateral derecho",
            Domain.Position.CDM => "Mediocentro defensivo",
            Domain.Position.CM => "Mediocentro",
            Domain.Position.CAM => "Mediapunta",
            Domain.Position.LM => "Interior izquierdo",
            Domain.Position.RM => "Interior derecho",
            Domain.Position.LW => "Extremo izquierdo",
            Domain.Position.RW => "Extremo derecho",
            _ => "Delantero centro",
        };

        public static string Role(SquadRole r) => r switch
        {
            SquadRole.Prospect => "Promesa",
            SquadRole.Rotation => "Rotación",
            SquadRole.Starter => "Titular",
            _ => "Estrella",
        };

        public static string MatchRole(MatchRole r) => r switch
        {
            Career.MatchRole.Starter => "Titular",
            Career.MatchRole.Substitute => "Suplente",
            Career.MatchRole.NotSelected => "No convocado",
            Career.MatchRole.Injured => "Lesionado",
            Career.MatchRole.Suspended => "Sancionado",
            _ => "Sin club",
        };

        public static string Focus(TrainingFocus f) => f switch
        {
            TrainingFocus.Pace => "Velocidad",
            TrainingFocus.Shooting => "Tiro",
            TrainingFocus.Passing => "Pase",
            TrainingFocus.Dribbling => "Regate",
            TrainingFocus.Defending => "Defensa",
            TrainingFocus.Physical => "Físico",
            TrainingFocus.Goalkeeping => "Portería",
            _ => "Equilibrado",
        };

        public static string Intensity(TrainingIntensity i) => i switch
        {
            TrainingIntensity.Low => "Suave",
            TrainingIntensity.High => "Intensa",
            _ => "Normal",
        };

        public static string Offer(OfferKind k) => k switch
        {
            OfferKind.InitialContract => "Contrato juvenil",
            OfferKind.Transfer => "Traspaso",
            OfferKind.Loan => "Cesión",
            OfferKind.Renewal => "Renovación",
            _ => "Fichaje libre",
        };

        public static string Attribute(AttributeId a) => a switch
        {
            AttributeId.Acceleration => "Aceleración",
            AttributeId.SprintSpeed => "Velocidad",
            AttributeId.Finishing => "Definición",
            AttributeId.ShotPower => "Potencia",
            AttributeId.LongShots => "Tiro lejano",
            AttributeId.Positioning => "Colocación",
            AttributeId.ShortPassing => "Pase corto",
            AttributeId.LongPassing => "Pase largo",
            AttributeId.Crossing => "Centros",
            AttributeId.Vision => "Visión",
            AttributeId.Dribbling => "Regate",
            AttributeId.BallControl => "Control",
            AttributeId.Balance => "Equilibrio",
            AttributeId.Agility => "Agilidad",
            AttributeId.Marking => "Marcaje",
            AttributeId.Tackling => "Entradas",
            AttributeId.DefensiveAwareness => "Lectura defensiva",
            AttributeId.Strength => "Fuerza",
            AttributeId.Stamina => "Resistencia",
            AttributeId.Jumping => "Salto",
            AttributeId.Reactions => "Reflejos",
            AttributeId.Composure => "Compostura",
            AttributeId.Aggression => "Agresividad",
            AttributeId.Heading => "Cabeza",
            AttributeId.GkDiving => "Estirada",
            AttributeId.GkHandling => "Blocaje",
            AttributeId.GkKicking => "Saque",
            AttributeId.GkPositioning => "Colocación (POR)",
            _ => "Reflejos (POR)",
        };

        public static string Nationality(string iso3) => iso3 switch
        {
            "ESP" => "España", "ARG" => "Argentina", "MEX" => "México", "COL" => "Colombia", "URY" => "Uruguay", "CHL" => "Chile",
            "BRA" => "Brasil", "PRT" => "Portugal", "ENG" => "Inglaterra", "USA" => "Estados Unidos", "IRL" => "Irlanda",
            "FRA" => "Francia", "BEL" => "Bélgica", "DEU" => "Alemania", "AUT" => "Austria", "NLD" => "Países Bajos",
            "ITA" => "Italia", "NGA" => "Nigeria", "SEN" => "Senegal", "GHA" => "Ghana", "CIV" => "Costa de Marfil",
            "HRV" => "Croacia", "SRB" => "Serbia",
            _ => iso3,
        };

        public static string Money(long credits) => credits >= 1_000_000 ? $"{credits / 1_000_000.0:0.0} M cr" : credits >= 1000 ? $"{credits / 1000.0:0.#} K cr" : $"{credits} cr";
    }
}
