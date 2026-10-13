using System;
using System.Collections.Generic;
using System.Linq;
using Dorsal.Core.Domain;
using Dorsal.Core.Random;
using Dorsal.Core.World;

namespace Dorsal.Core.Career
{
    /// <summary>Estilo de juego: desplaza la distribución inicial de atributos dentro de la posición.</summary>
    public sealed class PlayStyle
    {
        public PlayStyle(string id, string name, Position[] positions, (AttributeId attr, int delta)[] deltas)
        {
            Id = id;
            Name = name;
            Positions = positions;
            Deltas = deltas;
        }

        public string Id { get; }
        public string Name { get; }
        public Position[] Positions { get; }
        public (AttributeId attr, int delta)[] Deltas { get; }

        public static readonly IReadOnlyList<PlayStyle> All = new[]
        {
            new PlayStyle("gk-reflejos", "Portero de reflejos", new[] { Position.GK }, new[] { (AttributeId.GkReflexes, 6), (AttributeId.GkDiving, 4), (AttributeId.GkKicking, -4), (AttributeId.GkPositioning, -2) }),
            new PlayStyle("gk-colocacion", "Portero de colocación", new[] { Position.GK }, new[] { (AttributeId.GkPositioning, 6), (AttributeId.GkHandling, 4), (AttributeId.GkDiving, -3), (AttributeId.GkReflexes, -2) }),
            new PlayStyle("gk-libero", "Portero líbero", new[] { Position.GK }, new[] { (AttributeId.GkKicking, 7), (AttributeId.ShortPassing, 6), (AttributeId.SprintSpeed, 4), (AttributeId.GkHandling, -4) }),
            new PlayStyle("def-muro", "Central muro", new[] { Position.CB }, new[] { (AttributeId.Strength, 6), (AttributeId.Heading, 5), (AttributeId.Marking, 3), (AttributeId.Acceleration, -5), (AttributeId.ShortPassing, -3) }),
            new PlayStyle("def-salida", "Central con salida", new[] { Position.CB }, new[] { (AttributeId.ShortPassing, 6), (AttributeId.LongPassing, 5), (AttributeId.Composure, 3), (AttributeId.Strength, -4), (AttributeId.Heading, -3) }),
            new PlayStyle("lat-carrilero", "Lateral carrilero", new[] { Position.LB, Position.RB }, new[] { (AttributeId.Stamina, 6), (AttributeId.Crossing, 5), (AttributeId.SprintSpeed, 3), (AttributeId.Marking, -4), (AttributeId.Heading, -3) }),
            new PlayStyle("lat-defensivo", "Lateral defensivo", new[] { Position.LB, Position.RB }, new[] { (AttributeId.Tackling, 5), (AttributeId.Marking, 5), (AttributeId.Crossing, -4), (AttributeId.Dribbling, -3) }),
            new PlayStyle("mc-recuperador", "Recuperador", new[] { Position.CDM, Position.CM }, new[] { (AttributeId.Tackling, 6), (AttributeId.DefensiveAwareness, 5), (AttributeId.Stamina, 3), (AttributeId.Finishing, -5), (AttributeId.Dribbling, -3) }),
            new PlayStyle("mc-organizador", "Organizador", new[] { Position.CDM, Position.CM, Position.CAM }, new[] { (AttributeId.Vision, 6), (AttributeId.ShortPassing, 5), (AttributeId.LongPassing, 4), (AttributeId.Strength, -4), (AttributeId.Tackling, -3) }),
            new PlayStyle("mc-boxtobox", "Box to box", new[] { Position.CM }, new[] { (AttributeId.Stamina, 7), (AttributeId.Positioning, 3), (AttributeId.Tackling, 3), (AttributeId.Vision, -3), (AttributeId.Composure, -2) }),
            new PlayStyle("mp-mediapunta", "Mediapunta creativo", new[] { Position.CAM }, new[] { (AttributeId.Dribbling, 5), (AttributeId.Vision, 5), (AttributeId.LongShots, 3), (AttributeId.Strength, -5), (AttributeId.Tackling, -4) }),
            new PlayStyle("ext-regateador", "Extremo regateador", new[] { Position.LW, Position.RW, Position.LM, Position.RM }, new[] { (AttributeId.Dribbling, 6), (AttributeId.Agility, 5), (AttributeId.Acceleration, 3), (AttributeId.Strength, -5), (AttributeId.Heading, -4) }),
            new PlayStyle("ext-velocista", "Extremo velocista", new[] { Position.LW, Position.RW, Position.LM, Position.RM }, new[] { (AttributeId.SprintSpeed, 7), (AttributeId.Acceleration, 5), (AttributeId.BallControl, -3), (AttributeId.Vision, -3) }),
            new PlayStyle("ext-centrador", "Extremo centrador", new[] { Position.LM, Position.RM, Position.LW, Position.RW }, new[] { (AttributeId.Crossing, 7), (AttributeId.LongPassing, 3), (AttributeId.Dribbling, -3), (AttributeId.Finishing, -3) }),
            new PlayStyle("dc-rematador", "Rematador", new[] { Position.ST }, new[] { (AttributeId.Finishing, 6), (AttributeId.Positioning, 5), (AttributeId.Heading, 2), (AttributeId.Dribbling, -4), (AttributeId.Vision, -4) }),
            new PlayStyle("dc-movil", "Delantero móvil", new[] { Position.ST }, new[] { (AttributeId.Acceleration, 6), (AttributeId.SprintSpeed, 5), (AttributeId.Dribbling, 3), (AttributeId.Strength, -5), (AttributeId.Heading, -4) }),
            new PlayStyle("dc-referencia", "Delantero referencia", new[] { Position.ST }, new[] { (AttributeId.Strength, 7), (AttributeId.Heading, 6), (AttributeId.Acceleration, -5), (AttributeId.Agility, -4) }),
        };

        public static IEnumerable<PlayStyle> For(Position p) => All.Where(s => s.Positions.Contains(p));

        public static PlayStyle? Find(string id) => All.FirstOrDefault(s => s.Id == id);
    }

    public enum PersonalityArchetype
    {
        Professional,
        Ambitious,
        Temperamental,
        Loyal,
        Balanced,
    }

    /// <summary>Datos que el usuario introduce al crear su futbolista.</summary>
    public sealed class PlayerCreationRequest
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Nationality { get; set; } = "ESP";
        public DateTime BirthDate { get; set; }
        public int HeightCm { get; set; } = 180;
        public Build Build { get; set; } = Build.Normal;
        public Foot PreferredFoot { get; set; } = Foot.Right;
        public int WeakFoot { get; set; } = 3;
        public Position Position { get; set; } = Position.ST;
        public List<Position> SecondaryPositions { get; set; } = new List<Position>();
        public string PlayStyleId { get; set; } = "";
        public PersonalityArchetype Personality { get; set; } = PersonalityArchetype.Balanced;
        public Appearance Appearance { get; set; } = new Appearance();
        public int? PreferredShirtNumber { get; set; }
    }

    public static class PlayerCreator
    {
        public const int MinAge = 16;
        public const int MaxAge = 21;
        public const int StartingOverallMin = 48;
        public const int StartingOverallMax = 54;

        public static IReadOnlyList<string> Validate(PlayerCreationRequest r, DateTime careerStart)
        {
            var errors = new List<string>();
            ValidateName(r.FirstName, "nombre", errors);
            ValidateName(r.LastName, "apellido", errors);
            if (!NameGenerator.SupportedNationalities.Contains(r.Nationality)) errors.Add($"Nacionalidad no disponible: {r.Nationality}.");
            int age = AgeOn(r.BirthDate, careerStart);
            if (age < MinAge || age > MaxAge) errors.Add($"La edad inicial debe estar entre {MinAge} y {MaxAge} años (ahora {age}).");
            if (r.HeightCm < 155 || r.HeightCm > 205) errors.Add("La altura debe estar entre 155 y 205 cm.");
            if (r.WeakFoot < 1 || r.WeakFoot > 5) errors.Add("El pie débil debe valer entre 1 y 5.");
            if (r.SecondaryPositions.Count > 2) errors.Add("Máximo dos posiciones secundarias.");
            if (r.SecondaryPositions.Contains(r.Position)) errors.Add("Una posición secundaria no puede ser la principal.");
            if (r.SecondaryPositions.Distinct().Count() != r.SecondaryPositions.Count) errors.Add("Posiciones secundarias repetidas.");
            if (r.Position == Position.GK && r.SecondaryPositions.Count > 0) errors.Add("Un portero no puede tener posiciones secundarias de campo.");
            if (r.Position != Position.GK && r.SecondaryPositions.Contains(Position.GK)) errors.Add("Un jugador de campo no puede tener la portería como secundaria.");
            if (!string.IsNullOrEmpty(r.PlayStyleId))
            {
                var style = PlayStyle.Find(r.PlayStyleId);
                if (style == null) errors.Add("Estilo de juego desconocido.");
                else if (!style.Positions.Contains(r.Position)) errors.Add($"El estilo '{style.Name}' no corresponde a la posición elegida.");
            }
            if (r.PreferredShirtNumber.HasValue && (r.PreferredShirtNumber < 1 || r.PreferredShirtNumber > 99)) errors.Add("El dorsal debe estar entre 1 y 99.");
            return errors;
        }

        /// <summary>Crea el futbolista del usuario. Lanza ArgumentException si la solicitud no es válida.</summary>
        public static Player Create(PlayerCreationRequest r, DateTime careerStart, DeterministicRandom rng)
        {
            var errors = Validate(r, careerStart);
            if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors));

            int startOverall = rng.Range(StartingOverallMin, StartingOverallMax + 1);
            var attrs = PlayerFactory.GenerateAttributes(r.Position, startOverall, rng, r.HeightCm, r.Build);
            var style = PlayStyle.Find(r.PlayStyleId) ?? PlayStyle.For(r.Position).FirstOrDefault();
            if (style != null)
                foreach (var (attr, delta) in style.Deltas) attrs[attr] = attrs[attr] + delta;
            // Reajuste para conservar la media objetivo tras aplicar el estilo.
            int diff = startOverall - PositionRatings.Overall(attrs, r.Position);
            foreach (var (attr, _) in PositionRatings.WeightsFor(r.Position)) attrs[attr] = attrs[attr] + diff;

            int age = AgeOn(r.BirthDate, careerStart);
            var p = new Player
            {
                FirstName = r.FirstName.Trim(),
                LastName = r.LastName.Trim(),
                Nationality = r.Nationality,
                BirthDate = r.BirthDate.Date,
                HeightCm = r.HeightCm,
                WeightKg = r.HeightCm - 105 + (r.Build == Build.Stocky ? 6 : r.Build == Build.Lean ? -5 : 0),
                Build = r.Build,
                PreferredFoot = r.PreferredFoot,
                WeakFoot = r.WeakFoot,
                Position = r.Position,
                SecondaryPositions = r.SecondaryPositions.ToList(),
                PlayStyle = style?.Id ?? "",
                Attributes = attrs,
                Personality = PersonalityFor(r.Personality, rng),
                Appearance = r.Appearance,
                IsUserPlayer = true,
                Reputation = 5,
                ShirtNumber = r.PreferredShirtNumber,
                SourcePackId = Content.ContentRegistry.BasePackId,
            };
            // Potencial alto pero oculto; los más jóvenes tienen más margen.
            p.Potential = Math.Min(94, p.Overall + rng.Range(26, 37) - Math.Max(0, age - 17) * 2);
            return p;
        }

        /// <summary>Rango de potencial visible: se estrecha con la edad (más información sobre el jugador).</summary>
        public static (int min, int max) VisiblePotentialRange(Player p, DateTime date)
        {
            int age = p.AgeOn(date);
            int spread = age <= 18 ? 5 : age <= 21 ? 4 : age <= 24 ? 2 : 0;
            return (Math.Max(p.Overall, p.Potential - spread), Math.Min(99, p.Potential + spread));
        }

        public static Personality PersonalityFor(PersonalityArchetype a, DeterministicRandom rng)
        {
            int R(int lo, int hi) => rng.Range(lo, hi + 1);
            return a switch
            {
                PersonalityArchetype.Professional => new Personality { Professionalism = R(16, 20), Ambition = R(10, 15), Temperament = R(13, 18), Loyalty = R(10, 15) },
                PersonalityArchetype.Ambitious => new Personality { Professionalism = R(12, 16), Ambition = R(17, 20), Temperament = R(9, 14), Loyalty = R(5, 10) },
                PersonalityArchetype.Temperamental => new Personality { Professionalism = R(9, 14), Ambition = R(13, 18), Temperament = R(3, 8), Loyalty = R(8, 13) },
                PersonalityArchetype.Loyal => new Personality { Professionalism = R(13, 17), Ambition = R(8, 13), Temperament = R(12, 16), Loyalty = R(17, 20) },
                _ => new Personality { Professionalism = R(12, 15), Ambition = R(12, 15), Temperament = R(12, 15), Loyalty = R(12, 15) },
            };
        }

        private static void ValidateName(string s, string field, List<string> errors)
        {
            var t = (s ?? "").Trim();
            if (t.Length < 2 || t.Length > 20) { errors.Add($"El {field} debe tener entre 2 y 20 caracteres."); return; }
            if (!t.All(c => char.IsLetter(c) || c == ' ' || c == '-' || c == '\'')) errors.Add($"El {field} solo admite letras, espacios, guiones y apóstrofos.");
        }

        private static int AgeOn(DateTime birth, DateTime date)
        {
            int age = date.Year - birth.Year;
            if (date.Month < birth.Month || (date.Month == birth.Month && date.Day < birth.Day)) age--;
            return age;
        }
    }
}
