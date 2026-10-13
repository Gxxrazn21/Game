using System.Collections.Generic;

namespace Dorsal.Core.Domain
{
    /// <summary>Instrucciones tácticas del equipo (0–100 cada una).</summary>
    public sealed class Tactics
    {
        public FormationId Formation { get; set; } = FormationId.F442;

        /// <summary>Intensidad de presión: 0 = repliegue, 100 = presión alta constante.</summary>
        public int Pressing { get; set; } = 50;

        /// <summary>Altura de la línea defensiva: 0 = muy baja, 100 = muy alta.</summary>
        public int DefensiveLine { get; set; } = 50;

        /// <summary>Amplitud en ataque.</summary>
        public int Width { get; set; } = 50;

        /// <summary>Ritmo: 0 = posesión paciente, 100 = vertical y directo.</summary>
        public int Tempo { get; set; } = 50;

        public Tactics Clone() => (Tactics)MemberwiseClone();
    }

    public sealed class Club
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string ShortName { get; set; } = "";
        public string City { get; set; } = "";

        /// <summary>Colores en hexadecimal (#RRGGBB) de diseño original.</summary>
        public string PrimaryColor { get; set; } = "#FFFFFF";
        public string SecondaryColor { get; set; } = "#000000";

        /// <summary>Descripción paramétrica de un escudo original (forma, emblema, colores).</summary>
        public CrestSpec Crest { get; set; } = new CrestSpec();

        public int Reputation { get; set; } = 50;

        /// <summary>Nivel deportivo objetivo del club (media aproximada de su once); evoluciona entre temporadas.</summary>
        public int Level { get; set; } = 65;
        public int? LeagueId { get; set; }
        public Tactics Tactics { get; set; } = new Tactics();
        public List<int> SquadPlayerIds { get; set; } = new List<int>();
        public string StadiumName { get; set; } = "";
        public int StadiumCapacity { get; set; } = 15000;
        public long TransferBudget { get; set; }
        public long WageBudgetWeekly { get; set; }
        public string SourcePackId { get; set; } = "";
    }

    /// <summary>Escudo generado proceduralmente: evita depender de imágenes con derechos de terceros.</summary>
    public sealed class CrestSpec
    {
        public int Shape { get; set; }
        public int Emblem { get; set; }
        public int Pattern { get; set; }
    }
}
