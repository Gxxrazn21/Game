using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Dorsal.Unity
{
    /// <summary>
    /// Mide el rendimiento real durante el partido (FPS medio, 1 % más lento, mínimo) y lo añade a
    /// "rendimiento.csv" en Application.persistentDataPath junto con el modelo del dispositivo.
    /// Sirve para el informe de rendimiento en dispositivos (criterio de aceptación 14).
    /// </summary>
    public sealed class PerformanceMonitor : MonoBehaviour
    {
        private readonly List<float> _frameTimes = new List<float>(20000);
        private string _label = "";
        private float _start;
        private bool _running;
        private float _smoothed;

        public bool ShowOverlay { get; set; }

        public static string ReportPath => Path.Combine(Application.persistentDataPath, "rendimiento.csv");

        public void Begin(string label)
        {
            _label = label;
            _frameTimes.Clear();
            _start = Time.realtimeSinceStartup;
            _running = true;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _smoothed = Mathf.Lerp(_smoothed <= 0f ? dt : _smoothed, dt, 0.05f);
            if (_running && dt > 0f && dt < 1f) _frameTimes.Add(dt);
        }

        public void End()
        {
            if (!_running) return;
            _running = false;
            if (_frameTimes.Count < 30) return;
            var sorted = new List<float>(_frameTimes);
            sorted.Sort();
            float avgDt = 0f;
            foreach (var f in _frameTimes) avgDt += f;
            avgDt /= _frameTimes.Count;
            int p99 = Mathf.Clamp((int)(sorted.Count * 0.99f), 0, sorted.Count - 1);
            float avgFps = 1f / avgDt, low1 = 1f / sorted[p99], minFps = 1f / sorted[sorted.Count - 1];
            float duration = Time.realtimeSinceStartup - _start;
            try
            {
                bool header = !File.Exists(ReportPath);
                using var w = new StreamWriter(ReportPath, append: true);
                if (header) w.WriteLine("fecha;dispositivo;gpu;so;resolucion;ajustes;duracion_s;fps_medio;fps_1pct;fps_min;fotogramas");
                w.WriteLine(string.Join(";", new[]
                {
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    SystemInfo.deviceModel, SystemInfo.graphicsDeviceName, SystemInfo.operatingSystem,
                    $"{Screen.width}x{Screen.height}", _label,
                    duration.ToString("F0", CultureInfo.InvariantCulture),
                    avgFps.ToString("F1", CultureInfo.InvariantCulture), low1.ToString("F1", CultureInfo.InvariantCulture),
                    minFps.ToString("F1", CultureInfo.InvariantCulture), _frameTimes.Count.ToString(CultureInfo.InvariantCulture),
                }));
            }
            catch (IOException e)
            {
                Debug.LogWarning("No se pudo guardar el informe de rendimiento: " + e.Message);
            }
        }

        private void OnGUI()
        {
            if (!ShowOverlay) return;
            GUI.matrix = Matrix4x4.identity;
            float fps = _smoothed > 0f ? 1f / _smoothed : 0f;
            GUI.Label(new Rect(10, Screen.height - 30, 260, 24), $"FPS {fps:F0}  ({_smoothed * 1000f:F1} ms)");
        }
    }
}
