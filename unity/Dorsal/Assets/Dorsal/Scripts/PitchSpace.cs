using Dorsal.Core.Match;
using UnityEngine;

namespace Dorsal.Unity
{
    /// <summary>
    /// Conversión entre el espacio del motor (X largo, Y ancho, Z altura) y el de Unity (X, Y arriba, Z).
    /// </summary>
    public static class PitchSpace
    {
        public static Vector3 ToUnity(Vec2 p, float height = 0f) => new Vector3(p.X, height, p.Y);

        public static Vector3 ToUnity(Vec3 p) => new Vector3(p.X, p.Z, p.Y);

        public static Vec2 ToEngine(Vector3 v) => new Vec2(v.x, v.z);

        public static Color ParseColor(string hex, Color fallback) =>
            ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;

        /// <summary>Distancia perceptual simple entre colores, para evitar que los dos equipos vistan igual.</summary>
        public static float ColorDistance(Color a, Color b)
        {
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return Mathf.Sqrt(dr * dr * 0.3f + dg * dg * 0.59f + db * db * 0.11f);
        }
    }
}
