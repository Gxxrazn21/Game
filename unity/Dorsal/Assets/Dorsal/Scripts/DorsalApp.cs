using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dorsal.Core.Career;
using Dorsal.Core.Domain;
using Dorsal.Core.Match;
using Dorsal.Core.Persistence;
using Dorsal.Core.Simulation;
using Dorsal.Core.World;
using UnityEngine;

namespace Dorsal.Unity
{
    public enum AppScreen
    {
        MainMenu,
        NewCareer,
        LoadGame,
        Settings,
        ChooseClub,
        Hub,
        Training,
        Calendar,
        Table,
        Offers,
        Profile,
        History,
        PreMatch,
        PostMatch,
        SeasonEnd,
        Retired,
        InMatch,
    }

    /// <summary>
    /// Aplicación: se crea sola al pulsar Play en cualquier escena (también en la compilación de Android),
    /// gestiona menús, modo carrera, partidos, guardado y ajustes. Toda la lógica del juego vive en Dorsal.Core.
    /// </summary>
    public sealed partial class DorsalApp : MonoBehaviour
    {
        public const string Version = "0.2.0";

        private GameSettings _settings;
        private SaveManager _saves;
        private CareerSession _career;
        private string _slot = "";
        private AppScreen _screen = AppScreen.MainMenu;
        private AppScreen _returnFromSettings = AppScreen.MainMenu;
        private MatchDayInfo _pendingInfo;
        private MatchDaySummary _lastSummary;
        private MatchController _match;
        private string _message = "";
        private float _messageUntil;
        private Vector2 _scroll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindAnyObjectByType<DorsalApp>() != null) return;
            var go = new GameObject("DorsalApp");
            DontDestroyOnLoad(go);
            go.AddComponent<DorsalApp>();
        }

        private void Start()
        {
            _settings = GameSettings.Load();
            _settings.Apply();
            _saves = new SaveManager(new FileSaveStorage(Path.Combine(Application.persistentDataPath, "partidas")), Version);
            ResetCreationForm();
        }

        // ---------------------------------------------------------------- utilidades

        private void Toast(string text, float seconds = 3.5f)
        {
            _message = text;
            _messageUntil = Time.unscaledTime + seconds;
        }

        private void Go(AppScreen s)
        {
            _screen = s;
            _scroll = Vector2.zero;
        }

        private void Autosave()
        {
            if (_career == null || string.IsNullOrEmpty(_slot)) return;
            try
            {
                _saves.Save(_slot, SaveGame.From(_career));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Toast("No se pudo guardar: " + e.Message, 6f);
            }
        }

        private string FirstFreeSlot()
        {
            var used = new HashSet<string>(_saves.ListSlots().Select(s => s.slot));
            for (int i = 1; i <= 20; i++)
            {
                string s = "carrera" + i;
                if (!used.Contains(s)) return s;
            }
            return "carrera" + DateTime.Now.Ticks % 100000;
        }

        private void LoadSlot(string slot)
        {
            var r = _saves.Load(slot);
            if (!r.Success)
            {
                Toast("No se pudo cargar la partida: " + r.Error, 6f);
                return;
            }
            _career = r.Game.ToSession();
            _slot = slot;
            if (r.UsedBackup) Toast("La partida estaba dañada: se ha recuperado la copia de seguridad anterior.", 6f);
            GoToCareerHome();
        }

        /// <summary>Pantalla adecuada según la fase de la carrera.</summary>
        private void GoToCareerHome()
        {
            switch (_career.Phase)
            {
                case CareerPhase.ChoosingClub:
                case CareerPhase.FreeAgent:
                    Go(AppScreen.ChooseClub);
                    break;
                case CareerPhase.MatchDay:
                    _pendingInfo = new MatchDayInfo
                    {
                        Fixture = _career.State.PendingFixtureId.HasValue ? _career.World.Fixtures.First(f => f.Id == _career.State.PendingFixtureId.Value) : null,
                        Role = _career.State.PendingRole,
                        Objective = _career.State.PendingObjective,
                        Instruction = _career.State.PendingInstruction,
                    };
                    Go(AppScreen.PreMatch);
                    break;
                case CareerPhase.SeasonEnded:
                    Go(AppScreen.SeasonEnd);
                    break;
                case CareerPhase.Retired:
                    Go(AppScreen.Retired);
                    break;
                default:
                    Go(AppScreen.Hub);
                    break;
            }
        }

        // ---------------------------------------------------------------- partidos

        private void StartUserMatch(bool watch)
        {
            var engine = _career.CreateUserMatchEngine(new MatchConfig { HalfDurationSeconds = _settings.HalfMinutes * 60f, TickRate = 50 });
            if (engine == null) return;
            var skins = new Dictionary<int, int> { [_career.User.Id] = _career.User.Appearance.SkinTone };
            var f = _pendingInfo?.Fixture;
            string title = f != null ? $"{_career.World.Clubs[f.HomeClubId].Name} - {_career.World.Clubs[f.AwayClubId].Name}" : "Partido";
            Go(AppScreen.InMatch);
            _match = MatchController.Create(engine, _settings, title, report =>
            {
                _match = null;
                _lastSummary = _career.CompleteMatchDay(report);
                Autosave();
                Go(AppScreen.PostMatch);
            }, e => _career.MaybeBringOnUser(e), skins);
            if (watch) _match.ToggleWatchMode();
        }

        private void SimulateUserMatch()
        {
            _lastSummary = _career.SimulateUserMatch();
            Autosave();
            Go(AppScreen.PostMatch);
        }

        /// <summary>Partido amistoso con equipos del mundo ficticio (como delantero o como portero).</summary>
        private void StartQuickMatch(bool keeper)
        {
            int seed = (int)(DateTime.Now.Ticks % 100000);
            var world = WorldGenerator.Generate(new WorldGenerationOptions { Seed = (ulong)seed, GenerateContracts = false });
            var fx = world.Fixtures.OrderBy(x => x.Round).First();
            var home = LineupSelector.Select(world.Clubs[fx.HomeClubId], world.SquadOf(fx.HomeClubId));
            var away = LineupSelector.Select(world.Clubs[fx.AwayClubId], world.SquadOf(fx.AwayClubId));
            var engine = new MatchEngine(home, away, new MatchConfig { HalfDurationSeconds = _settings.HalfMinutes * 60f, TickRate = 50 }, (ulong)seed);
            var user = keeper ? home.Starters[0] : home.Starters.FirstOrDefault(p => p.Position == Position.ST) ?? home.Starters[10];
            engine.ControlledPlayerId = user.Id;
            Go(AppScreen.InMatch);
            _match = MatchController.Create(engine, _settings, keeper ? "Desafío portero" : "Partido rápido", _ =>
            {
                _match = null;
                Go(AppScreen.MainMenu);
            });
        }

        private void OnGUI()
        {
            if (_screen == AppScreen.InMatch) return;
            UiKit.Begin();
            UiKit.Background();
            switch (_screen)
            {
                case AppScreen.MainMenu: DrawMainMenu(); break;
                case AppScreen.NewCareer: DrawNewCareer(); break;
                case AppScreen.LoadGame: DrawLoadGame(); break;
                case AppScreen.Settings: DrawSettings(); break;
                case AppScreen.ChooseClub: DrawChooseClub(); break;
                case AppScreen.Hub: DrawHub(); break;
                case AppScreen.Training: DrawTraining(); break;
                case AppScreen.Calendar: DrawCalendar(); break;
                case AppScreen.Table: DrawTable(); break;
                case AppScreen.Offers: DrawOffers(); break;
                case AppScreen.Profile: DrawProfile(); break;
                case AppScreen.History: DrawHistory(); break;
                case AppScreen.PreMatch: DrawPreMatch(); break;
                case AppScreen.PostMatch: DrawPostMatch(); break;
                case AppScreen.SeasonEnd: DrawSeasonEnd(); break;
                case AppScreen.Retired: DrawRetired(); break;
            }
            if (!string.IsNullOrEmpty(_message) && Time.unscaledTime < _messageUntil)
            {
                var r = new Rect(UiKit.Width * 0.5f - 360f, UiKit.Height - 90f, 720f, 64f);
                GUI.Box(r, GUIContent.none, UiKit.Card);
                GUI.Label(new Rect(r.x + 14f, r.y + 8f, r.width - 28f, r.height - 16f), _message, UiKit.Label);
            }
            UiKit.End();
        }
    }
}
