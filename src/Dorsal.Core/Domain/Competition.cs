using System;
using System.Collections.Generic;

namespace Dorsal.Core.Domain
{
    public enum CompetitionType
    {
        League,
        Cup,
    }

    public enum Tiebreaker
    {
        GoalDifference,
        GoalsFor,
        HeadToHeadPoints,
        HeadToHeadGoalDifference,
        Wins,
        AwayGoalsFor,
    }

    /// <summary>
    /// Reglas de una competición. Se cargan desde datos (JSON) para no programar
    /// las reglas de cada liga en el código.
    /// </summary>
    public sealed class CompetitionRules
    {
        public string Id { get; set; } = "default-league";
        public CompetitionType Type { get; set; } = CompetitionType.League;

        /// <summary>Vueltas de la liga (2 = ida y vuelta).</summary>
        public int Rounds { get; set; } = 2;

        public int PointsForWin { get; set; } = 3;
        public int PointsForDraw { get; set; } = 1;
        public int PointsForLoss { get; set; }

        public List<Tiebreaker> Tiebreakers { get; set; } = new List<Tiebreaker>
        {
            Tiebreaker.GoalDifference,
            Tiebreaker.GoalsFor,
            Tiebreaker.HeadToHeadPoints,
        };

        public int PromotionSlots { get; set; }
        public int RelegationSlots { get; set; }
        public int MaxSubstitutions { get; set; } = 5;
        public int BenchSize { get; set; } = 7;
        public int YellowCardsForBan { get; set; } = 5;
        public int MinSquadSize { get; set; } = 18;
        public int MaxSquadSize { get; set; } = 28;

        /// <summary>Días entre jornadas.</summary>
        public int DaysBetweenRounds { get; set; } = 7;

        public void Validate()
        {
            if (Rounds < 1 || Rounds > 4) throw new InvalidOperationException($"Reglas '{Id}': Rounds fuera de rango.");
            if (PointsForWin <= PointsForDraw) throw new InvalidOperationException($"Reglas '{Id}': la victoria debe valer más que el empate.");
            if (MaxSubstitutions < 0 || MaxSubstitutions > BenchSize) throw new InvalidOperationException($"Reglas '{Id}': cambios incoherentes con el banquillo.");
            if (MinSquadSize > MaxSquadSize) throw new InvalidOperationException($"Reglas '{Id}': tamaño de plantilla incoherente.");
            if (DaysBetweenRounds < 1) throw new InvalidOperationException($"Reglas '{Id}': DaysBetweenRounds debe ser ≥ 1.");
        }
    }

    public sealed class Competition
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string ShortName { get; set; } = "";
        public int Tier { get; set; } = 1;
        public CompetitionRules Rules { get; set; } = new CompetitionRules();
        public List<int> ClubIds { get; set; } = new List<int>();
        public string SourcePackId { get; set; } = "";
    }

    public sealed class Fixture
    {
        public int Id { get; set; }
        public int CompetitionId { get; set; }
        public int Round { get; set; }
        public DateTime Date { get; set; }
        public int HomeClubId { get; set; }
        public int AwayClubId { get; set; }
        public FixtureResult? Result { get; set; }

        public bool IsPlayed => Result != null;
    }

    public sealed class FixtureResult
    {
        public int HomeGoals { get; set; }
        public int AwayGoals { get; set; }
    }
}
