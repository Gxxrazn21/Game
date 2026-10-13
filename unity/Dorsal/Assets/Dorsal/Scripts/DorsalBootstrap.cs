using System.Linq;
using Dorsal.Core.Domain;
using Dorsal.Core.Match;
using Dorsal.Core.Simulation;
using Dorsal.Core.World;
using UnityEngine;

namespace Dorsal.Unity
{
    public enum QualityPreset
    {
        Low,
        Medium,
        High,
    }

    /// <summary>
    /// Punto de entrada del prototipo M2. Se crea solo al pulsar Play en cualquier escena (incluida la escena vacía
    /// por defecto), genera el mundo ficticio, prepara un partido y conecta motor, vista, cámara, controles y HUD.
    /// El motor avanza en FixedUpdate con el mismo paso que su TickRate.
    /// </summary>
    public sealed class DorsalBootstrap : MonoBehaviour
    {
        public ulong Seed = 1;
        public int TickRate = 50;
        public float HalfDurationSeconds = 240f;
        public int TargetFps = 30;
        public QualityPreset Quality = QualityPreset.Medium;

        private MatchEngine _engine;
        private MatchView _view;
        private CameraRig _rig;
        private TouchControls _controls;
        private MatchHud _hud;
        private GameObject _matchRoot;
        private int _userPlayerId;

        public bool Paused { get; set; }
        public bool WatchMode { get; private set; }
        public int SpeedMultiplier { get; private set; } = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindAnyObjectByType<DorsalBootstrap>() != null) return;
            var go = new GameObject("DorsalBootstrap");
            go.AddComponent<DorsalBootstrap>();
        }

        private void Start()
        {
            ApplyPerformanceSettings();
            StartMatch();
        }

        /// <summary>Límite de FPS y nivel de calidad. Las cifras reales deben medirse en dispositivo (M2/M7).</summary>
        public void ApplyPerformanceSettings()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFps;
            int levels = QualitySettings.names.Length;
            if (levels > 0)
            {
                int idx = Quality == QualityPreset.Low ? 0 : Quality == QualityPreset.Medium ? levels / 2 : levels - 1;
                QualitySettings.SetQualityLevel(idx, true);
            }
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Time.fixedDeltaTime = 1f / TickRate;
        }

        private void StartMatch()
        {
            if (_matchRoot != null) Destroy(_matchRoot);
            _matchRoot = new GameObject("Partido");

            var world = WorldGenerator.Generate(new WorldGenerationOptions { Seed = Seed });
            var fixture = world.Fixtures.OrderBy(f => f.Round).ThenBy(f => f.Id).First();
            var homeClub = world.Clubs[fixture.HomeClubId];
            var awayClub = world.Clubs[fixture.AwayClubId];
            var home = LineupSelector.Select(homeClub, world.SquadOf(homeClub.Id));
            var away = LineupSelector.Select(awayClub, world.SquadOf(awayClub.Id));

            // Demostración: el usuario controla al delantero centro local. En el modo carrera (M4)
            // será el futbolista creado por el usuario, titular o suplente según el entrenador.
            var userPlayer = home.Starters.FirstOrDefault(p => p.Position == Position.ST) ?? home.Starters[home.Starters.Count - 1];
            _userPlayerId = userPlayer.Id;

            _engine = new MatchEngine(home, away, new MatchConfig { TickRate = TickRate, HalfDurationSeconds = HalfDurationSeconds }, Seed);
            _engine.ControlledPlayerId = WatchMode ? (int?)null : _userPlayerId;

            var existingLight = FindAnyObjectByType<Light>();
            if (existingLight == null || existingLight.transform.IsChildOf(_matchRoot.transform))
            {
                var light = new GameObject("Sol").AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                light.transform.SetParent(_matchRoot.transform, false);
                existingLight = light;
            }
            existingLight.shadows = Quality == QualityPreset.Low ? LightShadows.None : LightShadows.Soft;

            _view = new GameObject("Vista").AddComponent<MatchView>();
            _view.transform.SetParent(_matchRoot.transform, false);
            _view.Build(_engine);
            _view.SnapAll();

            var camGo = Camera.main != null ? Camera.main.gameObject : new GameObject("Camara", typeof(Camera));
            camGo.tag = "MainCamera";
            // Comparación explícita: el operador ?? no respeta el null "falso" de Unity.
            _rig = camGo.GetComponent<CameraRig>();
            if (_rig == null) _rig = camGo.AddComponent<CameraRig>();
            _rig.Init(_engine, _view.BallTransform);
            camGo.transform.position = new Vector3(0f, 26f, -Pitch.HalfWidth - 24f);

            _controls = _matchRoot.AddComponent<TouchControls>();
            _controls.Init(_engine, _rig);
            _controls.Visible = !WatchMode;

            _hud = _matchRoot.AddComponent<MatchHud>();
            var names = home.Starters.Concat(home.Bench).Concat(away.Starters).Concat(away.Bench).ToDictionary(p => p.Id, p => p.DisplayName);
            _hud.Init(_engine, this, names);
        }

        private void FixedUpdate()
        {
            if (_engine == null || _engine.IsFinished || Paused) return;
            int steps = WatchMode ? SpeedMultiplier : 1;
            for (int i = 0; i < steps && !_engine.IsFinished; i++)
            {
                _view.CaptureBeforeStep();
                if (_engine.ControlledAgent != null) _engine.Step(_controls.ConsumeInput());
                else _engine.Step();
                _view.CaptureAfterStep();
            }
        }

        public void CycleCamera() => _rig.Cycle();

        public void ToggleWatchMode()
        {
            WatchMode = !WatchMode;
            _engine.ControlledPlayerId = WatchMode ? (int?)null : _userPlayerId;
            _controls.Visible = !WatchMode;
            if (!WatchMode) SpeedMultiplier = 1;
        }

        public void CycleSpeed() => SpeedMultiplier = SpeedMultiplier >= 8 ? 1 : SpeedMultiplier * 2;

        public void SimulateToEnd()
        {
            // Simulación rápida: sin gráficos, el mismo motor y la misma semilla.
            while (!_engine.IsFinished) _engine.Step();
            _view.SnapAll();
        }

        public void Restart()
        {
            Seed++;
            StartMatch();
        }
    }
}
