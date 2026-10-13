using System;
using Newtonsoft.Json;

namespace Dorsal.Core.Domain
{
    /// <summary>Atributos del futbolista. El orden es parte del formato de guardado: añadir solo al final.</summary>
    public enum AttributeId
    {
        // Ritmo
        Acceleration,
        SprintSpeed,
        // Tiro
        Finishing,
        ShotPower,
        LongShots,
        Positioning,
        // Pase
        ShortPassing,
        LongPassing,
        Crossing,
        Vision,
        // Regate
        Dribbling,
        BallControl,
        Balance,
        Agility,
        // Defensa
        Marking,
        Tackling,
        DefensiveAwareness,
        // Físico
        Strength,
        Stamina,
        Jumping,
        // Mental
        Reactions,
        Composure,
        Aggression,
        Heading,
        // Portero
        GkDiving,
        GkHandling,
        GkKicking,
        GkPositioning,
        GkReflexes,
    }

    public enum AttributeGroup
    {
        Pace,
        Shooting,
        Passing,
        Dribbling,
        Defending,
        Physical,
        Mental,
        Goalkeeping,
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class Attributes
    {
        public const int Min = 1;
        public const int Max = 99;
        public static readonly int Count = Enum.GetValues(typeof(AttributeId)).Length;

        [JsonProperty("v")]
        private int[] _values;

        public Attributes()
        {
            _values = new int[Count];
            for (int i = 0; i < Count; i++) _values[i] = 50;
        }

        public Attributes(int uniformValue) : this()
        {
            for (int i = 0; i < Count; i++) _values[i] = Clamp(uniformValue);
        }

        public int this[AttributeId id]
        {
            get => _values[(int)id];
            set => _values[(int)id] = Clamp(value);
        }

        /// <summary>Valor normalizado en [0, 1] para el motor de partido.</summary>
        public float N(AttributeId id) => (_values[(int)id] - Min) / (float)(Max - Min);

        public Attributes Clone()
        {
            var c = new Attributes();
            Array.Copy(_values, c._values, Count);
            return c;
        }

        /// <summary>Suma un delta a todos los atributos (útil para pruebas y niveles de dificultad).</summary>
        public Attributes WithOffset(int delta)
        {
            var c = Clone();
            for (int i = 0; i < Count; i++) c._values[i] = Clamp(c._values[i] + delta);
            return c;
        }

        public static AttributeGroup GroupOf(AttributeId id)
        {
            switch (id)
            {
                case AttributeId.Acceleration:
                case AttributeId.SprintSpeed:
                    return AttributeGroup.Pace;
                case AttributeId.Finishing:
                case AttributeId.ShotPower:
                case AttributeId.LongShots:
                case AttributeId.Positioning:
                    return AttributeGroup.Shooting;
                case AttributeId.ShortPassing:
                case AttributeId.LongPassing:
                case AttributeId.Crossing:
                case AttributeId.Vision:
                    return AttributeGroup.Passing;
                case AttributeId.Dribbling:
                case AttributeId.BallControl:
                case AttributeId.Balance:
                case AttributeId.Agility:
                    return AttributeGroup.Dribbling;
                case AttributeId.Marking:
                case AttributeId.Tackling:
                case AttributeId.DefensiveAwareness:
                    return AttributeGroup.Defending;
                case AttributeId.Strength:
                case AttributeId.Stamina:
                case AttributeId.Jumping:
                    return AttributeGroup.Physical;
                case AttributeId.Reactions:
                case AttributeId.Composure:
                case AttributeId.Aggression:
                case AttributeId.Heading:
                    return AttributeGroup.Mental;
                default:
                    return AttributeGroup.Goalkeeping;
            }
        }

        private static int Clamp(int v) => v < Min ? Min : (v > Max ? Max : v);

        [JsonConstructor]
        private Attributes(int[] v)
        {
            _values = new int[Count];
            if (v != null) Array.Copy(v, _values, Math.Min(v.Length, Count));
            // Atributos añadidos en versiones posteriores reciben un valor neutro.
            for (int i = v?.Length ?? 0; i < Count; i++) _values[i] = 50;
        }
    }
}
