using System;
using System.Collections.Generic;
using Dorsal.Core.Random;

namespace Dorsal.Core.World
{
    /// <summary>
    /// Generador de nombres originales. Los apellidos se construyen con sílabas para reducir
    /// coincidencias con personas reales; además se puede pasar una lista de nombres reservados
    /// (p. ej. futbolistas famosos) que nunca se generarán.
    /// </summary>
    public sealed class NameGenerator
    {
        private sealed class NameStyle
        {
            public NameStyle(string[] first, string[] prefixes, string[] suffixes)
            {
                First = first;
                Prefixes = prefixes;
                Suffixes = suffixes;
            }

            public string[] First { get; }
            public string[] Prefixes { get; }
            public string[] Suffixes { get; }
        }

        private static readonly NameStyle Hispanic = new NameStyle(
            new[] { "Adrián", "Álvaro", "Andrés", "Bruno", "Carlos", "Daniel", "Diego", "Emilio", "Esteban", "Federico", "Gonzalo", "Hugo", "Iker", "Javier", "Joaquín", "Jorge", "Julián", "Lucas", "Manuel", "Marcos", "Mateo", "Nicolás", "Pablo", "Rafael", "Ramiro", "Rodrigo", "Santiago", "Sergio", "Tomás", "Víctor" },
            new[] { "Val", "Mar", "Cor", "Ber", "Sal", "Ama", "Est", "Riv", "Mon", "Gal", "Ped", "Ore", "Lin", "Car", "Tor", "Vel", "Ard", "Qui", "Bal", "Fer" },
            new[] { "ero", "ales", "ido", "ante", "uela", "arte", "ino", "orra", "ezo", "illa", "ano", "ueda", "ares", "ondo", "ín", "eño" });

        private static readonly NameStyle Lusophone = new NameStyle(
            new[] { "André", "Caio", "Davi", "Diogo", "Felipe", "Gabriel", "Gonçalo", "Henrique", "João", "Leandro", "Luan", "Marcelo", "Murilo", "Otávio", "Rafael", "Renato", "Thiago", "Vitor", "Wesley", "Yuri" },
            new[] { "Bran", "Cas", "Fon", "Gui", "Lou", "Mor", "Per", "Quei", "Rib", "Tav", "Vas", "Arau", "Mace", "Sou", "Pin" },
            new[] { "eira", "ado", "elo", "inho", "ães", "oso", "ares", "ega", "uim", "anha", "eto", "ote" });

        private static readonly NameStyle Anglo = new NameStyle(
            new[] { "Aaron", "Ben", "Callum", "Connor", "Dylan", "Ethan", "Finley", "George", "Harry", "Jack", "Jamie", "Jordan", "Kieran", "Liam", "Mason", "Nathan", "Owen", "Reece", "Ryan", "Tyler" },
            new[] { "Ash", "Brad", "Hal", "Kent", "Mar", "Pen", "Ros", "Stan", "Whit", "Thorn", "Gar", "Lang", "Bram", "Hol", "Wes" },
            new[] { "ford", "ley", "wick", "ton", "by", "well", "more", "worth", "field", "son", "dale", "croft" });

        private static readonly NameStyle Franco = new NameStyle(
            new[] { "Adrien", "Baptiste", "Clément", "Damien", "Enzo", "Florian", "Hugo", "Jules", "Kylian", "Léo", "Mathis", "Nolan", "Quentin", "Rayan", "Théo", "Yanis" },
            new[] { "Bel", "Char", "Duv", "Fau", "Gau", "Lam", "Mar", "Per", "Ron", "Vil", "Bou", "Mon", "Tess" },
            new[] { "ier", "ard", "ault", "elle", "in", "on", "eau", "ois", "et", "ac", "ière" });

        private static readonly NameStyle Germanic = new NameStyle(
            new[] { "Ben", "David", "Felix", "Florian", "Jan", "Jonas", "Julian", "Kai", "Lars", "Leon", "Lukas", "Max", "Niklas", "Paul", "Tim", "Tobias" },
            new[] { "Brand", "Eber", "Hart", "Kessel", "Lind", "Mohr", "Rein", "Stein", "Wald", "Zim", "Gross", "Holz", "Berg" },
            new[] { "mann", "er", "hardt", "inger", "ow", "berg", "feld", "hof", "ke", "rich", "stedt" });

        private static readonly NameStyle Italic = new NameStyle(
            new[] { "Alessandro", "Andrea", "Davide", "Federico", "Filippo", "Giacomo", "Lorenzo", "Luca", "Marco", "Matteo", "Nicolò", "Riccardo", "Simone", "Tommaso" },
            new[] { "Bel", "Cal", "Del", "Fer", "Gal", "Lom", "Mar", "Ross", "Sar", "Ten", "Vit", "Bor", "Cas" },
            new[] { "ini", "etti", "ardi", "one", "ucci", "elli", "ino", "aro", "ese", "otti", "ato" });

        private static readonly NameStyle WestAfrican = new NameStyle(
            new[] { "Abdou", "Amadou", "Chidi", "Emeka", "Ibrahima", "Kelechi", "Moussa", "Obinna", "Ousmane", "Samuel", "Seydou", "Tunde", "Yaw", "Kwame" },
            new[] { "Ade", "Ba", "Di", "Ek", "Ka", "Ma", "Nd", "Ok", "Sa", "To", "Ya", "Bo", "Fo" },
            new[] { "bayo", "kaye", "allo", "eke", "onye", "ama", "ouf", "iba", "uma", "ola", "ende" });

        private static readonly NameStyle Slavic = new NameStyle(
            new[] { "Aleksandar", "Bojan", "Dario", "Filip", "Ivan", "Luka", "Marko", "Mateo", "Milan", "Nikola", "Petar", "Stefan", "Toni", "Vedran" },
            new[] { "Bor", "Drag", "Hor", "Jov", "Kov", "Mil", "Pet", "Rad", "Stan", "Vuk", "Zor", "Bel" },
            new[] { "ić", "ović", "ac", "inski", "enko", "ar", "ek", "ević", "an" });

        private static readonly Dictionary<string, NameStyle> StyleByNation = new Dictionary<string, NameStyle>
        {
            ["ESP"] = Hispanic, ["ARG"] = Hispanic, ["MEX"] = Hispanic, ["COL"] = Hispanic, ["URY"] = Hispanic, ["CHL"] = Hispanic,
            ["BRA"] = Lusophone, ["PRT"] = Lusophone,
            ["ENG"] = Anglo, ["USA"] = Anglo, ["IRL"] = Anglo,
            ["FRA"] = Franco, ["BEL"] = Franco,
            ["DEU"] = Germanic, ["AUT"] = Germanic, ["NLD"] = Germanic,
            ["ITA"] = Italic,
            ["NGA"] = WestAfrican, ["SEN"] = WestAfrican, ["GHA"] = WestAfrican, ["CIV"] = WestAfrican,
            ["HRV"] = Slavic, ["SRB"] = Slavic,
        };

        public static IReadOnlyCollection<string> SupportedNationalities => StyleByNation.Keys;

        private static readonly string[] CityPrefixes = { "Val", "Mor", "Cal", "Ser", "Bel", "Ard", "Tor", "Lum", "Cor", "Nev", "Ost", "Ram", "Sol", "Vel", "Alt", "Bri", "Fal", "Gral", "Ise", "Mir" };
        private static readonly string[] CitySuffixes = { "monte", "aria", "dena", "vira", "oria", "abra", "uz", "anca", "ezal", "ona", "ida", "osa", "era", "ánzar", "edo" };

        private readonly DeterministicRandom _rng;
        private readonly HashSet<string> _reserved;
        private readonly HashSet<string> _used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public NameGenerator(DeterministicRandom rng, IEnumerable<string>? reservedFullNames = null)
        {
            _rng = rng;
            _reserved = new HashSet<string>(reservedFullNames ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        }

        public (string first, string last) PlayerName(string nationality)
        {
            if (!StyleByNation.TryGetValue(nationality, out var style)) style = Hispanic;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                string first = style.First[_rng.Range(0, style.First.Length)];
                string last = style.Prefixes[_rng.Range(0, style.Prefixes.Length)] + style.Suffixes[_rng.Range(0, style.Suffixes.Length)];
                string full = first + " " + last;
                if (_reserved.Contains(full)) continue;
                // Se permiten apellidos repetidos, pero no nombres completos repetidos.
                if (!_used.Add(full)) continue;
                return (first, last);
            }
            // Como último recurso se añade un segundo apellido.
            string f = style.First[_rng.Range(0, style.First.Length)];
            string l = style.Prefixes[_rng.Range(0, style.Prefixes.Length)] + style.Suffixes[_rng.Range(0, style.Suffixes.Length)] + "-" +
                       style.Prefixes[_rng.Range(0, style.Prefixes.Length)] + style.Suffixes[_rng.Range(0, style.Suffixes.Length)];
            _used.Add(f + " " + l);
            return (f, l);
        }

        public string CityName()
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                string name = CityPrefixes[_rng.Range(0, CityPrefixes.Length)] + CitySuffixes[_rng.Range(0, CitySuffixes.Length)];
                if (_used.Add("city:" + name)) return name;
            }
            return "Nueva " + CityPrefixes[_rng.Range(0, CityPrefixes.Length)] + CitySuffixes[_rng.Range(0, CitySuffixes.Length)];
        }
    }
}
