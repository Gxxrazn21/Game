using System.Text;
using Dorsal.Core.Career;
using Dorsal.Core.Domain;
using Dorsal.Core.Match;
using Dorsal.Core.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Dorsal.Core.Tests;

public class SaveSystemTests
{
    private static string Snapshot(CareerSession c) => JsonConvert.SerializeObject(SaveGame.From(c), SaveManager.JsonSettings);

    [Fact]
    public void SaveAndLoadRoundTripsExactly()
    {
        var c = CareerHelpers.Started(21);
        for (int i = 0; i < 3; i++) CareerHelpers.PlayRound(c);
        var mgr = new SaveManager(new MemorySaveStorage());
        mgr.Save("ranura1", SaveGame.From(c));
        var loaded = mgr.Load("ranura1");
        Assert.True(loaded.Success, loaded.Error);
        Assert.False(loaded.UsedBackup);
        var restored = loaded.Game!.ToSession();
        Assert.Equal(Snapshot(c), Snapshot(restored));
        Assert.Equal(c.User.FullName, restored.User.FullName);
    }

    [Fact]
    public void LoadedCareerContinuesExactlyLikeTheOriginal()
    {
        // Reproducibilidad: guardar a mitad y continuar debe dar lo mismo que no haber guardado.
        var original = CareerHelpers.Started(22);
        for (int i = 0; i < 4; i++) CareerHelpers.PlayRound(original);
        var mgr = new SaveManager(new MemorySaveStorage());
        mgr.Save("a", SaveGame.From(original));
        var copy = mgr.Load("a").Game!.ToSession();
        for (int i = 0; i < 5; i++)
        {
            CareerHelpers.PlayRound(original);
            CareerHelpers.PlayRound(copy);
        }
        Assert.Equal(Snapshot(original), Snapshot(copy));
    }

    [Fact]
    public void CorruptedSaveFallsBackToBackup()
    {
        var storage = new MemorySaveStorage();
        var mgr = new SaveManager(storage);
        var c = CareerHelpers.Started(23);
        mgr.Save("s", SaveGame.From(c));                 // primera versión
        CareerHelpers.PlayRound(c);
        mgr.Save("s", SaveGame.From(c));                 // segunda: la primera pasa a .bak
        Assert.True(storage.Exists("s.bak"));

        // Dañar el contenido sin romper el JSON: cambia la suma de comprobación.
        var env = JObject.Parse(Encoding.UTF8.GetString(storage.Files["s.sav"]));
        var payload = Convert.FromBase64String((string)env["Payload"]!);
        payload[payload.Length / 2] ^= 0xFF;
        env["Payload"] = Convert.ToBase64String(payload);
        storage.Files["s.sav"] = Encoding.UTF8.GetBytes(env.ToString());

        var result = mgr.Load("s");
        Assert.True(result.Success);
        Assert.True(result.UsedBackup);
        Assert.Contains("comprobación", result.Error);
        Assert.Equal(0, result.Game!.Career.RoundsPlayed);
    }

    [Fact]
    public void FullyCorruptedSaveFailsWithoutCrashing()
    {
        var storage = new MemorySaveStorage();
        var mgr = new SaveManager(storage);
        mgr.Save("s", SaveGame.From(CareerHelpers.Started(24)));
        storage.Files["s.sav"] = Encoding.UTF8.GetBytes("{ esto no es una partida");
        var r = mgr.Load("s");
        Assert.False(r.Success);
        Assert.NotEmpty(r.Error);
        Assert.False(mgr.Load("inexistente").Success);
    }

    [Fact]
    public void InterruptedWriteDoesNotDestroyPreviousSave()
    {
        var storage = new MemorySaveStorage();
        var mgr = new SaveManager(storage);
        var c = CareerHelpers.Started(25);
        mgr.Save("s", SaveGame.From(c));
        // Simula un corte: quedó un temporal a medio escribir.
        storage.Files["s.tmp"] = Encoding.UTF8.GetBytes("{\"Format\":\"dorsal-save\",\"Payl");
        var r = mgr.Load("s");
        Assert.True(r.Success);
        Assert.False(r.UsedBackup);
    }

    [Fact]
    public void OlderSchemasAreMigrated()
    {
        var storage = new MemorySaveStorage();
        var c = CareerHelpers.Started(26);
        // Guardado "antiguo" (esquema 0) en el que el banco se llamaba de otra forma.
        var old = new SaveManager(storage, schemaVersion: 0);
        old.Save("viejo", SaveGame.From(c));
        var env = JObject.Parse(Encoding.UTF8.GetString(storage.Files["viejo.sav"]));
        Assert.Equal(0, (int)env["SchemaVersion"]!);

        var current = new SaveManager(storage);
        Assert.False(current.Load("viejo").Success); // sin migración registrada no se carga

        current.RegisterMigration(new AddBonusMigration());
        var r = current.Load("viejo");
        Assert.True(r.Success, r.Error);
        Assert.Equal(0, r.MigratedFrom);
        Assert.Equal(c.State.Bank + 1234, r.Game!.Career.Bank);
    }

    private sealed class AddBonusMigration : ISaveMigration
    {
        public int FromVersion => 0;

        public void Migrate(JObject saveGame)
        {
            var career = (JObject)saveGame["Career"]!;
            career["Bank"] = (long)career["Bank"]! + 1234;
        }
    }

    [Fact]
    public void SlotsListExportAndImport()
    {
        var storage = new MemorySaveStorage();
        var mgr = new SaveManager(storage);
        var c = CareerHelpers.Started(27);
        mgr.Save("uno", SaveGame.From(c));
        mgr.Save("dos", SaveGame.From(CareerHelpers.Started(28)));
        var slots = mgr.ListSlots().ToList();
        Assert.Equal(2, slots.Count);
        Assert.Contains(slots, s => s.slot == "uno" && s.header.Summary.PlayerName == c.User.FullName);

        var exported = mgr.Export("uno");
        mgr.Import("tres", exported);
        Assert.True(mgr.Load("tres").Success);
        Assert.Throws<InvalidDataException>(() => mgr.Import("cuatro", Encoding.UTF8.GetBytes("basura")));
        mgr.Delete("dos");
        Assert.False(mgr.Load("dos").Success);
        Assert.Throws<ArgumentException>(() => mgr.Save("../fuera", SaveGame.From(c)));
    }

    [Fact]
    public void FileStorageWorksOnDisk()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dorsal-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var mgr = new SaveManager(new FileSaveStorage(dir));
            var c = CareerHelpers.Started(29);
            mgr.Save("disco", SaveGame.From(c));
            mgr.Save("disco", SaveGame.From(c));
            Assert.True(File.Exists(Path.Combine(dir, "disco.sav")));
            Assert.True(File.Exists(Path.Combine(dir, "disco.bak")));
            Assert.False(File.Exists(Path.Combine(dir, "disco.tmp")));
            Assert.True(mgr.Load("disco").Success);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}

public class GoalkeeperModeTests
{
    /// <summary>Prepara un disparo a puerta contra el portero visitante controlado por el usuario.</summary>
    private static (MatchEngine engine, MatchAgent gk, bool onTarget) ShotAgainstUserKeeper(ulong seed)
    {
        var world = TestWorlds.Small(seed);
        var (home, away) = TestWorlds.Teams(world);
        var engine = new MatchEngine(home, away, TestWorlds.FastConfig(), seed);
        while (engine.Phase != MatchPhase.Playing) engine.Step();
        var gk = engine.Away.Goalkeeper!;
        engine.ControlledPlayerId = gk.PlayerId;
        var shooter = engine.Home.Agents.First(a => a.OnPitch && a.Role == Position.ST);
        int dir = engine.Home.AttackDir;
        gk.Position = new Vec2(dir * (Pitch.HalfLength - 1.5f), 0f);
        foreach (var a in engine.Away.OnPitch.Where(a => a != gk)) a.Position = new Vec2(-dir * 20f, a.Position.Y);
        shooter.Position = new Vec2(dir * (Pitch.HalfLength - 15f), (seed % 2 == 0 ? 1 : -1) * 3f);
        engine.TestGiveBall(shooter);
        engine.Step();
        engine.TestShoot(shooter, 1f);
        return (engine, gk, engine.TestHumanSavePending);
    }

    private static bool Saved(MatchEngine engine, MatchAgent gk, bool dive)
    {
        if (dive)
        {
            // El usuario lee el disparo y se estira hacia el lado correcto.
            var v = engine.Ball.Velocity;
            float t = Math.Abs(v.X) > 0.1f ? (gk.Position.X - engine.Ball.Position.X) / v.X : 0f;
            float yCross = engine.Ball.Position.Y + v.Y * t;
            float side = Math.Sign(yCross - gk.Position.Y);
            engine.Step(new PlayerInput { Action = InputAction.Dive, Move = new Vec2(0f, side) });
        }
        int before = engine.Events.Count;
        for (int i = 0; i < 40; i++) engine.Step(PlayerInput.None);
        return engine.Events.Skip(before).Any(e => e.Type == MatchEventType.ShotSaved && e.PlayerId == gk.PlayerId);
    }

    [Fact]
    public void DivingTheRightWaySavesFarMoreThanStandingStill()
    {
        int diveTrials = 0, diveSaves = 0, standTrials = 0, standSaves = 0;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var (e1, gk1, on1) = ShotAgainstUserKeeper(seed);
            if (on1) { standTrials++; if (Saved(e1, gk1, dive: false)) standSaves++; }
            var (e2, gk2, on2) = ShotAgainstUserKeeper(seed);
            if (on2) { diveTrials++; if (Saved(e2, gk2, dive: true)) diveSaves++; }
        }
        Assert.True(diveTrials >= 15, $"pocos tiros a puerta: {diveTrials}");
        double diveRate = (double)diveSaves / diveTrials, standRate = (double)standSaves / Math.Max(1, standTrials);
        Assert.True(diveRate > standRate + 0.25, $"paradas estirándose {diveRate:P0} vs quieto {standRate:P0}");
    }

    [Fact]
    public void KeeperDiveMovesHimAndThenHeRecovers()
    {
        var (engine, gk, _) = ShotAgainstUserKeeper(3);
        var y0 = gk.Position.Y;
        engine.Step(new PlayerInput { Action = InputAction.Dive, Move = new Vec2(0f, 1f) });
        Assert.True(gk.IsDiving);
        Assert.Equal(AgentAction.Dive, gk.CurrentAction);
        for (int i = 0; i < 8; i++) engine.Step(PlayerInput.None);
        Assert.True(gk.Position.Y - y0 > 1.0f, $"desplazamiento {gk.Position.Y - y0:F2}");
        for (int i = 0; i < 40; i++) engine.Step(PlayerInput.None);
        Assert.False(gk.IsDiving);
    }

    [Fact]
    public void IdleUserKeeperPositionsHimselfAutomatically()
    {
        var world = TestWorlds.Small(4);
        var (home, away) = TestWorlds.Teams(world);
        var engine = new MatchEngine(home, away, TestWorlds.FastConfig(), 4);
        while (engine.Phase != MatchPhase.Playing) engine.Step();
        var gk = engine.Home.Goalkeeper!;
        engine.ControlledPlayerId = gk.PlayerId;
        gk.Position = new Vec2(gk.Position.X, 8f); // fuera de sitio
        for (int i = 0; i < 60; i++) engine.Step(PlayerInput.None);
        Assert.True(Math.Abs(gk.Position.Y) < 6f, $"el portero no volvió a su sitio: y={gk.Position.Y:F1}");
    }

    [Fact]
    public void UserKeeperCanPlayAFullMatch()
    {
        var world = TestWorlds.Small(5);
        var (home, away) = TestWorlds.Teams(world);
        var engine = new MatchEngine(home, away, TestWorlds.FastConfig(), 5);
        engine.ControlledPlayerId = engine.Home.Goalkeeper!.PlayerId;
        int t = 0;
        while (!engine.IsFinished)
        {
            // Usuario que se estira hacia el balón cuando hay un tiro.
            var input = PlayerInput.None;
            if (t++ % 7 == 0 && engine.Ball.Owner == null && engine.Ball.Velocity.Length > 15f)
                input = new PlayerInput { Action = InputAction.Dive, Move = new Vec2(0f, Math.Sign(engine.Ball.Velocity.Y)) };
            engine.Step(input);
        }
        var report = engine.BuildReport();
        Assert.True(report.Finished);
        Assert.Contains(report.PlayerStats, s => s.PlayerId == engine.Home.Goalkeeper!.PlayerId && s.MinutesPlayed == 90);
    }
}
