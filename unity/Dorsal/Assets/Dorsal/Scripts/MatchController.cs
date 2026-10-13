using System;
using System.Collections.Generic;
using Dorsal.Core.Match;
using UnityEngine;

namespace Dorsal.Unity
{
    /// <summary>
    /// Ejecuta un partido en 3D: avanza el motor en FixedUpdate (paso = TickRate), dibuja con interpolación,
    /// graba fotogramas para las repeticiones, gestiona modos Jugar/Ver, velocidad y simulación rápida.
    /// Al terminar entrega el informe a quien lo creó (modo carrera o partido rápido).
    /// </summary>
    public sealed class MatchController : MonoBehaviour
    {
        private MatchEngine _engine;
        private GameSettings _settings;
        private MatchView _view;
        private CameraRig _rig;
        private TouchControls _controls;
        private MatchHud _hud;
        private PerformanceMonitor _perf;
        private ReplaySystem _replay;
        private MatchFrame _prev, _cur;
        private Action<MatchReport> _onFinished;
        private Func<MatchEngine, bool> _perStep;
        private bool _perStepDone;
        private int? _userPlayerId;
        private float _replayAt = -1f;
        private bool _finishedNotified;

        public MatchEngine Engine => _engine;
        public bool Paused { get; set; }
        public bool WatchMode { get; private set; }
        public int SpeedMultiplier { get; private set; } = 1;
        public bool ReplayActive => _replay != null && _replay.Playing;
        public string Title { get; private set; } = "";

        public static MatchController Create(MatchEngine engine, GameSettings settings, string title, Action<MatchReport> onFinished,
                                             Func<MatchEngine, bool> perStep = null, IReadOnlyDictionary<int, int> skins = null)
        {
            var go = new GameObject("Partido");
            var mc = go.AddComponent<MatchController>();
            mc.Init(engine, settings, title, onFinished, perStep, skins);
            return mc;
        }

        private void Init(MatchEngine engine, GameSettings settings, string title, Action<MatchReport> onFinished,
                          Func<MatchEngine, bool> perStep, IReadOnlyDictionary<int, int> skins)
        {
            _engine = engine;
            _settings = settings;
            _onFinished = onFinished;
            _perStep = perStep;
            _userPlayerId = engine.ControlledPlayerId;
            Title = title;
            Time.fixedDeltaTime = 1f / engine.Config.TickRate;

            var light = FindAnyObjectByType<Light>();
            if (light == null)
            {
                light = new GameObject("Sol").AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
            light.shadows = settings.Quality == 0 ? LightShadows.None : LightShadows.Soft;

            _view = new GameObject("Vista").AddComponent<MatchView>();
            _view.transform.SetParent(transform, false);
            _view.Build(engine, settings.Quality, settings.Stadium, skins);

            var camGo = Camera.main != null ? Camera.main.gameObject : new GameObject("Camara", typeof(Camera));
            camGo.tag = "MainCamera";
            _rig = camGo.GetComponent<CameraRig>();
            if (_rig == null) _rig = camGo.AddComponent<CameraRig>();
            _rig.Init(engine, _view.BallTransform);
            camGo.transform.position = new Vector3(0f, 26f, -Pitch.HalfWidth - 24f);

            _controls = gameObject.AddComponent<TouchControls>();
            _controls.Settings.ButtonSize = settings.ButtonSize;
            _controls.Settings.Opacity = settings.ButtonOpacity;
            _controls.Settings.LeftHanded = settings.LeftHanded;
            _controls.Init(engine, _rig);

            _hud = gameObject.AddComponent<MatchHud>();
            _hud.Init(engine, this);

            _perf = gameObject.AddComponent<PerformanceMonitor>();
            _perf.ShowOverlay = settings.ShowPerformance;
            _perf.Begin($"Calidad {settings.Quality}, {settings.TargetFps} FPS");

            int n = engine.Agents.Count;
            _prev = new MatchFrame(n);
            _cur = new MatchFrame(n);
            _cur.Capture(engine);
            _prev.CopyFrom(_cur);
            _replay = new ReplaySystem(n, engine.Config.TickRate);
            engine.EventRaised += OnEvent;
        }

        private void OnDestroy()
        {
            if (_engine != null) _engine.EventRaised -= OnEvent;
        }

        private void OnEvent(MatchEvent e)
        {
            if (!_settings.AutoReplays || WatchMode && SpeedMultiplier > 1) return;
            bool goal = e.Type == MatchEventType.Goal || e.Type == MatchEventType.OwnGoal;
            bool userSave = e.Type == MatchEventType.ShotSaved && _userPlayerId.HasValue && e.PlayerId == _userPlayerId.Value;
            if (goal || userSave) _replayAt = Time.unscaledTime + (goal ? 1.2f : 0.6f);
        }

        private void FixedUpdate()
        {
            if (_engine == null || Paused || ReplayActive) return;
            if (_engine.IsFinished)
            {
                NotifyFinishedOnce();
                return;
            }
            int steps = WatchMode ? SpeedMultiplier : 1;
            for (int i = 0; i < steps && !_engine.IsFinished; i++)
            {
                _prev.CopyFrom(_cur);
                if (!WatchMode && _engine.ControlledAgent != null) _engine.Step(_controls.ConsumeInput());
                else _engine.Step();
                _cur.Capture(_engine);
                _replay.Record(_engine);
                if (_perStep != null && !_perStepDone) _perStepDone = _perStep(_engine);
            }
        }

        private void Update()
        {
            if (_engine == null) return;
            float dt = Time.unscaledDeltaTime;
            if (_replayAt > 0f && Time.unscaledTime >= _replayAt)
            {
                _replayAt = -1f;
                if (_replay.Start(4.5f)) _rig.ReplayMode = true;
            }
            if (ReplayActive)
            {
                if (_replay.Advance(dt, out var a, out var b, out var t)) _view.Render(a, b, t, dt * _replay.PlaybackSpeed);
                if (!ReplayActive) EndReplay();
                return;
            }
            float alpha = Time.fixedDeltaTime > 0f ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime) : 1f;
            _view.Render(_prev, _cur, alpha, Time.deltaTime);
        }

        public void SkipReplay()
        {
            _replay.Stop();
            EndReplay();
        }

        private void EndReplay()
        {
            _rig.ReplayMode = false;
            _replay.Clear();
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

        /// <summary>Simulación rápida del resto del partido: el mismo motor y la misma semilla, sin gráficos.</summary>
        public void SimulateToEnd()
        {
            _engine.ControlledPlayerId = null;
            while (!_engine.IsFinished)
            {
                _engine.Step();
                if (_perStep != null && !_perStepDone) _perStepDone = _perStep(_engine);
            }
            _cur.Capture(_engine);
            _prev.CopyFrom(_cur);
            _engine.ControlledPlayerId = _userPlayerId;
        }

        private void NotifyFinishedOnce()
        {
            if (_finishedNotified) return;
            _finishedNotified = true;
            _perf.End();
        }

        /// <summary>Lo llama el HUD con el botón "Continuar" tras el resumen.</summary>
        public void Finish()
        {
            var report = _engine.BuildReport();
            var cb = _onFinished;
            _onFinished = null;
            Destroy(gameObject);
            cb?.Invoke(report);
        }
    }
}
