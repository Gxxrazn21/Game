using System;
using System.Collections.Generic;

namespace Dorsal.Core.Domain
{
    public enum FormationId
    {
        F442,
        F433,
        F4231,
        F352,
    }

    /// <summary>
    /// Puesto dentro de una formación. Coordenadas de forma relativas al equipo:
    /// Depth −1 = línea defensiva, +1 = línea de delanteros; Lateral −1…+1 (positivo = banda izquierda del equipo).
    /// </summary>
    public sealed class FormationSlot
    {
        public FormationSlot(Position position, float depth, float lateral)
        {
            Position = position;
            Depth = depth;
            Lateral = lateral;
        }

        public Position Position { get; }
        public float Depth { get; }
        public float Lateral { get; }
    }

    public sealed class Formation
    {
        private Formation(FormationId id, string name, FormationSlot[] slots)
        {
            Id = id;
            Name = name;
            Slots = slots;
        }

        public FormationId Id { get; }
        public string Name { get; }

        /// <summary>11 puestos; el índice 0 es siempre el portero.</summary>
        public IReadOnlyList<FormationSlot> Slots { get; }

        private static readonly Dictionary<FormationId, Formation> All = new Dictionary<FormationId, Formation>
        {
            [FormationId.F442] = new Formation(FormationId.F442, "4-4-2", new[]
            {
                Gk(),
                new FormationSlot(Position.LB, -1f, 0.82f), new FormationSlot(Position.CB, -1f, 0.28f),
                new FormationSlot(Position.CB, -1f, -0.28f), new FormationSlot(Position.RB, -1f, -0.82f),
                new FormationSlot(Position.LM, 0f, 0.8f), new FormationSlot(Position.CM, -0.05f, 0.25f),
                new FormationSlot(Position.CM, -0.05f, -0.25f), new FormationSlot(Position.RM, 0f, -0.8f),
                new FormationSlot(Position.ST, 1f, 0.2f), new FormationSlot(Position.ST, 0.9f, -0.2f),
            }),
            [FormationId.F433] = new Formation(FormationId.F433, "4-3-3", new[]
            {
                Gk(),
                new FormationSlot(Position.LB, -1f, 0.82f), new FormationSlot(Position.CB, -1f, 0.28f),
                new FormationSlot(Position.CB, -1f, -0.28f), new FormationSlot(Position.RB, -1f, -0.82f),
                new FormationSlot(Position.CDM, -0.5f, 0f), new FormationSlot(Position.CM, 0.05f, 0.42f),
                new FormationSlot(Position.CM, 0.05f, -0.42f),
                new FormationSlot(Position.LW, 0.85f, 0.75f), new FormationSlot(Position.ST, 1f, 0f),
                new FormationSlot(Position.RW, 0.85f, -0.75f),
            }),
            [FormationId.F4231] = new Formation(FormationId.F4231, "4-2-3-1", new[]
            {
                Gk(),
                new FormationSlot(Position.LB, -1f, 0.82f), new FormationSlot(Position.CB, -1f, 0.28f),
                new FormationSlot(Position.CB, -1f, -0.28f), new FormationSlot(Position.RB, -1f, -0.82f),
                new FormationSlot(Position.CDM, -0.5f, 0.25f), new FormationSlot(Position.CDM, -0.5f, -0.25f),
                new FormationSlot(Position.LM, 0.45f, 0.78f), new FormationSlot(Position.CAM, 0.45f, 0f),
                new FormationSlot(Position.RM, 0.45f, -0.78f), new FormationSlot(Position.ST, 1f, 0f),
            }),
            [FormationId.F352] = new Formation(FormationId.F352, "3-5-2", new[]
            {
                Gk(),
                new FormationSlot(Position.CB, -1f, 0.45f), new FormationSlot(Position.CB, -1f, 0f),
                new FormationSlot(Position.CB, -1f, -0.45f), new FormationSlot(Position.LM, -0.1f, 0.88f),
                new FormationSlot(Position.CDM, -0.45f, 0f), new FormationSlot(Position.CM, 0.05f, 0.35f),
                new FormationSlot(Position.CM, 0.05f, -0.35f), new FormationSlot(Position.RM, -0.1f, -0.88f),
                new FormationSlot(Position.ST, 1f, 0.2f), new FormationSlot(Position.ST, 0.9f, -0.2f),
            }),
        };

        public static Formation Get(FormationId id) =>
            All.TryGetValue(id, out var f) ? f : throw new ArgumentOutOfRangeException(nameof(id));

        public static IEnumerable<Formation> List() => All.Values;

        private static FormationSlot Gk() => new FormationSlot(Position.GK, -2f, 0f);
    }
}
