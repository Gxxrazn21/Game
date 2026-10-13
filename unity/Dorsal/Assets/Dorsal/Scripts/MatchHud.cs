using System.Collections.Generic;
using System.Linq;
using Dorsal.Core.Match;
using UnityEngine;

namespace Dorsal.Unity
{
    /// <summary>Marcador, cronómetro, avisos de eventos y resumen final (IMGUI; se sustituirá por UI Toolkit en M6).</summary>
    public sealed class MatchHud : MonoBehaviour
    {
        private MatchEngine _engine;
        private DorsalBootstrap _owner;
        private Dictionary<int, string> _names = new Dictionary<int, string>();
        private readonly Queue<(string text, float until)> _toasts = new Queue<(string, float)>();
        private GUIStyle _big, _small, _toast;

        public void Init(MatchEngine engine, DorsalBootstrap owner, Dictionary<int, string> names)
        {
            _engine = engine;
            _owner = owner;
            _names = names;
            _engine.EventRaised += OnEvent;
        }

        private void OnDestroy()
        {
            if (_engine != null) _engine.EventRaised -= OnEvent;
        }

        private void OnEvent(MatchEvent e)
        {
            string who = e.PlayerId != 0 && _names.TryGetValue(e.PlayerId, out var n) ? n : "";
            string text = e.Type switch
            {
                MatchEventType.Goal => $"¡GOL! {who}",
                MatchEventType.OwnGoal => "Gol en propia puerta",
                MatchEventType.YellowCard => $"Amarilla: {who}",
                MatchEventType.SecondYellow => $"Segunda amarilla: {who}",
                MatchEventType.RedCard => $"Roja directa: {who}",
                MatchEventType.Penalty => "¡Penalti!",
                MatchEventType.PenaltyMissed => $"Penalti fallado: {who}",
                MatchEventType.Offside => $"Fuera de juego: {who}",
                MatchEventType.Injury => $"Lesión: {who}",
                MatchEventType.Substitution => $"Cambio: entra {who}",
                MatchEventType.ShotSaved => $"Parada de {who}",
                MatchEventType.HalfTime => "Descanso",
                MatchEventType.FullTime => "Final del partido",
                _ => null,
            };
            if (text == null) return;
            _toasts.Enqueue((text, Time.unscaledTime + 2.5f));
            while (_toasts.Count > 3) _toasts.Dequeue();
        }

        private void EnsureStyles()
        {
            if (_big != null) return;
            _big = new GUIStyle(GUI.skin.box) { fontSize = Mathf.RoundToInt(Screen.height * 0.035f), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _small = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(Screen.height * 0.022f) };
            _toast = new GUIStyle(GUI.skin.box) { fontSize = Mathf.RoundToInt(Screen.height * 0.028f), alignment = TextAnchor.MiddleCenter };
        }

        private static string Clock(MatchEngine e)
        {
            float half = e.HalfMinute;
            int baseMin = e.Half == 2 ? 45 : 0;
            if (half >= 45f)
            {
                int extra = Mathf.FloorToInt(half - 45f);
                return $"{baseMin + 45}+{extra + 1}'";
            }
            float total = baseMin + half;
            int m = Mathf.FloorToInt(total);
            int s = Mathf.FloorToInt((total - m) * 60f);
            return $"{m:00}:{s:00}";
        }

        private void OnGUI()
        {
            if (_engine == null) return;
            EnsureStyles();
            float w = Screen.width, h = Screen.height;

            // Marcador
            string home = _engine.Home.Name, away = _engine.Away.Name;
            string score = $"{Short(home)}  {_engine.Home.Goals} - {_engine.Away.Goals}  {Short(away)}   {Clock(_engine)}";
            if (_engine.AddedMinutes > 0 && _engine.HalfMinute >= 44f) score += $"  (+{_engine.AddedMinutes})";
            GUI.Box(new Rect(w * 0.5f - w * 0.22f, h * 0.015f, w * 0.44f, h * 0.07f), score, _big);

            // Botones de partido (arriba a la derecha).
            float bw = w * 0.11f, bh = h * 0.06f, x = w - bw - 8f, y = 8f;
            if (GUI.Button(new Rect(x, y, bw, bh), "Cámara", _small)) _owner.CycleCamera();
            if (GUI.Button(new Rect(x - bw - 6f, y, bw, bh), _owner.Paused ? "Seguir" : "Pausa", _small)) _owner.Paused = !_owner.Paused;
            if (GUI.Button(new Rect(8f, y, bw * 1.3f, bh), _owner.WatchMode ? "Modo: Ver" : "Modo: Jugar", _small)) _owner.ToggleWatchMode();
            if (_owner.WatchMode && GUI.Button(new Rect(8f, y + bh + 6f, bw * 1.3f, bh), $"Velocidad x{_owner.SpeedMultiplier}", _small)) _owner.CycleSpeed();
            if (GUI.Button(new Rect(8f, y + (bh + 6f) * 2f, bw * 1.3f, bh), "Simular final", _small)) _owner.SimulateToEnd();

            // Avisos
            float ty = h * 0.1f;
            foreach (var (text, until) in _toasts)
            {
                if (until < Time.unscaledTime) continue;
                GUI.Box(new Rect(w * 0.5f - w * 0.18f, ty, w * 0.36f, h * 0.05f), text, _toast);
                ty += h * 0.055f;
            }
            while (_toasts.Count > 0 && _toasts.Peek().until < Time.unscaledTime) _toasts.Dequeue();

            if (_engine.IsFinished) DrawSummary(w, h);
        }

        private void DrawSummary(float w, float h)
        {
            var report = _engine.BuildReport();
            var rect = new Rect(w * 0.15f, h * 0.15f, w * 0.7f, h * 0.72f);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, rect.height - 24f));
            GUILayout.Label($"<b>{_engine.Home.Name} {report.HomeGoals} - {report.AwayGoals} {_engine.Away.Name}</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = Mathf.RoundToInt(h * 0.035f) });
            GUILayout.Label($"Posesión {report.HomePossession:P0} - {1f - report.HomePossession:P0}   Tiros {report.HomeShots} ({report.HomeShotsOnTarget}) - {report.AwayShots} ({report.AwayShotsOnTarget})");
            var me = _engine.ControlledPlayerId;
            if (me.HasValue)
            {
                var mine = report.PlayerStats.FirstOrDefault(s => s.PlayerId == me.Value);
                if (mine != null)
                {
                    string rating = mine.Rating.HasValue ? mine.Rating.Value.ToString("F1") : "sin nota";
                    GUILayout.Label($"Tu actuación: nota {rating} · {mine.MinutesPlayed}' · {mine.Goals} goles · {mine.Assists} asist. · pases {mine.PassesCompleted}/{mine.PassesAttempted} · tiros {mine.Shots}");
                }
            }
            GUILayout.Space(8f);
            foreach (var s in report.PlayerStats.Where(s => s.Rating.HasValue).OrderByDescending(s => s.Rating).Take(6))
            {
                string n = _names.TryGetValue(s.PlayerId, out var nm) ? nm : s.PlayerId.ToString();
                GUILayout.Label($"{s.Rating:F1}  {n} ({s.Position})  {s.Goals}G {s.Assists}A");
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Nuevo partido", _small, GUILayout.Height(h * 0.07f))) _owner.Restart();
            GUILayout.EndArea();
        }

        private static string Short(string name) => name.Length <= 14 ? name : name.Substring(0, 14);
    }
}
