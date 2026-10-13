using System.Collections.Generic;
using Dorsal.Core.Match;
using UnityEngine;

namespace Dorsal.Unity
{
    /// <summary>
    /// Representación 3D del partido: campo, porterías, estadio, futbolistas humanoides y balón.
    /// Solo dibuja fotogramas (<see cref="MatchFrame"/>): en directo interpola entre pasos del motor y
    /// en las repeticiones reproduce los fotogramas grabados.
    /// </summary>
    public sealed class MatchView : MonoBehaviour
    {
        private MatchEngine _engine;
        private readonly List<PlayerFigure> _figures = new List<PlayerFigure>();
        private Transform _ball;
        private Transform _marker;
        private Vector3 _lastBallPos;
        private int _controlledIndex = -1;

        public Transform BallTransform => _ball;

        public void Build(MatchEngine engine, int quality, bool stadium, IReadOnlyDictionary<int, int> skinByPlayer = null)
        {
            _engine = engine;
            PitchBuilder.Build(transform);
            var homeColor = PitchSpace.ParseColor(engine.Home.Setup.PrimaryColor, Color.red);
            var awayColor = PitchSpace.ParseColor(engine.Away.Setup.PrimaryColor, Color.blue);
            var homeShorts = PitchSpace.ParseColor(engine.Home.Setup.SecondaryColor, Color.white);
            var awayShorts = PitchSpace.ParseColor(engine.Away.Setup.SecondaryColor, Color.white);
            if (PitchSpace.ColorDistance(homeColor, awayColor) < 0.25f)
            {
                // Choque de colores: el visitante juega con su segunda equipación.
                var tmp = awayColor;
                awayColor = awayShorts;
                awayShorts = tmp;
                if (PitchSpace.ColorDistance(homeColor, awayColor) < 0.25f) awayColor = Color.white;
            }
            if (stadium) StadiumBuilder.Build(transform, homeColor, engine.Home.ClubId * 7919 + engine.Away.ClubId, quality);

            foreach (var agent in engine.Agents)
            {
                bool home = agent.Team.Side == TeamSide.Home;
                bool keeper = agent.Player.Position == Dorsal.Core.Domain.Position.GK;
                var shirt = keeper ? (home ? new Color(0.95f, 0.85f, 0.15f) : new Color(0.2f, 0.85f, 0.45f)) : (home ? homeColor : awayColor);
                var shorts = keeper ? new Color(0.12f, 0.12f, 0.14f) : (home ? homeShorts : awayShorts);
                int skin = skinByPlayer != null && skinByPlayer.TryGetValue(agent.PlayerId, out var sk) ? sk : Hash(agent.PlayerId) % MaterialCache.SkinTones.Length;
                var look = new FigureLook
                {
                    Shirt = shirt,
                    Shorts = shorts,
                    Socks = shirt,
                    Skin = MaterialCache.SkinTones[Mathf.Clamp(skin, 0, MaterialCache.SkinTones.Length - 1)],
                    Hair = MaterialCache.HairColors[Hash(agent.PlayerId * 31) % MaterialCache.HairColors.Length],
                    Number = agent.Player.ShirtNumber ?? 0,
                    HeightScale = agent.Player.HeightCm / 180f,
                    Keeper = keeper,
                };
                var fig = PlayerFigure.Create(transform, look, agent.Player.DisplayName);
                fig.gameObject.SetActive(agent.OnPitch);
                _figures.Add(fig);
            }

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Balon";
            Destroy(ball.GetComponent<Collider>());
            ball.transform.SetParent(transform, false);
            ball.transform.localScale = Vector3.one * (Pitch.BallRadius * 2.6f); // algo mayor para verlo en móvil
            ball.GetComponent<Renderer>().sharedMaterial = MaterialCache.Get(Color.white);
            // Paneles oscuros para que se aprecie el giro.
            for (int i = 0; i < 3; i++)
            {
                var patch = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(patch.GetComponent<Collider>());
                patch.transform.SetParent(ball.transform, false);
                patch.transform.localPosition = Quaternion.Euler(i * 70f, i * 120f, 0f) * Vector3.forward * 0.42f;
                patch.transform.localScale = Vector3.one * 0.3f;
                patch.GetComponent<Renderer>().sharedMaterial = MaterialCache.Get(new Color(0.12f, 0.12f, 0.15f));
            }
            _ball = ball.transform;

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "MarcadorJugador";
            Destroy(marker.GetComponent<Collider>());
            marker.transform.SetParent(transform, false);
            marker.transform.localScale = new Vector3(1.3f, 0.008f, 1.3f);
            marker.GetComponent<Renderer>().sharedMaterial = MaterialCache.Get(UiKit.Accent);
            _marker = marker.transform;
        }

        /// <summary>Dibuja la interpolación entre dos fotogramas.</summary>
        public void Render(MatchFrame a, MatchFrame b, float t, float dt)
        {
            var controlled = _engine.ControlledAgent;
            _controlledIndex = controlled != null ? controlled.Index : -1;
            for (int i = 0; i < _figures.Count; i++)
            {
                var fig = _figures[i];
                bool visible = b.OnPitch[i];
                if (fig.gameObject.activeSelf != visible) fig.gameObject.SetActive(visible);
                if (!visible) continue;
                // Los saltos grandes (colocación de un saque) no se interpolan.
                var pos = (b.Pos[i] - a.Pos[i]).sqrMagnitude > 25f ? b.Pos[i] : Vector3.Lerp(a.Pos[i], b.Pos[i], t);
                fig.transform.localPosition = pos;
                fig.transform.localRotation = Quaternion.Slerp(a.Rot[i], b.Rot[i], t);
                fig.Animate(dt, Mathf.Lerp(a.Speed[i], b.Speed[i], t), b.Action[i], Mathf.Lerp(a.ActionTime[i], b.ActionTime[i], t), b.DiveTime[i], b.DiveSide[i]);
                if (i == _controlledIndex) _marker.localPosition = new Vector3(pos.x, 0.02f, pos.z);
            }
            _marker.gameObject.SetActive(_controlledIndex >= 0);

            var ballPos = (b.Ball - a.Ball).sqrMagnitude > 25f ? b.Ball : Vector3.Lerp(a.Ball, b.Ball, t);
            ballPos.y = Mathf.Max(ballPos.y, Pitch.BallRadius * 1.3f);
            var delta = ballPos - _lastBallPos;
            delta.y = 0f;
            if (delta.sqrMagnitude > 1e-6f && delta.sqrMagnitude < 25f)
            {
                // Rodadura: gira según la distancia recorrida.
                var axis = Vector3.Cross(Vector3.up, delta.normalized);
                _ball.Rotate(axis, delta.magnitude / (Pitch.BallRadius * 1.3f) * Mathf.Rad2Deg, Space.World);
            }
            _ball.localPosition = ballPos;
            _lastBallPos = ballPos;
        }

        private static int Hash(int x)
        {
            unchecked
            {
                uint h = (uint)x * 2654435761u;
                return (int)(h >> 8) & 0x7FFFFF;
            }
        }
    }

    /// <summary>Campo reglamentario: césped con franjas, líneas y porterías con red.</summary>
    public static class PitchBuilder
    {
        public static void Build(Transform parent)
        {
            var root = new GameObject("Campo").transform;
            root.SetParent(parent, false);
            var grass = GameObject.CreatePrimitive(PrimitiveType.Plane);
            grass.name = "Cesped";
            Object.Destroy(grass.GetComponent<Collider>());
            grass.transform.SetParent(root, false);
            grass.transform.localScale = new Vector3(13f, 1f, 9f);
            grass.GetComponent<Renderer>().sharedMaterial = MaterialCache.Get(new Color(0.16f, 0.45f, 0.18f));

            for (int i = 0; i < 10; i++)
            {
                if (i % 2 == 0) continue;
                var stripe = Slab(root, "Franja", new Vector3(-Pitch.HalfLength + 10.5f * i + 5.25f, 0.01f, 0f), new Vector3(10.5f, 1f, Pitch.HalfWidth * 2f));
                stripe.GetComponent<Renderer>().sharedMaterial = MaterialCache.Get(new Color(0.18f, 0.5f, 0.2f));
            }

            float w = 0.15f, L = Pitch.HalfLength, W = Pitch.HalfWidth;
            Line(root, new Vector3(0f, 0f, W), new Vector3(2 * L, 1, w));
            Line(root, new Vector3(0f, 0f, -W), new Vector3(2 * L, 1, w));
            Line(root, new Vector3(L, 0f, 0f), new Vector3(w, 1, 2 * W));
            Line(root, new Vector3(-L, 0f, 0f), new Vector3(w, 1, 2 * W));
            Line(root, Vector3.zero, new Vector3(w, 1, 2 * W));
            foreach (int s in new[] { 1, -1 })
            {
                float pa = Pitch.PenaltyAreaDepth, pw = Pitch.PenaltyAreaHalfWidth;
                Line(root, new Vector3(s * (L - pa), 0f, 0f), new Vector3(w, 1, 2 * pw));
                Line(root, new Vector3(s * (L - pa / 2f), 0f, pw), new Vector3(pa, 1, w));
                Line(root, new Vector3(s * (L - pa / 2f), 0f, -pw), new Vector3(pa, 1, w));
                float ga = Pitch.GoalAreaDepth, gw = 9.16f;
                Line(root, new Vector3(s * (L - ga), 0f, 0f), new Vector3(w, 1, 2 * gw));
                Line(root, new Vector3(s * (L - ga / 2f), 0f, gw), new Vector3(ga, 1, w));
                Line(root, new Vector3(s * (L - ga / 2f), 0f, -gw), new Vector3(ga, 1, w));
                Line(root, new Vector3(s * (L - Pitch.PenaltySpotDistance), 0f, 0f), new Vector3(0.3f, 1, 0.3f));
                Goal(root, s);
            }
            const int segments = 48;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                var p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * Pitch.CenterCircleRadius;
                var p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * Pitch.CenterCircleRadius;
                var seg = Line(root, (p0 + p1) / 2f, new Vector3((p1 - p0).magnitude, 1, w));
                seg.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(p1 - p0, Vector3.up).normalized, Vector3.up);
            }
        }

        private static GameObject Slab(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = new Vector3(size.x, 0.01f, size.z);
            return go;
        }

        private static GameObject Line(Transform parent, Vector3 center, Vector3 size)
        {
            // Separación vertical suficiente para evitar el parpadeo (z-fighting) con el césped a distancia.
            var go = Slab(parent, "Linea", center + new Vector3(0f, 0.03f, 0f), size);
            go.GetComponent<Renderer>().sharedMaterial = MaterialCache.Get(Color.white);
            return go;
        }

        private static void Goal(Transform parent, int sign)
        {
            float x = sign * (Pitch.HalfLength + 0.06f);
            float h = Pitch.GoalHeight, hw = Pitch.GoalHalfWidth, depth = 1.8f;
            var white = MaterialCache.Get(Color.white);
            var net = MaterialCache.Get(new Color(0.88f, 0.88f, 0.9f));
            Cyl(parent, new Vector3(x, h / 2f, hw), new Vector3(0.12f, h / 2f, 0.12f), Quaternion.identity, white);
            Cyl(parent, new Vector3(x, h / 2f, -hw), new Vector3(0.12f, h / 2f, 0.12f), Quaternion.identity, white);
            Cyl(parent, new Vector3(x, h, 0f), new Vector3(0.12f, hw, 0.12f), Quaternion.Euler(90f, 0f, 0f), white);
            // Red: fondo, techo y laterales finos.
            Box(parent, new Vector3(x + sign * depth, h / 2f, 0f), new Vector3(0.03f, h, hw * 2f), net);
            Box(parent, new Vector3(x + sign * depth / 2f, h, 0f), new Vector3(depth, 0.03f, hw * 2f), net);
            Box(parent, new Vector3(x + sign * depth / 2f, h / 2f, hw), new Vector3(depth, h, 0.03f), net);
            Box(parent, new Vector3(x + sign * depth / 2f, h / 2f, -hw), new Vector3(depth, h, 0.03f), net);
        }

        private static void Cyl(Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Poste";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = rot;
            go.GetComponent<Renderer>().sharedMaterial = m;
        }

        private static void Box(Transform parent, Vector3 pos, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Red";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
        }
    }
}
