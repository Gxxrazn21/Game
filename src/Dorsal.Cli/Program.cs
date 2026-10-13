using System.Diagnostics;
using System.Globalization;
using Dorsal.Core.Domain;
using Dorsal.Core.Match;
using Dorsal.Core.Simulation;
using Dorsal.Core.World;

// Herramienta de consola para ejecutar el núcleo sin Unity: partidos, lotes de ajuste y temporadas.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
var argList = args.ToList();
string cmd = argList.Count > 0 ? argList[0] : "help";

ulong seed = ulong.Parse(Opt("--seed", "1"));
float half = float.Parse(Opt("--half", "270"));
int n = int.Parse(Opt("--n", "100"));
bool verbose = argList.Contains("--verbose");

switch (cmd)
{
    case "match": RunMatch(); break;
    case "batch": RunBatch(); break;
    case "season": RunSeason(); break;
    default:
        Console.WriteLine("Uso: dorsal <match|batch|season> [--seed N] [--half SEGUNDOS] [--n PARTIDOS] [--verbose]");
        break;
}

string Opt(string name, string def)
{
    int i = argList.IndexOf(name);
    return i >= 0 && i + 1 < argList.Count ? argList[i + 1] : def;
}

GameWorld NewWorld() => WorldGenerator.Generate(new WorldGenerationOptions { Seed = seed });

MatchEngine NewEngine(GameWorld world, int homeId, int awayId, ulong matchSeed)
{
    var home = LineupSelector.Select(world.Clubs[homeId], world.SquadOf(homeId));
    var away = LineupSelector.Select(world.Clubs[awayId], world.SquadOf(awayId));
    return new MatchEngine(home, away, new MatchConfig { HalfDurationSeconds = half }, matchSeed);
}

void RunMatch()
{
    var world = NewWorld();
    var fx = world.Fixtures[0];
    var engine = NewEngine(world, fx.HomeClubId, fx.AwayClubId, seed);
    var names = world.Players.Values.ToDictionary(p => p.Id, p => p.DisplayName);
    if (argList.Contains("--trace")) engine.DebugLog = Console.WriteLine;
    var sw = Stopwatch.StartNew();
    var report = engine.RunToEnd();
    sw.Stop();

    string H = world.Clubs[fx.HomeClubId].Name, A = world.Clubs[fx.AwayClubId].Name;
    Console.WriteLine($"{H} {report.HomeGoals} - {report.AwayGoals} {A}   (semilla {seed}, {engine.Tick} pasos, {sw.ElapsedMilliseconds} ms)");
    Console.WriteLine();
    foreach (var e in report.Events)
    {
        bool notable = e.Type is MatchEventType.Goal or MatchEventType.OwnGoal or MatchEventType.YellowCard or MatchEventType.SecondYellow
            or MatchEventType.RedCard or MatchEventType.Penalty or MatchEventType.PenaltyMissed or MatchEventType.Substitution
            or MatchEventType.Injury or MatchEventType.HalfTime or MatchEventType.FullTime or MatchEventType.KickOff;
        if (!notable && !verbose) continue;
        string who = e.PlayerId != 0 && names.TryGetValue(e.PlayerId, out var nm) ? nm : "";
        string other = e.OtherPlayerId != 0 && names.TryGetValue(e.OtherPlayerId, out var on) ? on : "";
        string minute = e.Half == 1 && e.Minute > 45 ? $"45+{e.Minute - 45}" : e.Half == 2 && e.Minute > 90 ? $"90+{e.Minute - 90}" : e.Minute.ToString();
        Console.WriteLine($"{minute,6}'  {e.Type,-14} {(e.Side == TeamSide.Home ? H : A),-22} {who} {(other != "" ? "(" + other + ")" : "")}");
    }
    var dg = engine.Diagnostics;
    Console.WriteLine($"[diag] pases {dg.PassesKicked}: destinatario {dg.PassToIntended}, otro compañero {dg.PassToOtherTeammate}, interceptados {dg.PassIntercepted}, desviados por rival {dg.PassDeflectedByOpponent}, mal control propio {dg.PassControlFailedByTeammate}, fuera {dg.PassOut} | entradas {dg.Tackles} | tiros {dg.Shots} paradas {dg.SavesAssigned} bloqueos {dg.Blocks} goles de tiro {dg.GoalsFromShots}");
    Console.WriteLine();
    Console.WriteLine($"Posesión {report.HomePossession:P0} - {1 - report.HomePossession:P0} | Tiros {report.HomeShots}({report.HomeShotsOnTarget}) - {report.AwayShots}({report.AwayShotsOnTarget}) | Córners {report.HomeCorners}-{report.AwayCorners} | Faltas {report.HomeFouls}-{report.AwayFouls} | Fueras de juego {report.HomeOffsides}-{report.AwayOffsides}");
    Console.WriteLine();
    Console.WriteLine("Jugador                Pos  Min  G  A  Tir Pas(%)   Ent Int Par Nota");
    foreach (var s in report.PlayerStats.OrderBy(s => s.Side).ThenBy(s => s.Started ? 0 : 1))
    {
        Console.WriteLine($"{names[s.PlayerId],-22} {s.Position,-4} {s.MinutesPlayed,3} {s.Goals,2} {s.Assists,2} {s.Shots,4} {s.PassesCompleted,3}/{s.PassesAttempted,-3}({s.PassAccuracy * 100,3:F0}) {s.TacklesWon,3} {s.Interceptions,3} {s.Saves,3} {(s.Rating.HasValue ? s.Rating.Value.ToString("F1") : "-"),4}");
    }
}

void RunBatch()
{
    var world = NewWorld();
    var clubs = world.Clubs.Keys.OrderBy(k => k).ToList();
    var rng = new Dorsal.Core.Random.DeterministicRandom(seed);
    int goals = 0, shots = 0, onTarget = 0, fouls = 0, yellows = 0, reds = 0, corners = 0, offsides = 0, homeWins = 0, draws = 0, pens = 0, subs = 0, injuries = 0, passes = 0, passOk = 0;
    float possSpread = 0f, ratingSum = 0f, energySum = 0f; int ratingCount = 0, energyCount = 0;
    var goalHist = new int[12];
    var sw = Stopwatch.StartNew();
    for (int i = 0; i < n; i++)
    {
        int h = clubs[rng.Range(0, clubs.Count)];
        int a;
        do { a = clubs[rng.Range(0, clubs.Count)]; } while (a == h);
        var eng = NewEngine(world, h, a, rng.NextUInt());
        var r = eng.RunToEnd();
        foreach (var ag in eng.Agents) if (ag.OnPitch && !ag.IsGoalkeeper) { energySum += ag.Energy; energyCount++; }
        int g = r.HomeGoals + r.AwayGoals;
        goals += g;
        goalHist[Math.Min(11, g)]++;
        shots += r.HomeShots + r.AwayShots;
        onTarget += r.HomeShotsOnTarget + r.AwayShotsOnTarget;
        fouls += r.HomeFouls + r.AwayFouls;
        corners += r.HomeCorners + r.AwayCorners;
        offsides += r.HomeOffsides + r.AwayOffsides;
        if (r.HomeGoals > r.AwayGoals) homeWins++; else if (r.HomeGoals == r.AwayGoals) draws++;
        possSpread += Math.Abs(r.HomePossession - 0.5f);
        yellows += r.Events.Count(e => e.Type == MatchEventType.YellowCard);
        reds += r.Events.Count(e => e.Type is MatchEventType.RedCard or MatchEventType.SecondYellow);
        pens += r.Events.Count(e => e.Type == MatchEventType.Penalty);
        subs += r.Events.Count(e => e.Type == MatchEventType.Substitution);
        injuries += r.Events.Count(e => e.Type == MatchEventType.Injury);
        foreach (var s in r.PlayerStats)
        {
            passes += s.PassesAttempted;
            passOk += s.PassesCompleted;
            if (s.Rating.HasValue) { ratingSum += s.Rating.Value; ratingCount++; }
        }
    }
    sw.Stop();
    Console.WriteLine($"Partidos: {n}  |  tiempo medio por partido: {sw.Elapsed.TotalMilliseconds / n:F1} ms (mitades de {half} s)");
    Console.WriteLine($"Goles/partido {goals / (float)n:F2} | Tiros {shots / (float)n:F1} | A puerta {onTarget / (float)n:F1} | Conversión {goals / (float)Math.Max(1, shots):P1}");
    Console.WriteLine($"Pases/partido {passes / (float)n:F0} | Precisión {passOk / (float)Math.Max(1, passes):P1} | Desviación media de posesión {possSpread / n:P1}");
    Console.WriteLine($"Faltas {fouls / (float)n:F1} | Amarillas {yellows / (float)n:F2} | Rojas {reds / (float)n:F2} | Penaltis {pens / (float)n:F2} | Córners {corners / (float)n:F1} | Fueras de juego {offsides / (float)n:F1}");
    Console.WriteLine($"Cambios {subs / (float)n:F1} | Lesiones {injuries / (float)n:F2} | Victoria local {homeWins / (float)n:P0} | Empates {draws / (float)n:P0} | Nota media {ratingSum / Math.Max(1, ratingCount):F2} | Energía final media {energySum / Math.Max(1, energyCount):F2}");
    Console.WriteLine("Distribución de goles por partido: " + string.Join(" ", goalHist.Select((c, i) => $"{(i == 11 ? "11+" : i.ToString())}:{c}")));
}

void RunSeason()
{
    var world = NewWorld();
    var league = world.Competitions[0];
    var rng = new Dorsal.Core.Random.DeterministicRandom(seed);
    var sw = Stopwatch.StartNew();
    foreach (var fx in world.Fixtures.OrderBy(f => f.Round).ThenBy(f => f.Id))
    {
        var r = NewEngine(world, fx.HomeClubId, fx.AwayClubId, rng.NextUInt()).RunToEnd();
        fx.Result = new FixtureResult { HomeGoals = r.HomeGoals, AwayGoals = r.AwayGoals };
    }
    sw.Stop();
    Console.WriteLine($"{league.Name} — {world.Fixtures.Count} partidos simulados con el motor completo en {sw.Elapsed.TotalSeconds:F1} s");
    Console.WriteLine();
    Console.WriteLine(" #  Club                       PJ  G  E  P  GF  GC  DG  Pts  Media");
    int pos = 1;
    foreach (var row in LeagueTable.Compute(league, world.Fixtures))
    {
        var club = world.Clubs[row.ClubId];
        double avg = world.SquadOf(club.Id).OrderByDescending(p => p.Overall).Take(11).Average(p => p.Overall);
        Console.WriteLine($"{pos++,2}  {club.Name,-26} {row.Played,2} {row.Won,2} {row.Drawn,2} {row.Lost,2} {row.GoalsFor,3} {row.GoalsAgainst,3} {row.GoalDifference,3} {row.Points,4}  {avg,5:F1}");
    }
}
