using System;

namespace Dorsal.Core.Match
{
    public enum TeamSide
    {
        Home,
        Away,
    }

    public enum MatchPhase
    {
        /// <summary>Balón parado: los jugadores se colocan y el lanzador ejecuta al terminar la preparación.</summary>
        SetPiece,
        Playing,
        GoalCelebration,
        HalfTime,
        FullTime,
    }

    public enum SetPieceType
    {
        KickOff,
        ThrowIn,
        Corner,
        GoalKick,
        FreeKick,
        Penalty,
    }

    public enum KickKind
    {
        None,
        Pass,
        ThroughPass,
        LoftedPass,
        Cross,
        Shot,
        Header,
        Clearance,
        ThrowIn,
        GoalKick,
        Corner,
        GkDistribution,
        Deflection,
    }

    /// <summary>Acción visible del agente (para animaciones en la capa de presentación).</summary>
    public enum AgentAction
    {
        None,
        Pass,
        Shoot,
        Cross,
        Tackle,
        Header,
        Dive,
        Catch,
        Throw,
        Celebrate,
        Stumble,
    }

    public enum MatchEventType
    {
        KickOff,
        Goal,
        OwnGoal,
        Shot,
        ShotSaved,
        ShotOffTarget,
        ShotBlocked,
        Foul,
        YellowCard,
        SecondYellow,
        RedCard,
        Offside,
        Corner,
        ThrowIn,
        GoalKick,
        FreeKick,
        Penalty,
        PenaltyMissed,
        Substitution,
        Injury,
        HalfTime,
        FullTime,
    }

    public sealed class MatchEvent
    {
        public MatchEvent(int minute, MatchEventType type, TeamSide side, int playerId, int otherPlayerId, Vec2 position)
        {
            Minute = minute;
            Type = type;
            Side = side;
            PlayerId = playerId;
            OtherPlayerId = otherPlayerId;
            Position = position;
        }

        /// <summary>Minuto de partido (1–90+, incluye añadido: 45+2 se guarda como 47 con Half=1).</summary>
        public int Minute { get; }
        public int Half { get; internal set; } = 1;
        public MatchEventType Type { get; }
        public TeamSide Side { get; }

        /// <summary>Protagonista (0 si no aplica).</summary>
        public int PlayerId { get; }

        /// <summary>Jugador secundario: asistente, víctima de la falta, jugador que entra… (0 si no aplica).</summary>
        public int OtherPlayerId { get; }

        public Vec2 Position { get; }

        public override string ToString() => $"{Minute}' {Type} {Side} #{PlayerId}" + (OtherPlayerId != 0 ? $" (+{OtherPlayerId})" : "");
    }

    public enum InputAction
    {
        None,
        Pass,
        ThroughPass,
        LoftedPass,
        Shoot,
        Cross,
        Tackle,
    }

    /// <summary>Entrada humana para el agente controlado en un tick.</summary>
    public struct PlayerInput
    {
        /// <summary>Dirección del joystick en coordenadas de campo (longitud 0–1).</summary>
        public Vec2 Move;
        public bool Sprint;

        /// <summary>Acción pulsada en este tick (flanco, no mantenida).</summary>
        public InputAction Action;

        /// <summary>Potencia 0–1 (p. ej. tiempo de pulsación del tiro).</summary>
        public float Power;

        public static PlayerInput None => new PlayerInput();
    }

    public sealed class MatchConfig
    {
        /// <summary>Segundos de simulación por parte. El reloj de partido se escala a 45'.</summary>
        public float HalfDurationSeconds { get; set; } = 270f;

        /// <summary>Pasos de simulación por segundo.</summary>
        public int TickRate { get; set; } = 20;

        public int MaxSubstitutions { get; set; } = 5;
        public bool EnableInjuries { get; set; } = true;
        public bool EnableOffside { get; set; } = true;

        /// <summary>Ventaja de campo: se suma a los atributos normalizados del local (0 = campo neutral).</summary>
        public float HomeAdvantage { get; set; } = 0.015f;

        /// <summary>Si es false, el entrenador IA no hace cambios por cansancio (útil en pruebas).</summary>
        public bool EnableTacticalSubstitutions { get; set; } = true;

        public void Validate()
        {
            if (HalfDurationSeconds < 30f) throw new ArgumentException("HalfDurationSeconds debe ser ≥ 30.");
            if (TickRate < 10 || TickRate > 120) throw new ArgumentException("TickRate debe estar entre 10 y 120.");
            if (MaxSubstitutions < 0) throw new ArgumentException("MaxSubstitutions no puede ser negativo.");
        }
    }

    /// <summary>Geometría reglamentaria del campo (metros).</summary>
    public static class Pitch
    {
        public const float HalfLength = 52.5f;
        public const float HalfWidth = 34f;
        public const float GoalHalfWidth = 3.66f;
        public const float GoalHeight = 2.44f;
        public const float PenaltyAreaDepth = 16.5f;
        public const float PenaltyAreaHalfWidth = 20.16f;
        public const float GoalAreaDepth = 5.5f;
        public const float PenaltySpotDistance = 11f;
        public const float CenterCircleRadius = 9.15f;
        public const float BallRadius = 0.11f;

        /// <summary>¿Está el punto dentro del área de penalti de la portería situada en X = goalSign·HalfLength?</summary>
        public static bool InPenaltyArea(Vec2 p, int goalSign) =>
            p.X * goalSign > HalfLength - PenaltyAreaDepth && p.X * goalSign <= HalfLength + 0.5f && Math.Abs(p.Y) < PenaltyAreaHalfWidth;

        public static Vec2 GoalCenter(int goalSign) => new Vec2(HalfLength * goalSign, 0f);
    }
}
