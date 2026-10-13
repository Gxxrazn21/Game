using Dorsal.Core.Match;
using UnityEngine;

namespace Dorsal.Unity
{
    public enum CameraMode
    {
        /// <summary>Vista lateral elevada tipo retransmisión.</summary>
        Broadcast,
        /// <summary>Detrás del futbolista controlado.</summary>
        Player,
        /// <summary>Vista alta y lejana, útil en pantallas pequeñas.</summary>
        Tactical,
    }

    /// <summary>Cámaras de partido con seguimiento suavizado del balón o del jugador.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public CameraMode Mode = CameraMode.Broadcast;

        private Camera _camera;
        private MatchEngine _engine;
        private Transform _ball;
        private Vector3 _focus;

        public Camera Camera => _camera;

        public void Init(MatchEngine engine, Transform ball)
        {
            _engine = engine;
            _ball = ball;
            _camera = GetComponent<Camera>();
            if (_camera == null) _camera = gameObject.AddComponent<Camera>();
            _camera.fieldOfView = 40f;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 400f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
            _focus = Vector3.zero;
        }

        public void Cycle() => Mode = (CameraMode)(((int)Mode + 1) % 3);

        private void LateUpdate()
        {
            if (_engine == null || _ball == null) return;
            var controlled = _engine.ControlledAgent;
            Vector3 target = _ball.position;
            if (Mode == CameraMode.Player && controlled != null)
                target = Vector3.Lerp(PitchSpace.ToUnity(controlled.Position), _ball.position, 0.35f);

            float dt = Time.deltaTime;
            _focus = Vector3.Lerp(_focus, new Vector3(target.x, 0f, target.z), 1f - Mathf.Exp(-4f * dt));

            Vector3 desiredPos;
            switch (Mode)
            {
                case CameraMode.Player:
                {
                    // Detrás del jugador mirando hacia la portería rival.
                    float dir = controlled != null ? controlled.Team.AttackDir : 1f;
                    desiredPos = _focus + new Vector3(-dir * 16f, 11f, 0f);
                    break;
                }
                case CameraMode.Tactical:
                    desiredPos = new Vector3(_focus.x * 0.6f, 75f, -38f);
                    break;
                default:
                    // Lateral: sigue el balón a lo largo, se mantiene en la banda.
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
