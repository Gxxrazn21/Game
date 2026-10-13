using Dorsal.Core.Match;
using UnityEngine;

namespace Dorsal.Unity
{
    public enum CameraMode
    {
        /// <summary>Vista lateral elevada tipo retransmisión.</summary>
        Broadcast,
        /// <summary>Detrás del futbolista controlado (para el portero: detrás de su portería).</summary>
        Player,
        /// <summary>Vista alta y lejana, útil en pantallas pequeñas.</summary>
        Tactical,
    }

    /// <summary>Cámaras de partido con seguimiento suavizado, más una cámara cinematográfica para repeticiones.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public CameraMode Mode = CameraMode.Broadcast;

        private Camera _camera;
        private MatchEngine _engine;
        private Transform _ball;
        private Vector3 _focus;
        private float _replayOrbit;

        public Camera Camera => _camera;

        /// <summary>Activa la cámara de repetición (a ras de césped, girando alrededor del balón).</summary>
        public bool ReplayMode { get; set; }

        public void Init(MatchEngine engine, Transform ball)
        {
            _engine = engine;
            _ball = ball;
            _camera = GetComponent<Camera>();
            if (_camera == null) _camera = gameObject.AddComponent<Camera>();
            _camera.fieldOfView = 40f;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 500f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.32f, 0.45f, 0.62f);
            _focus = Vector3.zero;
            var c = engine.ControlledAgent;
            if (c != null && c.IsGoalkeeper) Mode = CameraMode.Player; // el portero ve mejor el juego desde su portería
        }

        public void Cycle() => Mode = (CameraMode)(((int)Mode + 1) % 3);

        private void LateUpdate()
        {
            if (_engine == null || _ball == null) return;
            float dt = Time.unscaledDeltaTime;

            if (ReplayMode)
            {
                _replayOrbit += dt * 12f;
                var b = _ball.position;
                var offset = Quaternion.Euler(0f, _replayOrbit, 0f) * new Vector3(0f, 2.2f, -9f);
                transform.position = Vector3.Lerp(transform.position, b + offset, 1f - Mathf.Exp(-5f * dt));
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(b + Vector3.up * 0.8f - transform.position, Vector3.up), 1f - Mathf.Exp(-8f * dt));
                _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, 34f, 1f - Mathf.Exp(-4f * dt));
                return;
            }
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, 40f, 1f - Mathf.Exp(-4f * dt));

            var controlled = _engine.ControlledAgent;
            Vector3 target = _ball.position;
            if (Mode == CameraMode.Player && controlled != null)
                target = Vector3.Lerp(PitchSpace.ToUnity(controlled.Position), _ball.position, controlled.IsGoalkeeper ? 0.55f : 0.35f);

            _focus = Vector3.Lerp(_focus, new Vector3(target.x, 0f, target.z), 1f - Mathf.Exp(-4f * dt));

            Vector3 desiredPos;
            switch (Mode)
            {
                case CameraMode.Player:
                {
                    float dir = controlled != null ? controlled.Team.AttackDir : 1f;
                    desiredPos = controlled != null && controlled.IsGoalkeeper
                        ? new Vector3(-dir * (Pitch.HalfLength + 12f), 9f, _focus.z * 0.5f)
                        : _focus + new Vector3(-dir * 16f, 11f, 0f);
                    break;
                }
                case CameraMode.Tactical:
                    desiredPos = new Vector3(_focus.x * 0.6f, 75f, -38f);
                    break;
                default:
                    desiredPos = new Vector3(_focus.x * 0.85f, 26f, -Pitch.HalfWidth - 24f + _focus.z * 0.25f);
                    break;
            }
            transform.position = Vector3.Lerp(transform.position, desiredPos, 1f - Mathf.Exp(-3f * dt));
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(_focus - transform.position, Vector3.up), 1f - Mathf.Exp(-6f * dt));
        }

        /// <summary>
        /// Convierte la dirección del joystick (pantalla) a dirección en el campo según la orientación de la cámara,
        /// para que "arriba" en el joystick sea siempre "hacia el fondo de la pantalla".
        /// </summary>
        public Vec2 ScreenToPitchDirection(Vector2 stick)
        {
            if (_camera == null) return new Vec2(stick.x, stick.y);
            var fwd = _camera.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            var right = Vector3.Cross(Vector3.up, fwd);
            var world = right * stick.x + fwd * stick.y;
            return new Vec2(world.x, world.z);
        }
    }
}
