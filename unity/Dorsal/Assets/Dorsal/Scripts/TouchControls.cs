using System.Collections.Generic;
using Dorsal.Core.Match;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
// Alias explícitos: UnityEngine.InputSystem también define InputAction y PlayerInput.
using Gamepad = UnityEngine.InputSystem.Gamepad;
using Keyboard = UnityEngine.InputSystem.Keyboard;
using InputAction = Dorsal.Core.Match.InputAction;
using PlayerInput = Dorsal.Core.Match.PlayerInput;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using ETouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Dorsal.Unity
{
    /// <summary>Ajustes de controles configurables por el usuario.</summary>
    [System.Serializable]
    public sealed class ControlSettings
    {
        [Range(0.08f, 0.2f)] public float ButtonSize = 0.13f;     // fracción de la altura de pantalla
        [Range(0.2f, 1f)] public float Opacity = 0.55f;
        [Range(0.08f, 0.2f)] public float JoystickRadius = 0.11f;
        [Range(0f, 0.4f)] public float DeadZone = 0.12f;
        [Range(0.5f, 2f)] public float Sensitivity = 1f;
        public bool LeftHanded;
    }

    /// <summary>
    /// Controles táctiles (joystick flotante + botones contextuales), teclado y mando.
    /// Genera un <see cref="PlayerInput"/> por paso fijo del motor.
    /// </summary>
    public sealed class TouchControls : MonoBehaviour
    {
        private enum Button { None, A, B, C, D, Sprint }

        public ControlSettings Settings = new ControlSettings();

        private CameraRig _rig;
        private MatchEngine _engine;
        private readonly Dictionary<int, Button> _touchRoles = new Dictionary<int, Button>();
        private int _joystickTouch = -1;
        private Vector2 _joyOrigin, _joyCurrent;
        private bool _sprintHeld;
        private InputAction _pending = InputAction.None;
        private float _pendingPower;
        private float _shootHoldStart = -1f;
        private bool _attackingContext;

        public bool Visible { get; set; } = true;

        public void Init(MatchEngine engine, CameraRig rig)
        {
            _engine = engine;
            _rig = rig;
        }

        private void OnEnable() => EnhancedTouchSupport.Enable();

        private void OnDisable() => EnhancedTouchSupport.Disable();

        private void Update()
        {
            if (_engine == null) return;
            var me = _engine.ControlledAgent;
            _attackingContext = me != null && _engine.Ball.Owner == me;
            ReadTouches();
            ReadKeyboardAndGamepadActions();
        }

        /// <summary>Entrada para el siguiente paso del motor. Las acciones se consumen (flanco).</summary>
        public PlayerInput ConsumeInput()
        {
            Vector2 stick = Vector2.zero;
            if (_joystickTouch >= 0)
            {
                float radius = Settings.JoystickRadius * Screen.height;
                stick = Vector2.ClampMagnitude((_joyCurrent - _joyOrigin) / radius, 1f);
            }
            var kb = Keyboard.current;
            if (kb != null)
            {
                var k = new Vector2(
                    (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f),
                    (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f));
                if (k.sqrMagnitude > 0f) stick = k.normalized;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                var ls = pad.leftStick.ReadValue();
                if (ls.sqrMagnitude > Settings.DeadZone * Settings.DeadZone) stick = ls;
            }
            if (stick.magnitude < Settings.DeadZone) stick = Vector2.zero;
            stick = Vector2.ClampMagnitude(stick * Settings.Sensitivity, 1f);

            bool sprint = _sprintHeld || (kb != null && kb.leftShiftKey.isPressed) || (pad != null && pad.rightTrigger.isPressed);
            var input = new PlayerInput
            {
                Move = _rig != null ? _rig.ScreenToPitchDirection(stick) : new Vec2(stick.x, stick.y),
                Sprint = sprint,
                Action = _pending,
                Power = _pendingPower,
            };
            _pending = InputAction.None;
            _pendingPower = 0f;
            return input;
        }

        // ---------------------------------------------------------------- táctil

        private void ReadTouches()
        {
            foreach (var t in ETouch.activeTouches)
            {
                int id = t.touchId;
                Vector2 pos = t.screenPosition;
                switch (t.phase)
                {
                    case ETouchPhase.Began:
                    {
                        var b = HitButton(pos);
                        if (b != Button.None)
                        {
                            _touchRoles[id] = b;
                            Press(b);
                        }
                        else if (IsJoystickZone(pos) && _joystickTouch < 0)
                        {
                            _joystickTouch = id;
                            _joyOrigin = pos;
                            _joyCurrent = pos;
                        }
                        break;
                    }
                    case ETouchPhase.Moved:
                    case ETouchPhase.Stationary:
                        if (id == _joystickTouch) _joyCurrent = pos;
                        break;
                    case ETouchPhase.Ended:
                    case ETouchPhase.Canceled:
                        if (id == _joystickTouch) _joystickTouch = -1;
                        if (_touchRoles.TryGetValue(id, out var role))
                        {
                            Release(role);
                            _touchRoles.Remove(id);
                        }
                        break;
                }
            }
        }

        private bool IsJoystickZone(Vector2 screenPos)
        {
            bool leftSide = Settings.LeftHanded ? screenPos.x > Screen.width * 0.55f : screenPos.x < Screen.width * 0.45f;
            return leftSide && screenPos.y < Screen.height * 0.85f; // la franja superior es del marcador
        }

        private void Press(Button b)
        {
            switch (b)
            {
                case Button.A: _pending = _attackingContext ? InputAction.Pass : InputAction.Tackle; break;
                case Button.B: if (_attackingContext) _shootHoldStart = Time.unscaledTime; break;
                case Button.C: if (_attackingContext) _pending = InputAction.ThroughPass; break;
                case Button.D: if (_attackingContext) _pending = InputAction.Cross; break;
                case Button.Sprint: _sprintHeld = true; break;
            }
        }

        private void Release(Button b)
        {
            if (b == Button.Sprint) _sprintHeld = false;
            if (b == Button.B && _shootHoldStart >= 0f)
            {
                _pending = InputAction.Shoot;
                _pendingPower = Mathf.Clamp01((Time.unscaledTime - _shootHoldStart) / 0.8f);
                _shootHoldStart = -1f;
            }
        }

        // ---------------------------------------------------------------- teclado y mando

        private void ReadKeyboardAndGamepadActions()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.jKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Press(Button.A);
                if (kb.kKey.wasPressedThisFrame) Press(Button.B);
                if (kb.kKey.wasReleasedThisFrame) Release(Button.B);
                if (kb.lKey.wasPressedThisFrame) Press(Button.C);
                if (kb.uKey.wasPressedThisFrame) Press(Button.D);
                if (kb.iKey.wasPressedThisFrame && _attackingContext) _pending = InputAction.LoftedPass;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.buttonSouth.wasPressedThisFrame) Press(Button.A);
                if (pad.buttonEast.wasPressedThisFrame) Press(Button.B);
                if (pad.buttonEast.wasReleasedThisFrame) Release(Button.B);
                if (pad.buttonNorth.wasPressedThisFrame) Press(Button.C);
                if (pad.buttonWest.wasPressedThisFrame) Press(Button.D);
                if (pad.rightShoulder.wasPressedThisFrame && _attackingContext) _pending = InputAction.LoftedPass;
            }
        }

        // ---------------------------------------------------------------- dibujo (IMGUI)

        /// <summary>Rectángulo del botón en coordenadas de pantalla (origen abajo-izquierda, como el táctil).</summary>
        private Rect ButtonRect(Button b)
        {
            float s = Settings.ButtonSize * Screen.height;
            float margin = s * 0.35f;
            float right = Settings.LeftHanded ? margin + s : Screen.width - margin;
            float sign = Settings.LeftHanded ? 1f : -1f;
            Vector2 c;
            switch (b)
            {
                case Button.A: c = new Vector2(right + sign * s * 0.5f, margin + s * 0.5f); break;
                case Button.B: c = new Vector2(right + sign * s * 1.7f, margin + s * 1.0f); break;
                case Button.C: c = new Vector2(right + sign * s * 0.6f, margin + s * 1.75f); break;
                case Button.D: c = new Vector2(right + sign * s * 1.9f, margin + s * 2.3f); break;
                default: c = new Vector2(right + sign * s * 2.9f, margin + s * 0.45f); break;
            }
            float size = b == Button.A ? s * 1.1f : s * 0.85f;
            return new Rect(c.x - size / 2f, c.y - size / 2f, size, size);
        }

        private Button HitButton(Vector2 screenPos)
        {
            foreach (var b in new[] { Button.A, Button.B, Button.C, Button.D, Button.Sprint })
                if (ButtonRect(b).Contains(screenPos)) return b;
            return Button.None;
        }

        private string Label(Button b)
        {
            switch (b)
            {
                case Button.A: return _attackingContext ? "PASE" : "ENTRADA";
                case Button.B: return _attackingContext ? "TIRO" : "";
                case Button.C: return _attackingContext ? "AL HUECO" : "";
                case Button.D: return _attackingContext ? "CENTRO" : "";
                default: return "SPRINT";
            }
        }

        private void OnGUI()
        {
            if (!Visible || _engine == null || _engine.ControlledAgent == null) return;
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Settings.Opacity);
            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(Screen.height * 0.022f),
                fontStyle = FontStyle.Bold,
            };
            foreach (var b in new[] { Button.A, Button.B, Button.C, Button.D, Button.Sprint })
            {
                string label = Label(b);
                if (string.IsNullOrEmpty(label)) continue;
                var r = ButtonRect(b);
                GUI.Box(new Rect(r.x, Screen.height - r.yMax, r.width, r.height), label, style);
            }
            if (_shootHoldStart >= 0f)
            {
                var r = ButtonRect(Button.B);
                float p = Mathf.Clamp01((Time.unscaledTime - _shootHoldStart) / 0.8f);
                GUI.Box(new Rect(r.x, Screen.height - r.yMax - 18f, r.width * p, 12f), GUIContent.none);
            }
            if (_joystickTouch >= 0)
            {
                float radius = Settings.JoystickRadius * Screen.height;
                GUI.Box(new Rect(_joyOrigin.x - radius, Screen.height - _joyOrigin.y - radius, radius * 2f, radius * 2f), GUIContent.none);
                var knob = _joyOrigin + Vector2.ClampMagnitude(_joyCurrent - _joyOrigin, radius);
                GUI.Box(new Rect(knob.x - radius * 0.35f, Screen.height - knob.y - radius * 0.35f, radius * 0.7f, radius * 0.7f), GUIContent.none);
            }
            GUI.color = old;
        }
    }
}
