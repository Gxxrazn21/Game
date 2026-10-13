using System;
using System.Collections.Generic;
using Dorsal.Core.Domain;
using Newtonsoft.Json;

namespace Dorsal.Core.Career
{
    public enum CareerPhase
    {
        /// <summary>Recién creado: el jugador elige club o hace pruebas.</summary>
        ChoosingClub,
        /// <summary>Temporada en curso, listo para avanzar a la siguiente jornada.</summary>
        InSeason,
        /// <summary>Jornada preparada: falta jugar o simular el partido del usuario.</summary>
        MatchDay,
        /// <summary>Temporada terminada: resumen, premios, ascensos y descensos.</summary>
        SeasonEnded,
        /// <summary>Sin club: hay que aceptar una oferta o hacer pruebas.</summary>
        FreeAgent,
        Retired,
    }

    public enum OfferKind
    {
        InitialContract,
        Transfer,
        Loan,
        Renewal,
        FreeAgent,
    }

    public enum OfferStatus
    {
        Pending,
        Accepted,
        Rejected,
        Withdrawn,
        Expired,
    }

    public sealed class TransferOffer
    {
        public int Id { get; set; }
        public OfferKind Kind { get; set; }

        /// <summary>Club que hace la oferta (en una renovación, el club actual).</summary>
        public int ClubId { get; set; }

        public long Fee { get; set; }
        public long WageWeekly { get; set; }
        public int Years { get; set; } = 2;
        public SquadRole Role { get; set; }
        public long? ReleaseClause { get; set; }
        public OfferStatus Status { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime ExpiresOn { get; set; }
        public int NegotiationRound { get; set; }

        /// <summary>Salario máximo que el club está dispuesto a pagar (oculto al usuario).</summary>
        public long MaxWage { get; set; }
    }

    public sealed class NewsItem
    {
        public DateTime Date { get; set; }
        public string Text { get; set; } = "";
        public string Category { get; set; } = "general";
    }

    public enum ObjectiveKind
    {
        Appearances,
        GoalContributions,
        AverageRating,
        CleanSheets,
        LeaguePosition,
    }

    public sealed class CareerObjective
    {
        public ObjectiveKind Kind { get; set; }
        public string Description { get; set; } = "";
        public float Target { get; set; }
        public bool Evaluated { get; set; }
        public bool Completed { get; set; }
    }

    public sealed class CareerAward
    {
        public int SeasonYear { get; set; }
        public string Name { get; set; } = "";
        public string Competition { get; set; } = "";
    }

    public sealed class SeasonRecord
    {
        public int SeasonYear { get; set; }
        public int ClubId { get; set; }
        public string ClubName { get; set; } = "";
        public string LeagueName { get; set; } = "";
        public bool OnLoan { get; set; }
        public int Appearances { get; set; }
        public int Starts { get; set; }
        public int Minutes { get; set; }
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int CleanSheets { get; set; }
        public float AverageRating { get; set; }
        public int LeaguePosition { get; set; }
        public int OverallAtEnd { get; set; }
        public List<string> Awards { get; set; } = new List<string>();
        public int ObjectivesCompleted { get; set; }
        public int ObjectivesTotal { get; set; }
    }

    /// <summary>Alineación preparada para la jornada (solo ids: el estado persistido no duplica jugadores).</summary>
    public sealed class LineupIds
    {
        public int ClubId { get; set; }
        public List<int> Starters { get; set; } = new List<int>();
        public List<int> Bench { get; set; } = new List<int>();
    }

    public sealed class CareerSettings
    {
        /// <summary>Duración de cada parte del partido jugado (segundos reales).</summary>
        public float HalfDurationSeconds { get; set; } = 240f;

        public int TickRate { get; set; } = 50;
    }

    /// <summary>Estado persistente del modo carrera (todo lo que no es el mundo).</summary>
    public sealed class CareerState
    {
        public int UserPlayerId { get; set; }
        public CareerPhase Phase { get; set; }
        public uint[] RngState { get; set; } = new uint[4];
        public DateTime CareerStartDate { get; set; }
        public int RoundsPlayed { get; set; }
        public float CoachTrust { get; set; } = 50f;
        public float Popularity { get; set; } = 5f;
        public long Bank { get; set; }
        public int Experience { get; set; }
        public TrainingFocus TrainingFocus { get; set; } = TrainingFocus.Balanced;
        public TrainingIntensity TrainingIntensity { get; set; } = TrainingIntensity.Normal;
        public bool TransferRequested { get; set; }
        public List<CareerObjective> Objectives { get; set; } = new List<CareerObjective>();
        public List<NewsItem> News { get; set; } = new List<NewsItem>();
        public List<TransferOffer> Offers { get; set; } = new List<TransferOffer>();
        public List<SeasonRecord> History { get; set; } = new List<SeasonRecord>();
        public List<CareerAward> Awards { get; set; } = new List<CareerAward>();
        public List<string> Achievements { get; set; } = new List<string>();
        public List<int> TrialsUsed { get; set; } = new List<int>();
        public int NextOfferId { get; set; } = 1;

        // Jornada pendiente
        public int? PendingFixtureId { get; set; }
        public MatchRole PendingRole { get; set; }
        public string PendingObjective { get; set; } = "";
        public string PendingInstruction { get; set; } = "";
        public ulong PendingMatchSeed { get; set; }
        public Dictionary<int, LineupIds> PendingLineups { get; set; } = new Dictionary<int, LineupIds>();
        public List<float> WeekTrainingGrades { get; set; } = new List<float>();

        /// <summary>Contrato con el club propietario mientras el usuario está cedido.</summary>
        public Contract? ParentContract { get; set; }

        public int TotalAppearances { get; set; }
        public int TotalGoals { get; set; }
        public int TotalAssists { get; set; }
        public CareerSettings Settings { get; set; } = new CareerSettings();

        [JsonIgnore]
        public bool IsRetired => Phase == CareerPhase.Retired;
    }
}
