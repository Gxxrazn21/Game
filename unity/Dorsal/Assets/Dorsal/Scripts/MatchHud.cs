using System.Collections.Generic;
using System.Linq;
using Dorsal.Core.Match;
using UnityEngine;

namespace Dorsal.Unity
{
    /// <summary>Marcador, cronómetro con añadido, avisos de eventos, botones de partido y resumen final.</summary>
    public sealed class MatchHud : MonoBehaviour
    {
        private MatchEngine _engine;
        private MatchController _owner;
        private readonly Dictionary<int, string> _names = new Dictionary<int, string>();
        private readonly Queue<(string text, float until)> _toasts = new Queue<(string, float)>();
        private GUIStyle _scoreStyle, _toastStyle, _benchStyle;

        public void Init(MatchEngine engine, MatchController owner)
        {
            _engine = engine;
            _owner = owner;
            foreach (var a in engine.Agents) _names[a.PlayerId] = a.Player.DisplayName;
            _engine.EventRaised += OnEvent;
        }

        private void OnDestroy()
        {
            if (_engine != null) _engine.EventRaised -= OnEvent;
        }

        private void OnEvent(MatchEvent e)
        {
            string who = e.PlayerId != 0 && _names.TryGetValue(e.PlayerId, out var n) ? n : "";
            bool mine = _engine.ControlledPlayerId.HasValue && e.PlayerId == _engine.ControlledPlayerId.Value;
            string text = e.Type switch
            {
                MatchEventType.Goal => mine ? $"¡GOL TUYO! {who}" : $"¡GOL! {who}",
                MatchEventType.OwnGoal => "Gol en propia puerta",
                MatchEventType.YellowCard => $"Amarilla: {who}",
                MatchEventType.SecondYellow => $"Segunda amarilla: {who}",
                MatchEventType.RedCard => $"Roja directa: {who}",
                MatchEventType.Penalty => "¡Penalti!",
                MatchEventType.PenaltyMissed => $"Penalti fallado: {who}",
                MatchEventType.Offside => $"Fuera de juego: {who}",
                MatchEventType.Injury => $"Lesión: {who}",
                MatchEventType.Substitution => mine ? "¡Sales al campo!" : $"Cambio: entra {who}",
                MatchEventType.ShotSaved => mine ? "¡PARADÓN!" : $"Parada de {who}",
                MatchEventType.HalfTime => "Descanso",
                MatchEventType.FullTime => "Final del partido",
                _ => null,
            };
            if (text == null) return;
            _toasts.Enqueue((text, Time.unscaledTime + 2.6f));
            while (_toasts.Count > 3) _toasts.Dequeue();
        }

        private static string Clock(MatchEngine e)
        {
            float half = e.HalfMinute;
            int baseMin = e.Half == 2 ? 45 : 0;
            if (half >= 45f) return $"{baseMin + 45}+{Mathf.FloorToInt(half - 45f) + 1}'";
            float total = baseMin + half;
            int m = Mathf.FloorToInt(total);
            return $"{m:00}:{Mathf.FloorToInt((total - m) * 60f):00}";
        }

        private void OnGUI()
        {
            if (_engine == null) return;
            UiKit.Begin();
            float w = UiKit.Width, h = UiKit.Height;
            if (_scoreStyle == null)
            {
                // Estilos creados una vez: crear objetos en cada OnGUI genera basura en móvil.
                _scoreStyle = new GUIStyle(UiKit.H2) { alignment = TextAnchor.MiddleCenter, normal = { textColor = UiKit.Text } };
                _toastStyle = new GUIStyle(UiKit.H2) { alignment = TextAnchor.MiddleCenter, normal = { textColor = UiKit.Text } };
                _benchStyle = new GUIStyle(UiKit.MutedLabel) { alignment = TextAnchor.MiddleCenter };
            }

            // Marcador
            string score = $"{Short(_engine.Home.Name)}  {_engine.Home.Goals} - {_engine.Away.Goals}  {Short(_engine.Away.Name)}   {Clock(_engine)}";
            if (_engine.AddedMinutes > 0 && _engine.HalfMinute >= 44f) score += $" (+{_engine.AddedMinutes})";
            GUI.Box(new Rect(w * 0.5f - 300f, 10f, 600f, 46f), GUIContent.none, UiKit.PanelStyle);
            GUI.Label(new Rect(w * 0.5f - 290f, 16f, 580f, 36f), score, _scoreStyle);

            if (_owner.ReplayActive)
            {
                GUI.Label(new Rect(20f, 70f, 300f, 40f), "REPETICIÓN", UiKit.H2);
                if (GUI.Button(new Rect(w - 190f, h - 80f, 170f, 52f), "Saltar", UiKit.Button)) _owner.SkipReplay();
                UiKit.End();
                return;
            }

            // Botones de partido
            float bw = 150f;
            if (GUI.Button(new Rect(w - bw - 10f, 10f, bw, 46f), "Cámara", UiKit.Button)) _owner.CycleCamera();
            if (GUI.Button(new Rect(w - 2 * bw - 16f, 10f, bw, 46f), _owner.Paused ? "Seguir" : "Pausa", UiKit.Button)) _owner.Paused = !_owner.Paused;
            if (GUI.Button(new Rect(10f, 10f, 190f, 46f), _owner.WatchMode ? "Modo: Ver" : "Modo: Jugar", UiKit.Button)) _owner.ToggleWatchMode();
            if (_owner.WatchMode && GUI.Button(new Rect(10f, 62f, 190f, 46f), $"Velocidad x{_owner.SpeedMultiplier}", UiKit.Button)) _owner.CycleSpeed();
            if (!_engine.IsFinished && GUI.Button(new Rect(10f, 114f, 190f, 46f), "Simular final", UiKit.Button)) _owner.SimulateToEnd();

            var me = _engine.ControlledAgent;
            if (me == null && _engine.ControlledPlayerId.HasValue && !_owner.WatchMode && !_engine.IsFinished)
                GUI.Label(new Rect(w * 0.5f - 250f, 62f, 500f, 30f), "En el banquillo: el entrenador decidirá si sales", _benchStyle);

            // Avisos
            float ty = h * 0.16f;
            foreach (var (text, until) in _toasts)
            {
                if (until < Time.unscaledTime) continue;
                GUI.Box(new Rect(w * 0.5f - 220f, ty, 440f, 44f), GUIContent.none, UiKit.Card);
                GUI.Label(new Rect(w * 0.5f - 210f, ty + 6f, 420f, 34f), text, _toastStyle);
                ty += 50f;
            }
            while (_toasts.Count > 0 && _toasts.Peek().until < Time.unscaledTime) _toasts.Dequeue();

            if (_engine.IsFinished) DrawSummary(w, h);
            UiKit.End();
        }

        private void DrawSummary(float w, float h)
        {
            var report = _engine.BuildReport();
            var rect = new Rect(w * 0.5f - 380f, 90f, 760f, h - 120f);
            GUI.Box(rect, GUIContent.none, UiKit.PanelStyle);
            GUILayout.BeginArea(new Rect(rect.x + 20f, rect.y + 16f, rect.width - 40f, rect.height - 32f));
            GUILayout.Label($"{_engine.Home.Name} {report.HomeGoals} - {report.AwayGoals} {_engine.Away.Name}", UiKit.Title);
            GUILayout.Label($"Posesión {report.HomePossession:P0} - {1f - report.HomePossession:P0}   ·   Tiros {report.HomeShots} ({report.HomeShotsOnTarget}) - {report.AwayShots} ({report.AwayShotsOnTarget})", UiKit.MutedLabel);
            var meId = _engine.ControlledPlayerId;
            if (meId.HasValue)
            {
                var mine = report.PlayerStats.FirstOrDefault(s => s.PlayerId == meId.Value);
                if (mine != null)
                {
                    GUILayout.Space(6f);
                    string rating = mine.Rating.HasValue ? mine.Rating.Value.ToString("F1") : "sin nota";
                    string line = mine.Position == Dorsal.Core.Domain.Position.GK
                        ? $"Tu partido: nota {rating} · {mine.MinutesPlayed}' · {mine.Saves} paradas · {mine.GoalsConceded} goles encajados"
                        : $"Tu partido: nota {rating} · {mine.MinutesPlayed}' · {mine.Goals} G · {mine.Assists} A · pases {mine.PassesCompleted}/{mine.PassesAttempted} · tiros {mine.Shots}";
                    GUILayout.Label(line, UiKit.H2);
                }
            }
            GUILayout.Space(8f);
            GUILayout.Label("Mejores notas", UiKit.MutedLabel);
            foreach (var s in report.PlayerStats.Where(s => s.Rating.HasValue).OrderByDescending(s => s.Rating).Take(5))
            {
                string n = _names.TryGetValue(s.PlayerId, out var nm) ? nm : s.PlayerId.ToString();
                GUILayout.Label($"{s.Rating:F1}   {n} ({Dorsal.Core.Localization.Es.Position(s.Position)})  {s.Goals} G  {s.Assists} A", UiKit.Label);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Continuar", UiKit.AccentButton)) _owner.Finish();
            GUILayout.EndArea();
        }

        private static string Short(string name) => name.Length <= 16 ? name : name.Substring(0, 16);
    }
}
