using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Dorsal.Core.Domain
{
    public enum Foot
    {
        Right,
        Left,
    }

    public enum Build
    {
        Lean,
        Normal,
        Stocky,
    }

    /// <summary>Rasgos de personalidad deportiva (1–20).</summary>
    public sealed class Personality
    {
        public int Professionalism { get; set; } = 10;
        public int Ambition { get; set; } = 10;
        public int Temperament { get; set; } = 10;
        public int Loyalty { get; set; } = 10;
    }

    /// <summary>Estado físico y anímico, que cambia semana a semana.</summary>
    public sealed class PlayerCondition
    {
        /// <summary>Estado físico 0–100 (100 = recuperado por completo).</summary>
        public float Fitness { get; set; } = 100f;

        /// <summary>Forma reciente −10…+10, derivada de las últimas notas.</summary>
        public float Form { get; set; }

        /// <summary>Moral 0–100.</summary>
        public float Morale { get; set; } = 60f;
    }

    public sealed class Appearance
    {
        public int SkinTone { get; set; }
        public int FacePreset { get; set; }
        public int HairStyle { get; set; }
        public int HairColor { get; set; }
        public int FacialHair { get; set; }
    }

    public sealed class Player
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";

        /// <summary>Código de país ISO 3166-1 alfa-3 (dato geográfico; no implica ninguna marca de federación).</summary>
        public string Nationality { get; set; } = "";

        public DateTime BirthDate { get; set; }
        public int HeightCm { get; set; } = 180;
        public int WeightKg { get; set; } = 75;
        public Build Build { get; set; } = Build.Normal;
        public Foot PreferredFoot { get; set; } = Foot.Right;

        /// <summary>Pie débil 1–5.</summary>
        public int WeakFoot { get; set; } = 3;

        public Position Position { get; set; }
        public List<Position> SecondaryPositions { get; set; } = new List<Position>();
        public string PlayStyle { get; set; } = "";
        public Attributes Attributes { get; set; } = new Attributes();

        /// <summary>Techo de media (oculto para el usuario, que ve un rango).</summary>
        public int Potential { get; set; } = 70;

        public Personality Personality { get; set; } = new Personality();
        public PlayerCondition Condition { get; set; } = new PlayerCondition();
        public Appearance Appearance { get; set; } = new Appearance();
        public int? ClubId { get; set; }
        public int? ShirtNumber { get; set; }
        public int Reputation { get; set; } = 10;
        public bool IsUserPlayer { get; set; }

        /// <summary>Paquete de contenido del que procede (control de licencias).</summary>
        public string SourcePackId { get; set; } = "";

        /// <summary>Lesión actual (null si está disponible).</summary>
        public Injury? Injury { get; set; }

        /// <summary>Partidos de sanción pendientes.</summary>
        public int SuspensionMatches { get; set; }

        /// <summary>Amarillas acumuladas para el ciclo de sanción.</summary>
        public int YellowAccumulated { get; set; }

        /// <summary>Puntos fraccionarios de progresión pendientes de aplicar.</summary>
        public float ProgressAccumulator { get; set; }

        /// <summary>Últimas notas (la más reciente al final) para calcular la forma.</summary>
        public List<float> RecentRatings { get; set; } = new List<float>();

        /// <summary>Minutos de los últimos partidos de su club (para la progresión por tiempo de juego).</summary>
        public List<int> RecentMinutes { get; set; } = new List<int>();

        public bool Retired { get; set; }

        [JsonIgnore]
        public bool IsAvailable => Injury == null && SuspensionMatches <= 0 && !Retired;

        [JsonIgnore]
        public string DisplayName => string.IsNullOrEmpty(FirstName) ? LastName : FirstName.Substring(0, 1) + ". " + LastName;

        [JsonIgnore]
        public string FullName => (FirstName + " " + LastName).Trim();

        [JsonIgnore]
        public int Overall => PositionRatings.Overall(Attributes, Position);

        public int AgeOn(DateTime date)
        {
            int age = date.Year - BirthDate.Year;
            if (date.Month < BirthDate.Month || (date.Month == BirthDate.Month && date.Day < BirthDate.Day)) age--;
            return age;
        }

        /// <summary>Mejor encaje entre la posición natural y las secundarias.</summary>
        public float FamiliarityWith(Position slot)
        {
            float best = Positions.Familiarity(Position, slot);
            foreach (var p in SecondaryPositions)
            {
                // Las secundarias valen algo menos que la natural.
                float f = p == slot ? 0.95f : Positions.Familiarity(p, slot) * 0.95f;
                if (f > best) best = f;
            }
            return best;
        }

        public int OverallInSlot(Position slot) =>
            (int)Math.Round(PositionRatings.Overall(Attributes, slot) * FamiliarityWith(slot));
    }
}
