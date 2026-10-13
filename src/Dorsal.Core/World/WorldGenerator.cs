using System;
using System.Collections.Generic;
using System.Linq;
using Dorsal.Core.Content;
using Dorsal.Core.Domain;
using Dorsal.Core.Random;
using Dorsal.Core.Simulation;

namespace Dorsal.Core.World
{
    public sealed class WorldGenerationOptions
    {
        public ulong Seed { get; set; } = 1;
        public string LeagueName { get; set; } = "Liga Solaria";
        public string LeagueShortName { get; set; } = "LSO";
        public int ClubCount { get; set; } = 12;

        /// <summary>Media de plantilla del club más débil y del más fuerte.</summary>
        public int MinClubLevel { get; set; } = 62;
        public int MaxClubLevel { get; set; } = 75;

        public DateTime SeasonStart { get; set; } = new DateTime(2026, 8, 15);
        public CompetitionRules? Rules { get; set; }
        public IEnumerable<string>? ReservedNames { get; set; }
    }

    /// <summary>
    /// Genera el mundo ficticio de la Configuración A: un país ficticio, una liga, clubes,
    /// plantillas y calendario. No necesita conexión ni datos externos.
    /// </summary>
    public static class WorldGenerator
    {
        // Composición de plantilla (23 jugadores).
        private static readonly Position[] SquadTemplate =
        {
            Position.GK, Position.GK,
            Position.CB, Position.CB, Position.CB, Position.CB,
            Position.LB, Position.LB, Position.RB, Position.RB,
            Position.CDM, Position.CDM, Position.CM, Position.CM, Position.CM, Position.CAM,
            Position.LM, Position.RM, Position.LW, Position.RW,
            Position.ST, Position.ST, Position.ST,
        };

        private static readonly string[] ClubPatterns = { "{0} FC", "Deportivo {0}", "Atlético {0}", "{0} Sport", "Unión {0}", "Racing {0}", "{0} Athletic", "Club {0}" };

        private static readonly string[] Palette =
        {
            "#C8102E", "#1D428A", "#FFD100", "#00843D", "#5B2C83", "#F57F17", "#212121", "#FFFFFF",
            "#00A3AD", "#8B0000", "#87CEEB", "#2E7D32", "#B0BEC5", "#6D4C41", "#E91E63", "#0D47A1",
        };

        private static readonly string[] Nationalities = NameGenerator.SupportedNationalities.ToArray();

        public static GameWorld Generate(WorldGenerationOptions options)
        {
            var rng = new DeterministicRandom(options.Seed);
            var names = new NameGenerator(rng.Fork(), options.ReservedNames);
            var world = new GameWorld
            {
                CurrentDate = options.SeasonStart.AddDays(-14),
                SeasonYear = options.SeasonStart.Year,
            };

            var rules = options.Rules ?? new CompetitionRules { Id = "solaria-primera" };
            rules.Validate();
            var league = world.AddCompetition(new Competition
            {
                Name = options.LeagueName,
                ShortName = options.LeagueShortName,
                Tier = 1,
                Rules = rules,
                SourcePackId = ContentRegistry.BasePackId,
            });

            for (int i = 0; i < options.ClubCount; i++)
            {
                string city = names.CityName();
                string pattern = ClubPatterns[rng.Range(0, ClubPatterns.Length)];
                int level = options.ClubCount == 1
                    ? options.MaxClubLevel
                    : options.MinClubLevel + (options.MaxClubLevel - options.MinClubLevel) * i / (options.ClubCount - 1);
                level += rng.Range(-1, 2);

                string primary = Palette[rng.Range(0, Palette.Length)];
                string secondary;
                do { secondary = Palette[rng.Range(0, Palette.Length)]; } while (secondary == primary);

                var club = world.AddClub(new Club
                {
                    Name = string.Format(pattern, city),
                    ShortName = city.Substring(0, Math.Min(3, city.Length)).ToUpperInvariant(),
                    City = city,
                    PrimaryColor = primary,
                    SecondaryColor = secondary,
                    Crest = new CrestSpec { Shape = rng.Range(0, 6), Emblem = rng.Range(0, 24), Pattern = rng.Range(0, 8) },
                    Reputation = Math.Max(1, Math.Min(100, (level - 50) * 3 + rng.Range(0, 10))),
                    LeagueId = league.Id,
                    StadiumName = "Estadio de " + city,
                    StadiumCapacity = 8000 + (level - options.MinClubLevel) * 2500 + rng.Range(0, 5000),
                    TransferBudget = (long)Math.Pow(level - 40, 2.6) * 1000,
                    WageBudgetWeekly = (long)Math.Pow(level - 40, 2.2) * 100,
                    Tactics = RandomTactics(rng),
                    SourcePackId = ContentRegistry.BasePackId,
                });
                league.ClubIds.Add(club.Id);

                var usedNumbers = new HashSet<int>();
                foreach (var pos in SquadTemplate)
                {
                    var p = CreateSquadPlayer(pos, level, options.SeasonStart, rng, names);
                    world.AddPlayer(p);
                    world.AssignToClub(p.Id, club.Id);
                    p.ShirtNumber = PickShirtNumber(pos, usedNumbers, rng);
                }
            }

            world.Fixtures.AddRange(FixtureGenerator.Generate(league, options.SeasonStart, () => world.NextFixtureId++));
            return world;
        }

        public static Player CreateSquadPlayer(Position pos, int clubLevel, DateTime refDate, DeterministicRandom rng, NameGenerator names)
        {
            int age = (int)Math.Round(Math.Max(17, Math.Min(35, rng.Gaussian(25.5, 4.2))));
            // Los muy jóvenes y los veteranos suelen estar algo por debajo del nivel del club.
            int ageAdj = age < 21 ? -(21 - age) * 2 : (age > 31 ? -(age - 31) : 0);
            int ovr = (int)Math.Round(clubLevel + ageAdj + rng.Gaussian(0, 3.5));
            ovr = Math.Max(40, Math.Min(90, ovr));
            string nat = Nationalities[rng.Range(0, Nationalities.Length)];
            var (first, last) = names.PlayerName(nat);
            int height = PlayerFactory.HeightFor(pos, rng);
            var build = (Build)rng.Range(0, 3);
            var birth = refDate.AddYears(-age).AddDays(-rng.Range(0, 365));
            var p = new Player
            {
                FirstName = first,
                LastName = last,
                Nationality = nat,
                BirthDate = birth.Date,
                HeightCm = height,
                WeightKg = (int)Math.Round(height - 105 + (build == Build.Stocky ? 6 : build == Build.Lean ? -5 : 0) + rng.Gaussian(0, 3)),
                Build = build,
                PreferredFoot = pos == Position.LB || pos == Position.LM || (pos == Position.RW && rng.Chance(0.5)) || rng.Chance(0.2) ? Foot.Left : Foot.Right,
                WeakFoot = rng.Range(1, 6),
                Position = pos,
                Attributes = PlayerFactory.GenerateAttributes(pos, ovr, rng, height, build),
                Personality = new Personality
                {
                    Professionalism = rng.Range(4, 21),
                    Ambition = rng.Range(4, 21),
                    Temperament = rng.Range(4, 21),
                    Loyalty = rng.Range(4, 21),
                },
                Reputation = Math.Max(1, (ovr - 45) * 2),
                SourcePackId = ContentRegistry.BasePackId,
            };
            p.Potential = PlayerFactory.PotentialFor(p.Overall, age, rng);
            return p;
        }

        private static Tactics RandomTactics(DeterministicRandom rng) => new Tactics
        {
            Formation = (FormationId)rng.Range(0, 4),
            Pressing = rng.Range(30, 80),
            DefensiveLine = rng.Range(30, 75),
            Width = rng.Range(35, 75),
            Tempo = rng.Range(30, 80),
        };

        private static int PickShirtNumber(Position pos, HashSet<int> used, DeterministicRandom rng)
        {
            int[] preferred = pos switch
            {
                Position.GK => new[] { 1, 13, 25 },
                Position.CB => new[] { 4, 5, 3, 15 },
                Position.LB => new[] { 3, 12 },
                Position.RB => new[] { 2, 12 },
                Position.CDM => new[] { 6, 16 },
                Position.CM => new[] { 8, 14, 18 },
                Position.CAM => new[] { 10, 20 },
                Position.LM or Position.LW => new[] { 11, 17 },
                Position.RM or Position.RW => new[] { 7, 19 },
                _ => new[] { 9, 21, 22 },
            };
            foreach (var n in preferred) if (used.Add(n)) return n;
            int k;
            do { k = rng.Range(2, 40); } while (!used.Add(k));
            return k;
        }
    }
}
