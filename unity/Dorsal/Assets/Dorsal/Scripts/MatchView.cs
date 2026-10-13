using System.Collections.Generic;
using Dorsal.Core.Match;
using UnityEngine;

namespace Dorsal.Unity
{
    /// <summary>
    /// Representación 3D del partido. M2: primitivas (campo, porterías, cápsulas, balón).
    /// M3 sustituirá las cápsulas por modelos humanoides con animaciones (la interfaz no cambia:
    /// posición, orientación y <see cref="AgentAction"/> de cada agente).
    /// </summary>
    public sealed class MatchView : MonoBehaviour
    {
        private MatchEngine _engine;
        private readonly List<Transform> _agentViews = new List<Transform>();
        private readonly List<Vector3> _prevPos = new List<Vector3>();
        private readonly List<Vector3> _curPos = new List<Vector3>();
        private readonly List<Quaternion> _prevRot = new List<Quaternion>();
        private readonly List<Quaternion> _curRot = new List<Quaternion>();
        private Transform _ball;
        private Vector3 _prevBall, _curBall;
        private Transform _marker;
        private readonly List<Transform> _labels = new List<Transform>();

        public Transform BallTransform => _ball;

        public void Build(MatchEngine engine)
        {
            _engine = engine;
            BuildPitch();
            BuildGoal(1);
            BuildGoal(-1);

            var homeColor = PitchSpace.ParseColor(engine.Home.Setup.PrimaryColor, Color.red);
            var awayColor = PitchSpace.ParseColor(engine.Away.Setup.PrimaryColor, Color.blue);
            if (PitchSpace.ColorDistance(homeColor, awayColor) < 0.25f)
                awayColor = PitchSpace.ParseColor(engine.Away.Setup.SecondaryColor, Color.white);
            var gkHome = Color.Lerp(homeColor, Color.yellow, 0.6f);
            var gkAway = Color.Lerp(awayColor, Color.green, 0.6f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (var agent in engine.Agents)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = agent.Player.DisplayName;
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                go.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
                bool home = agent.Team.Side == TeamSide.Home;
                var color = agent.IsGoalkeeper ? (home ? gkHome : gkAway) : (home ? homeColor : awayColor);
                go.GetComponent<Renderer>().material.color = color;

                // Dorsal sobre la cabeza (identidad propia, sin recursos de terceros).
                var label = new GameObject("Dorsal");
                label.transform.SetParent(go.transform, false);
                label.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                var tm = label.AddComponent<TextMesh>();
                tm.text = agent.Player.ShirtNumber?.ToString() ?? "";
                tm.font = font;
                tm.characterSize = 0.18f;
                tm.fontSize = 48;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.color = Color.white;
                label.GetComponent<MeshRenderer>().material = font.material;
                _labels.Add(label.transform);

                _agentViews.Add(go.transform);
                var p = PitchSpace.ToUnity(agent.Position, 0.9f);
                _prevPos.Add(p);
                _curPos.Add(p);
                _prevRot.Add(Quaternion.identity);
                _curRot.Add(Quaternion.identity);
            }

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Balon";
            Destroy(ball.GetComponent<Collider>());
            ball.transform.SetParent(transform, false);
            ball.transform.localScale = Vector3.one * (Pitch.BallRadius * 2f);
            ball.GetComponent<Renderer>().material.color = Color.white;
            _ball = ball.transform;
            _prevBall = _curBall = PitchSpace.ToUnity(engine.Ball.Position);

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "MarcadorJugador";
            Destroy(marker.GetComponent<Collider>());
            marker.transform.SetParent(transform, false);
            marker.transform.localScale = new Vector3(1.4f, 0.01f, 1.4f);
            marker.GetComponent<Renderer>().material.color = new Color(1f, 0.85f, 0.1f);
            _marker = marker.transform;
        }

        /// <summary>Guardar el estado actual antes de avanzar el motor (para interpolar).</summary>
        public void CaptureBeforeStep()
        {
            for (int i = 0; i < _agentViews.Count; i++)
            {
                _prevPos[i] = _curPos[i];
                _prevRot[i] = _curRot[i];
            }
            _prevBall = _curBall;
        }

        /// <summary>Leer el estado del motor tras avanzar.</summary>
        public void CaptureAfterStep()
        {
            var agents = _engine.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                var a = agents[i];
                _curPos[i] = PitchSpace.ToUnity(a.Position, 0.9f);
                var f = PitchSpace.ToUnity(a.Facing);
                if (f.sqrMagnitude > 0.01f) _curRot[i] = Quaternion.LookRotation(f, Vector3.up);
            }
            _curBall = PitchSpace.ToUnity(_engine.Ball.Position);
        }

        /// <summary>Teletransporte sin interpolar (saques, descanso).</summary>
        public void SnapAll()
        {
            CaptureAfterStep();
            for (int i = 0; i < _agentViews.Count; i++)
            {
                _prevPos[i] = _curPos[i];
                _prevRot[i] = _curRot[i];
            }
            _prevBall = _curBall;
        }

        private void Update()
        {
            if (_engine == null) return;
            float alpha = Time.fixedDeltaTime > 0f ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime) : 1f;
            var agents = _engine.Agents;
            var controlled = _engine.ControlledAgent;
            for (int i = 0; i < _agentViews.Count; i++)
            {
                var view = _agentViews[i];
                bool visible = agents[i].OnPitch;
                if (view.gameObject.activeSelf != visible) view.gameObject.SetActive(visible);
                if (!visible) continue;
                // Saltos grandes (colocación de saque) no se interpolan.
                var target = (_curPos[i] - _prevPos[i]).sqrMagnitude > 25f ? _curPos[i] : Vector3.Lerp(_prevPos[i], _curPos[i], alpha);
                view.localPosition = target;
                view.localRotation = Quaternion.Slerp(_prevRot[i], _curRot[i], alpha);
                // Indicación mínima de acción: inclinación al entrar o tirarse.
                var act = agents[i].CurrentAction;
                if (act == AgentAction.Dive || act == AgentAction.Tackle)
                    view.localRotation *= Quaternion.Euler(0f, 0f, act == AgentAction.Dive ? 70f : 35f);
                if (agents[i] == controlled) _marker.localPosition = new Vector3(target.x, 0.02f, target.z);
            }
            _marker.gameObject.SetActive(controlled != null);
            _ball.localPosition = (_curBall - _prevBall).sqrMagnitude > 25f ? _curBall : Vector3.Lerp(_prevBall, _curBall, alpha);
        }

        private void LateUpdate()
        {
            // Los dorsales miran siempre a la cámara.
            var cam = Camera.main;
            if (cam == null) return;
            var rot = cam.transform.rotation;
            for (int i = 0; i < _labels.Count; i++) _labels[i].rotation = rot;
        }

        // ---------------------------------------------------------------- campo

        private void BuildPitch()
        {
            var grass = GameObject.CreatePrimitive(PrimitiveType.Plane);
            grass.name = "Cesped";
            Destroy(grass.GetComponent<Collider>());
            grass.transform.SetParent(transform, false);
            // Un Plane mide 10×10 unidades: 120×80 m con margen alrededor del rectángulo de juego.
            grass.transform.localScale = new Vector3(12f, 1f, 8f);
            grass.GetComponent<Renderer>().material.color = new Color(0.16f, 0.45f, 0.18f);

            // Franjas de corte del césped.
            for (int i = 0; i < 10; i++)
            {
                if (i % 2 == 0) continue;
                var stripe = Quad("Franja", new Vector3(-Pitch.HalfLength + 10.5f * i + 5.25f, 0.01f, 0f), new Vector3(10.5f, 1f, Pitch.HalfWidth * 2f));
                stripe.GetComponent<Renderer>().material.color = new Color(0.18f, 0.5f, 0.2f);
            }

            float w = 0.15f;
            float L = Pitch.HalfLength, W = Pitch.HalfWidth;
            Line(new Vector3(0f, 0f, W), new Vector3(2 * L, 1, w));
            Line(new Vector3(0f, 0f, -W), new Vector3(2 * L, 1, w));
            Line(new Vector3(L, 0f, 0f), new Vector3(w, 1, 2 * W));
            Line(new Vector3(-L, 0f, 0f), new Vector3(w, 1, 2 * W));
            Line(Vector3.zero, new Vector3(w, 1, 2 * W));
            foreach (int s in new[] { 1, -1 })
            {
                float pa = Pitch.PenaltyAreaDepth, pw = Pitch.PenaltyAreaHalfWidth;
                Line(new Vector3(s * (L - pa), 0f, 0f), new Vector3(w, 1, 2 * pw));
                Line(new Vector3(s * (L - pa / 2f), 0f, pw), new Vector3(pa, 1, w));
                Line(new Vector3(s * (L - pa / 2f), 0f, -pw), new Vector3(pa, 1, w));
                float ga = Pitch.GoalAreaDepth, gw = 9.16f;
                Line(new Vector3(s * (L - ga), 0f, 0f), new Vector3(w, 1, 2 * gw));
                Line(new Vector3(s * (L - ga / 2f), 0f, gw), new Vector3(ga, 1, w));
                Line(new Vector3(s * (L - ga / 2f), 0f, -gw), new Vector3(ga, 1, w));
                Line(new Vector3(s * (L - Pitch.PenaltySpotDistance), 0f, 0f), new Vector3(0.3f, 1, 0.3f));
            }
            // Círculo central por segmentos.
            const int segments = 48;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                var p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * Pitch.CenterCircleRadius;
                var p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * Pitch.CenterCircleRadius;
                var seg = Line((p0 + p1) / 2f, new Vector3((p1 - p0).magnitude, 1, w));
                seg.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(p1 - p0, Vector3.up).normalized, Vector3.up);
            }
        }

        private GameObject Quad(string name, Vector3 center, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = center;
            go.transform.localScale = new Vector3(size.x, 0.01f, size.z);
            return go;
        }

        private GameObject Line(Vector3 center, Vector3 size)
        {
            // Separación vertical suficiente para evitar el parpadeo (z-fighting) con el césped a distancia.
            var go = Quad("Linea", center + new Vector3(0f, 0.03f, 0f), size);
            go.GetComponent<Renderer>().material.color = Color.white;
            return go;
        }

        private void BuildGoal(int sign)
        {
            float x = sign * (Pitch.HalfLength + 0.06f);
            float h = Pitch.GoalHeight, hw = Pitch.GoalHalfWidth;
            Post(new Vector3(x, h / 2f, hw), new Vector3(0.12f, h / 2f, 0.12f), false);
            Post(new Vector3(x, h / 2f, -hw), new Vector3(0.12f, h / 2f, 0.12f), false);
            Post(new Vector3(x, h, 0f), new Vector3(0.12f, hw, 0.12f), true);
            // Red simplificada (cubo semitransparente no es trivial sin material propio: se usa un fondo gris).
            var net = GameObject.CreatePrimitive(PrimitiveType.Cube);
            net.name = "Red";
            Destroy(net.GetComponent<Collider>());
            net.transform.SetParent(transform, false);
            net.transform.localPosition = new Vector3(x + sign * 1.0f, h / 2f, 0f);
            net.transform.localScale = new Vector3(0.05f, h, hw * 2f);
            net.GetComponent<Renderer>().material.color = new Color(0.85f, 0.85f, 0.85f);
        }

        private void Post(Vector3 pos, Vector3 halfSize, bool horizontal)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Poste";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(halfSize.x, horizontal ? halfSize.y : halfSize.y, halfSize.z);
            if (horizontal) go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.GetComponent<Renderer>().material.color = Color.white;
        }
    }
}
