using UnityEngine;

namespace Dorsal.Unity
{
    /// <summary>
    /// Estilo visual propio de DORSAL (IMGUI escalado a una resolución de referencia de 1280×720).
    /// Paleta: azul noche, blanco roto y acento coral; sin elementos de otras franquicias.
    /// </summary>
    public static class UiKit
    {
        public const float RefHeight = 720f;

        public static readonly Color Night = new Color(0.07f, 0.09f, 0.14f);
        public static readonly Color Panel = new Color(0.12f, 0.15f, 0.22f, 0.96f);
        public static readonly Color PanelLight = new Color(0.18f, 0.22f, 0.31f, 0.96f);
        public static readonly Color Accent = new Color(1f, 0.42f, 0.33f);
        public static readonly Color AccentDark = new Color(0.78f, 0.28f, 0.22f);
        public static readonly Color Text = new Color(0.95f, 0.94f, 0.9f);
        public static readonly Color Muted = new Color(0.65f, 0.68f, 0.75f);
        public static readonly Color Good = new Color(0.36f, 0.82f, 0.5f);
        public static readonly Color Bad = new Color(0.95f, 0.38f, 0.38f);

        private static Texture2D _panelTex, _panelLightTex, _accentTex, _accentDarkTex, _whiteTex, _nightTex;
        private static GUIStyle _title, _h2, _label, _muted, _button, _accentButton, _panel, _card, _small, _field, _bar;
        private static int _builtForHeight = -1;

        public static float Scale => Screen.height / RefHeight;

        /// <summary>Ancho visible en unidades de referencia.</summary>
        public static float Width => Screen.width / Scale;

        public static float Height => RefHeight;

        /// <summary>Aplica la escala global. Llamar al principio de cada OnGUI de pantallas.</summary>
        public static void Begin()
        {
            Ensure();
            float s = Scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
        }

        public static void End() => GUI.matrix = Matrix4x4.identity;

        public static GUIStyle Title { get { Ensure(); return _title; } }
        public static GUIStyle H2 { get { Ensure(); return _h2; } }
        public static GUIStyle Label { get { Ensure(); return _label; } }
        public static GUIStyle MutedLabel { get { Ensure(); return _muted; } }
        public static GUIStyle Small { get { Ensure(); return _small; } }
        public static GUIStyle Button { get { Ensure(); return _button; } }
        public static GUIStyle AccentButton { get { Ensure(); return _accentButton; } }
        public static GUIStyle PanelStyle { get { Ensure(); return _panel; } }
        public static GUIStyle Card { get { Ensure(); return _card; } }
        public static GUIStyle Field { get { Ensure(); return _field; } }

        public static void Background()
        {
            Ensure();
            GUI.DrawTexture(new Rect(0, 0, Width, Height), _nightTex);
            // Franja de acento: identidad del juego.
            GUI.DrawTexture(new Rect(0, 0, Width, 6), _accentTex);
        }

        public static void Bar(Rect r, float value01, Color color)
        {
            Ensure();
            GUI.DrawTexture(r, _panelLightTex);
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(value01), r.height), _whiteTex);
            GUI.color = old;
        }

        public static void LabeledBar(string label, float value01, string valueText, Color color, float width = 300f)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Small, GUILayout.Width(width * 0.42f));
            var r = GUILayoutUtility.GetRect(width * 0.42f, 14f, GUILayout.Width(width * 0.42f));
            r.y += 6f;
            Bar(r, value01, color);
            GUILayout.Label(valueText, Small, GUILayout.Width(width * 0.16f));
            GUILayout.EndHorizontal();
        }

        public static Color RatingColor(float rating) =>
            rating >= 7.5f ? Good : rating >= 6.5f ? Text : rating >= 5.8f ? new Color(1f, 0.8f, 0.35f) : Bad;

        public static Color AttributeColor(int v) =>
            v >= 80 ? Good : v >= 65 ? new Color(0.75f, 0.9f, 0.45f) : v >= 50 ? new Color(1f, 0.8f, 0.35f) : Bad;

        private static void Ensure()
        {
            if (_title != null && _builtForHeight == Screen.height) return;
            _builtForHeight = Screen.height;
            _panelTex = Solid(Panel);
            _panelLightTex = Solid(PanelLight);
            _accentTex = Solid(Accent);
            _accentDarkTex = Solid(AccentDark);
            _whiteTex = Solid(Color.white);
            _nightTex = Solid(Night);

            var skin = GUI.skin;
            _title = new GUIStyle(skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, normal = { textColor = Text } };
            _h2 = new GUIStyle(skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = Accent } };
            _label = new GUIStyle(skin.label) { fontSize = 18, wordWrap = true, normal = { textColor = Text } };
            _muted = new GUIStyle(_label) { normal = { textColor = Muted } };
            _small = new GUIStyle(_label) { fontSize = 15 };
            _button = new GUIStyle(skin.button)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                fixedHeight = 52f,
                margin = new RectOffset(4, 4, 4, 4),
                normal = { background = _panelLightTex, textColor = Text },
                hover = { background = _panelLightTex, textColor = Accent },
                active = { background = _accentDarkTex, textColor = Text },
            };
            _accentButton = new GUIStyle(_button)
            {
                normal = { background = _accentTex, textColor = Night },
                hover = { background = _accentTex, textColor = Color.white },
                active = { background = _accentDarkTex, textColor = Color.white },
            };
            _panel = new GUIStyle(skin.box) { padding = new RectOffset(16, 16, 14, 14), normal = { background = _panelTex } };
            _card = new GUIStyle(skin.box) { padding = new RectOffset(12, 12, 10, 10), margin = new RectOffset(4, 4, 4, 4), normal = { background = _panelLightTex } };
            _field = new GUIStyle(skin.textField) { fontSize = 20, fixedHeight = 40f, padding = new RectOffset(10, 10, 8, 8) };
        }

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }

    /// <summary>Ajustes del usuario guardados con PlayerPrefs (gráficos, controles, partido).</summary>
    public sealed class GameSettings
    {
        public int Quality = 1;          // 0 bajo, 1 medio, 2 alto
        public int TargetFps = 30;
        public float HalfMinutes = 4f;   // minutos reales por parte
        public float ButtonSize = 0.13f;
        public float ButtonOpacity = 0.55f;
        public bool LeftHanded;
        public bool AutoReplays = true;
        public bool ShowPerformance;
        public bool Stadium = true;

        public static GameSettings Load()
        {
            var s = new GameSettings
            {
                Quality = PlayerPrefs.GetInt("dorsal.quality", 1),
                TargetFps = PlayerPrefs.GetInt("dorsal.fps", 30),
                HalfMinutes = PlayerPrefs.GetFloat("dorsal.half", 4f),
                ButtonSize = PlayerPrefs.GetFloat("dorsal.btnsize", 0.13f),
                ButtonOpacity = PlayerPrefs.GetFloat("dorsal.btnopacity", 0.55f),
                LeftHanded = PlayerPrefs.GetInt("dorsal.lefthanded", 0) == 1,
                AutoReplays = PlayerPrefs.GetInt("dorsal.replays", 1) == 1,
                ShowPerformance = PlayerPrefs.GetInt("dorsal.perf", 0) == 1,
                Stadium = PlayerPrefs.GetInt("dorsal.stadium", 1) == 1,
            };
            return s;
        }

        public void Save()
        {
            PlayerPrefs.SetInt("dorsal.quality", Quality);
            PlayerPrefs.SetInt("dorsal.fps", TargetFps);
            PlayerPrefs.SetFloat("dorsal.half", HalfMinutes);
            PlayerPrefs.SetFloat("dorsal.btnsize", ButtonSize);
            PlayerPrefs.SetFloat("dorsal.btnopacity", ButtonOpacity);
            PlayerPrefs.SetInt("dorsal.lefthanded", LeftHanded ? 1 : 0);
            PlayerPrefs.SetInt("dorsal.replays", AutoReplays ? 1 : 0);
            PlayerPrefs.SetInt("dorsal.perf", ShowPerformance ? 1 : 0);
            PlayerPrefs.SetInt("dorsal.stadium", Stadium ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Aplica límite de FPS y nivel de calidad. Las cifras reales deben medirse en el dispositivo.</summary>
        public void Apply()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFps;
            int levels = QualitySettings.names.Length;
            if (levels > 0)
            {
                int idx = Quality == 0 ? 0 : Quality == 1 ? levels / 2 : levels - 1;
                QualitySettings.SetQualityLevel(idx, true);
            }
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
