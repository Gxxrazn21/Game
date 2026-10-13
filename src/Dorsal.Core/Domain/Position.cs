using System;
using System.Collections.Generic;

namespace Dorsal.Core.Domain
{
    public enum Position
    {
        GK,
        CB,
        LB,
        RB,
        CDM,
        CM,
        CAM,
        LM,
        RM,
        LW,
        RW,
        ST,
    }

    public enum PositionLine
    {
        Goalkeeper,
        Defence,
        Midfield,
        Attack,
    }

    public static class Positions
    {
        public static PositionLine LineOf(Position p)
        {
            switch (p)
            {
                case Position.GK: return PositionLine.Goalkeeper;
                case Position.CB:
                case Position.LB:
                case Position.RB: return PositionLine.Defence;
                case Position.CDM:
                case Position.CM:
                case Position.CAM:
                case Position.LM:
                case Position.RM: return PositionLine.Midfield;
                default: return PositionLine.Attack;
            }
        }

        /// <summary>
        /// Encaje de un jugador en un puesto distinto al suyo (1 = natural, 0 = inutilizable).
        /// Se usa en la alineación y en la media efectiva.
        /// </summary>
        public static float Familiarity(Position natural, Position slot)
        {
            if (natural == slot) return 1f;
            if (natural == Position.GK || slot == Position.GK) return 0.05f;
            float similar = 0.9f, near = 0.78f, far = 0.6f;
            switch (natural)
            {
                case Position.CB: return slot == Position.CDM ? near : (slot == Position.LB || slot == Position.RB ? near : far);
                case Position.LB: return slot == Position.RB ? similar : (slot == Position.LM ? near : (slot == Position.CB ? near : far));
                case Position.RB: return slot == Position.LB ? similar : (slot == Position.RM ? near : (slot == Position.CB ? near : far));
                case Position.CDM: return slot == Position.CM ? similar : (slot == Position.CB ? near : far);
                case Position.CM: return slot == Position.CDM || slot == Position.CAM ? similar : (slot == Position.LM || slot == Position.RM ? near : far);
                case Position.CAM: return slot == Position.CM ? similar : (slot == Position.ST || slot == Position.LW || slot == Position.RW ? near : far);
                case Position.LM: return slot == Position.LW ? similar : (slot == Position.RM || slot == Position.CM || slot == Position.LB ? near : far);
                case Position.RM: return slot == Position.RW ? similar : (slot == Position.LM || slot == Position.CM || slot == Position.RB ? near : far);
                case Position.LW: return slot == Position.LM || slot == Position.RW ? similar : (slot == Position.ST || slot == Position.CAM ? near : far);
                case Position.RW: return slot == Position.RM || slot == Position.LW ? similar : (slot == Position.ST || slot == Position.CAM ? near : far);
                case Position.ST: return slot == Position.CAM || slot == Position.LW || slot == Position.RW ? near : far;
                default: return far;
            }
        }
    }

    /// <summary>
    /// Pesos de atributos por posición para calcular la media (OVR).
    /// Los pesos están en datos (no dispersos por el código) para poder ajustarlos sin tocar la lógica.
    /// </summary>
    public static class PositionRatings
    {
        private static readonly Dictionary<Position, (AttributeId attr, float w)[]> Weights =
            new Dictionary<Position, (AttributeId, float)[]>
            {
                [Position.GK] = new[]
                {
                    (AttributeId.GkDiving, 0.22f), (AttributeId.GkHandling, 0.21f), (AttributeId.GkPositioning, 0.21f),
                    (AttributeId.GkReflexes, 0.24f), (AttributeId.GkKicking, 0.06f), (AttributeId.Reactions, 0.06f),
                },
                [Position.CB] = new[]
                {
                    (AttributeId.Marking, 0.16f), (AttributeId.Tackling, 0.17f), (AttributeId.DefensiveAwareness, 0.17f),
                    (AttributeId.Heading, 0.10f), (AttributeId.Strength, 0.10f), (AttributeId.Jumping, 0.05f),
                    (AttributeId.ShortPassing, 0.06f), (AttributeId.BallControl, 0.05f), (AttributeId.Reactions, 0.06f),
                    (AttributeId.SprintSpeed, 0.04f), (AttributeId.Composure, 0.04f),
                },
                [Position.LB] = FullBack(),
                [Position.RB] = FullBack(),
                [Position.CDM] = new[]
                {
                    (AttributeId.ShortPassing, 0.13f), (AttributeId.LongPassing, 0.10f), (AttributeId.Tackling, 0.13f),
                    (AttributeId.DefensiveAwareness, 0.13f), (AttributeId.Marking, 0.08f), (AttributeId.BallControl, 0.08f),
                    (AttributeId.Vision, 0.06f), (AttributeId.Stamina, 0.07f), (AttributeId.Strength, 0.07f),
                    (AttributeId.Reactions, 0.08f), (AttributeId.Composure, 0.07f),
                },
                [Position.CM] = new[]
                {
                    (AttributeId.ShortPassing, 0.17f), (AttributeId.LongPassing, 0.13f), (AttributeId.Vision, 0.13f),
                    (AttributeId.BallControl, 0.14f), (AttributeId.Dribbling, 0.07f), (AttributeId.Stamina, 0.06f),
                    (AttributeId.Reactions, 0.08f), (AttributeId.Composure, 0.07f), (AttributeId.LongShots, 0.05f),
                    (AttributeId.Tackling, 0.05f), (AttributeId.Positioning, 0.05f),
                },
                [Position.CAM] = new[]
                {
                    (AttributeId.ShortPassing, 0.16f), (AttributeId.Vision, 0.14f), (AttributeId.BallControl, 0.15f),
                    (AttributeId.Dribbling, 0.13f), (AttributeId.Positioning, 0.09f), (AttributeId.Finishing, 0.07f),
                    (AttributeId.LongShots, 0.06f), (AttributeId.Agility, 0.04f), (AttributeId.Acceleration, 0.04f),
                    (AttributeId.Reactions, 0.07f), (AttributeId.Composure, 0.05f),
                },
                [Position.LM] = WideMid(),
                [Position.RM] = WideMid(),
                [Position.LW] = Winger(),
                [Position.RW] = Winger(),
                [Position.ST] = new[]
                {
                    (AttributeId.Finishing, 0.20f), (AttributeId.Positioning, 0.13f), (AttributeId.ShotPower, 0.09f),
                    (AttributeId.Heading, 0.07f), (AttributeId.BallControl, 0.10f), (AttributeId.Dribbling, 0.07f),
                    (AttributeId.SprintSpeed, 0.06f), (AttributeId.Acceleration, 0.05f), (AttributeId.Strength, 0.05f),
                    (AttributeId.Reactions, 0.08f), (AttributeId.Composure, 0.05f), (AttributeId.LongShots, 0.05f),
                },
            };

        private static (AttributeId, float)[] FullBack() => new[]
        {
            (AttributeId.Tackling, 0.13f), (AttributeId.Marking, 0.11f), (AttributeId.DefensiveAwareness, 0.12f),
            (AttributeId.SprintSpeed, 0.09f), (AttributeId.Acceleration, 0.07f), (AttributeId.Stamina, 0.08f),
            (AttributeId.Crossing, 0.09f), (AttributeId.ShortPassing, 0.08f), (AttributeId.BallControl, 0.07f),
            (AttributeId.Reactions, 0.08f), (AttributeId.Heading, 0.04f), (AttributeId.Agility, 0.04f),
        };

        private static (AttributeId, float)[] WideMid() => new[]
        {
            (AttributeId.Crossing, 0.12f), (AttributeId.Dribbling, 0.13f), (AttributeId.BallControl, 0.12f),
            (AttributeId.ShortPassing, 0.11f), (AttributeId.SprintSpeed, 0.08f), (AttributeId.Acceleration, 0.08f),
            (AttributeId.Stamina, 0.07f), (AttributeId.Vision, 0.07f), (AttributeId.Reactions, 0.07f),
            (AttributeId.Positioning, 0.06f), (AttributeId.Agility, 0.05f), (AttributeId.LongShots, 0.04f),
        };

        private static (AttributeId, float)[] Winger() => new[]
        {
            (AttributeId.Dribbling, 0.15f), (AttributeId.BallControl, 0.13f), (AttributeId.SprintSpeed, 0.10f),
            (AttributeId.Acceleration, 0.10f), (AttributeId.Crossing, 0.09f), (AttributeId.Finishing, 0.09f),
            (AttributeId.ShortPassing, 0.08f), (AttributeId.Positioning, 0.07f), (AttributeId.Agility, 0.07f),
            (AttributeId.Reactions, 0.07f), (AttributeId.Vision, 0.05f),
        };

        public static IReadOnlyList<(AttributeId attr, float w)> WeightsFor(Position p) => Weights[p];

        public static int Overall(Attributes a, Position p)
        {
            float sum = 0f, wsum = 0f;
            foreach (var (attr, w) in Weights[p])
            {
                sum += a[attr] * w;
                wsum += w;
            }
            return (int)Math.Round(sum / wsum);
        }

        /// <summary>Media efectiva al jugar en un puesto que no es el natural.</summary>
        public static int OverallInSlot(Attributes a, Position natural, Position slot)
        {
            float slotOvr = Overall(a, slot);
            return (int)Math.Round(slotOvr * Positions.Familiarity(natural, slot));
        }
    }
}
