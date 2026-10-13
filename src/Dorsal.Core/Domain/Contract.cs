using System;
using Newtonsoft.Json;

namespace Dorsal.Core.Domain
{
    /// <summary>Rol prometido en el contrato; influye en las decisiones del entrenador y en las expectativas.</summary>
    public enum SquadRole
    {
        Prospect,
        Rotation,
        Starter,
        Star,
    }

    public sealed class Contract
    {
        public int PlayerId { get; set; }

        /// <summary>Club donde juega (en una cesión, el club que lo recibe).</summary>
        public int ClubId { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public long WageWeekly { get; set; }
        public long AppearanceBonus { get; set; }
        public long GoalBonus { get; set; }
        public long? ReleaseClause { get; set; }
        public SquadRole Role { get; set; } = SquadRole.Rotation;
        public bool IsLoan { get; set; }

        /// <summary>Club propietario durante una cesión.</summary>
        public int? ParentClubId { get; set; }

        public Contract Clone() => (Contract)MemberwiseClone();

        public double YearsRemaining(DateTime date) => Math.Max(0, (EndDate - date).TotalDays / 365.25);
    }

    public enum TransferType
    {
        Transfer,
        Loan,
        LoanReturn,
        FreeAgentSigning,
        Release,
        Retirement,
        YouthIntake,
    }

    public sealed class TransferRecord
    {
        public int PlayerId { get; set; }
        public int? FromClubId { get; set; }
        public int? ToClubId { get; set; }
        public DateTime Date { get; set; }
        public long Fee { get; set; }
        public TransferType Type { get; set; }
    }

    public sealed class Injury
    {
        public string Description { get; set; } = "";
        public int DaysRemaining { get; set; }
        public DateTime StartDate { get; set; }
    }

    /// <summary>Estadísticas acumuladas de un jugador en la temporada actual (todas las competiciones de liga).</summary>
    public sealed class PlayerSeasonStats
    {
        public int PlayerId { get; set; }
        public int ClubId { get; set; }
        public int Appearances { get; set; }
        public int Starts { get; set; }
        public int Minutes { get; set; }
        public int Goals { get; set; }
        public int Assists { get; set; }
        public float RatingSum { get; set; }
        public int RatedMatches { get; set; }
        public int YellowCards { get; set; }
        public int RedCards { get; set; }
        public int CleanSheets { get; set; }

        [JsonIgnore]
        public float AverageRating => RatedMatches == 0 ? 0f : RatingSum / RatedMatches;
    }
}
