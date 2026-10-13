using Dorsal.Core.Domain;
using Dorsal.Core.Match;
using Dorsal.Core.Random;
using Dorsal.Core.Simulation;
using Dorsal.Core.World;

namespace Dorsal.Core.Tests;

/// <summary>Utilidades compartidas para construir mundos y equipos de prueba.</summary>
internal static class TestWorlds
{
    public static GameWorld Small(ulong seed = 42) =>
        WorldGenerator.Generate(new WorldGenerationOptions { Seed = seed, ClubCount = 6 });

    public static (MatchTeamSetup home, MatchTeamSetup away) Teams(GameWorld world, int homeIndex = 0, int awayIndex = 1)
    {
        var ids = world.Clubs.Keys.OrderBy(k => k).ToList();
        var h = world.Clubs[ids[homeIndex]];
        var a = world.Clubs[ids[awayIndex]];
        return (LineupSelector.Select(h, world.SquadOf(h.Id)), LineupSelector.Select(a, world.SquadOf(a.Id)));
    }

    /// <summary>Equipo sintético en el que todos los jugadores tienen el mismo nivel (para aislar el efecto de los atributos).</summary>
    public static MatchTeamSetup UniformTeam(int clubId, int level, ref int nextPlayerId, ulong seed, FormationId formation = FormationId.F442,
                                             Action<Attributes>? tweakAttackers = null)
    {
        var rng = new DeterministicRandom(seed);
        var f = Formation.Get(formation);
        var setup = new MatchTeamSetup
        {
            ClubId = clubId,
            Name = "Equipo " + clubId,
            Tactics = new Tactics { Formation = formation },
        };
        foreach (var slot in f.Slots)
        {
            var attrs = PlayerFactory.GenerateAttributes(slot.Position, level, rng);
            if (tweakAttackers != null && Positions.LineOf(slot.Position) == PositionLine.Attack) tweakAttackers(attrs);
            setup.Starters.Add(new Player
            {
                Id = nextPlayerId++,
                FirstName = "J",
                LastName = "Jugador" + nextPlayerId,
                Position = slot.Position,
                Attributes = attrs,
                HeightCm = 182,
                BirthDate = new DateTime(2000, 1, 1),
            });
        }
        foreach (var pos in new[] { Position.GK, Position.CB, Position.CM, Position.CM, Position.ST, Position.LW, Position.RB })
        {
            setup.Bench.Add(new Player
            {
                Id = nextPlayerId++,
                LastName = "Suplente" + nextPlayerId,
                Position = pos,
                Attributes = PlayerFactory.GenerateAttributes(pos, level - 3, rng),
                BirthDate = new DateTime(2000, 1, 1),
            });
        }
        return setup;
    }

    public static MatchConfig FastConfig() => new MatchConfig { HalfDurationSeconds = 180f };
}
