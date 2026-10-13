using System;
using System.Linq;
using Dorsal.Core.Career;
using Dorsal.Core.Domain;
using Dorsal.Core.Localization;
using Dorsal.Core.World;
using UnityEngine;

namespace Dorsal.Unity
{
    /// <summary>Pantallas de menú: inicio, creación del futbolista, cargar partida y ajustes.</summary>
    public sealed partial class DorsalApp
    {
        // Formulario de creación
        private string _firstName, _lastName;
        private int _natIndex, _age, _height, _build, _foot, _weakFoot, _posIndex, _styleIndex, _personality, _skin, _hair, _shirt;
        private string[] _nationalities;

        private static readonly Position[] CreatablePositions =
        {
            Position.GK, Position.CB, Position.LB, Position.RB, Position.CDM, Position.CM,
            Position.CAM, Position.LM, Position.RM, Position.LW, Position.RW, Position.ST,
        };

        private void ResetCreationForm()
        {
            _firstName = "";
            _lastName = "";
            _nationalities = NameGenerator.SupportedNationalities.OrderBy(Es.Nationality).ToArray();
            _natIndex = Array.IndexOf(_nationalities, "ESP");
            if (_natIndex < 0) _natIndex = 0;
            _age = 17;
            _height = 178;
            _build = 1;
            _foot = 0;
            _weakFoot = 2;
            _posIndex = 11;
            _styleIndex = 0;
            _personality = 4;
            _skin = 1;
            _hair = 0;
            _shirt = 9;
        }

        private static bool Back(string text = "Volver")
        {
            return GUI.Button(new Rect(UiKit.Width - 190f, 18f, 170f, 52f), text, UiKit.Button);
        }

        private void DrawMainMenu()
        {
            float w = UiKit.Width;
            GUI.Label(new Rect(60f, 70f, 800f, 70f), "DORSAL", new GUIStyle(UiKit.Title) { fontSize = 64 });
            GUI.Label(new Rect(64f, 150f, 800f, 40f), "Carrera de futbolista", UiKit.H2);

            GUILayout.BeginArea(new Rect(60f, 220f, 420f, 460f));
            var latest = _saves.ListSlots().OrderByDescending(s => s.header.SavedUtc).FirstOrDefault();
            if (latest.header != null && GUILayout.Button($"Continuar: {latest.header.Summary.PlayerName}", UiKit.AccentButton)) LoadSlot(latest.slot);
            if (GUILayout.Button("Nueva carrera", latest.header == null ? UiKit.AccentButton : UiKit.Button)) { ResetCreationForm(); Go(AppScreen.NewCareer); }
            if (GUILayout.Button("Cargar partida", UiKit.Button)) Go(AppScreen.LoadGame);
            if (GUILayout.Button("Partido rápido", UiKit.Button)) StartQuickMatch(keeper: false);
            if (GUILayout.Button("Desafío portero", UiKit.Button)) StartQuickMatch(keeper: true);
            if (GUILayout.Button("Ajustes", UiKit.Button)) { _returnFromSettings = AppScreen.MainMenu; Go(AppScreen.Settings); }
            GUILayout.EndArea();

            GUI.Label(new Rect(w - 520f, UiKit.Height - 70f, 500f, 50f),
                $"v{Version} · Clubes, ligas y jugadores ficticios. Sin licencias oficiales.", new GUIStyle(UiKit.MutedLabel) { alignment = TextAnchor.LowerRight, fontSize = 14 });
        }

        private static int Cycle(string label, int index, int count, Func<int, string> text, float width = 420f)
        {
            GUILayout.BeginHorizontal(GUILayout.Width(width));
            GUILayout.Label(label, UiKit.Label, GUILayout.Width(150f));
            if (GUILayout.Button("<", UiKit.Button, GUILayout.Width(52f))) index = (index - 1 + count) % count;
            GUILayout.Label(text(index), new GUIStyle(UiKit.Label) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width(width - 290f), GUILayout.Height(52f));
            if (GUILayout.Button(">", UiKit.Button, GUILayout.Width(52f))) index = (index + 1) % count;
            GUILayout.EndHorizontal();
            return index;
        }

        private PlayerCreationRequest BuildRequest()
        {
            var pos = CreatablePositions[_posIndex];
            var styles = PlayStyle.For(pos).ToList();
            var start = new DateTime(2026, 8, 1);
            return new PlayerCreationRequest
            {
                FirstName = _firstName,
                LastName = _lastName,
                Nationality = _nationalities[_natIndex],
                BirthDate = new DateTime(start.Year - _age, 3, 15),
                HeightCm = _height,
                Build = (Build)_build,
                PreferredFoot = (Foot)_foot,
                WeakFoot = _weakFoot + 1,
                Position = pos,
                PlayStyleId = styles.Count > 0 ? styles[Mathf.Clamp(_styleIndex, 0, styles.Count - 1)].Id : "",
                Personality = (PersonalityArchetype)_personality,
                Appearance = new Appearance { SkinTone = _skin, HairColor = _hair },
                PreferredShirtNumber = _shirt,
            };
        }

        private void DrawNewCareer()
        {
            GUI.Label(new Rect(40f, 20f, 700f, 50f), "Crea tu futbolista", UiKit.Title);
            if (Back()) Go(AppScreen.MainMenu);
            float w = UiKit.Width;
            _scroll = GUI.BeginScrollView(new Rect(30f, 80f, w - 60f, UiKit.Height - 160f), _scroll, new Rect(0f, 0f, w - 90f, 760f));

            GUILayout.BeginArea(new Rect(10f, 0f, 460f, 760f));
            GUILayout.Label("Identidad", UiKit.H2);
            GUILayout.Label("Nombre", UiKit.Small);
            _firstName = GUILayout.TextField(_firstName ?? "", 20, UiKit.Field);
            GUILayout.Label("Apellido", UiKit.Small);
            _lastName = GUILayout.TextField(_lastName ?? "", 20, UiKit.Field);
            _natIndex = Cycle("Nacionalidad", _natIndex, _nationalities.Length, i => Es.Nationality(_nationalities[i]));
            _age = 16 + Cycle("Edad", _age - 16, 6, i => $"{16 + i} años");
            GUILayout.Space(8f);
            GUILayout.Label("Físico", UiKit.H2);
            GUILayout.Label($"Altura: {_height} cm", UiKit.Small);
            _height = Mathf.RoundToInt(GUILayout.HorizontalSlider(_height, 160f, 200f, GUILayout.Width(420f), GUILayout.Height(30f)));
            _build = GUILayout.Toolbar(_build, new[] { "Delgado", "Normal", "Robusto" }, UiKit.Button, GUILayout.Width(420f));
            _foot = GUILayout.Toolbar(_foot, new[] { "Diestro", "Zurdo" }, UiKit.Button, GUILayout.Width(420f));
            GUILayout.Label("Pie débil (estrellas)", UiKit.Small);
            _weakFoot = GUILayout.Toolbar(_weakFoot, new[] { "1", "2", "3", "4", "5" }, UiKit.Button, GUILayout.Width(420f));
            _skin = Cycle("Piel", _skin, MaterialCache.SkinTones.Length, i => $"Tono {i + 1}");
            _hair = Cycle("Pelo", _hair, MaterialCache.HairColors.Length, i => $"Color {i + 1}");
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(500f, 0f, w - 600f, 760f));
            GUILayout.Label("Juego", UiKit.H2);
            int oldPos = _posIndex;
            _posIndex = GUILayout.SelectionGrid(_posIndex, CreatablePositions.Select(p => Es.Position(p)).ToArray(), 6, UiKit.Button);
            if (oldPos != _posIndex) _styleIndex = 0;
            var pos = CreatablePositions[_posIndex];
            GUILayout.Label(Es.PositionLong(pos) + (pos == Position.GK ? " — incluye el modo portero: colócate, estírate y para." : ""), UiKit.MutedLabel);
            var styles = PlayStyle.For(pos).ToList();
            if (styles.Count > 0)
            {
                GUILayout.Label("Estilo de juego", UiKit.Small);
                _styleIndex = GUILayout.SelectionGrid(Mathf.Clamp(_styleIndex, 0, styles.Count - 1), styles.Select(s => s.Name).ToArray(), 2, UiKit.Button);
            }
            GUILayout.Label("Personalidad", UiKit.Small);
            _personality = GUILayout.SelectionGrid(_personality, new[] { "Profesional", "Ambicioso", "Temperamental", "Leal", "Equilibrado" }, 3, UiKit.Button);
            _shirt = 1 + Cycle("Dorsal", _shirt - 1, 99, i => (i + 1).ToString(), 380f);

            var request = BuildRequest();
            var errors = PlayerCreator.Validate(request, new DateTime(2026, 8, 1));
            GUILayout.Space(10f);
            foreach (var e in errors.Take(3)) GUILayout.Label("• " + e, new GUIStyle(UiKit.Small) { normal = { textColor = UiKit.Bad } });
            GUI.enabled = errors.Count == 0;
            if (GUILayout.Button("Crear futbolista y empezar", UiKit.AccentButton, GUILayout.Width(420f)))
            {
                _career = CareerSession.NewCareer(request, (ulong)DateTime.Now.Ticks, new CareerSettings { HalfDurationSeconds = _settings.HalfMinutes * 60f });
                _slot = FirstFreeSlot();
                Autosave();
                Toast($"Partida guardada en la ranura '{_slot}'.");
                Go(AppScreen.ChooseClub);
            }
            GUI.enabled = true;
            GUILayout.EndArea();
            GUI.EndScrollView();
        }

        private void DrawLoadGame()
        {
            GUI.Label(new Rect(40f, 20f, 700f, 50f), "Cargar partida", UiKit.Title);
            if (Back()) Go(AppScreen.MainMenu);
            var slots = _saves.ListSlots().OrderByDescending(s => s.header.SavedUtc).ToList();
            GUILayout.BeginArea(new Rect(40f, 90f, UiKit.Width - 80f, UiKit.Height - 120f));
            if (slots.Count == 0) GUILayout.Label("No hay partidas guardadas.", UiKit.MutedLabel);
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var (slot, header) in slots)
            {
                GUILayout.BeginHorizontal(UiKit.Card);
                var s = header.Summary;
                GUILayout.Label($"{s.PlayerName} · {s.ClubName} · temporada {s.SeasonYear}/{(s.SeasonYear + 1) % 100:00} · {s.Age} años · media {s.Overall}\n" +
                                $"Ranura '{slot}' · guardada {header.SavedUtc.ToLocalTime():dd/MM/yyyy HH:mm}", UiKit.Label);
                if (GUILayout.Button("Cargar", UiKit.AccentButton, GUILayout.Width(150f))) LoadSlot(slot);
                if (GUILayout.Button("Borrar", UiKit.Button, GUILayout.Width(130f))) { _saves.Delete(slot); Toast($"Ranura '{slot}' borrada."); }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawSettings()
        {
            GUI.Label(new Rect(40f, 20f, 700f, 50f), "Ajustes", UiKit.Title);
            if (Back("Guardar"))
            {
                _settings.Save();
                _settings.Apply();
                Go(_returnFromSettings);
            }
            GUILayout.BeginArea(new Rect(40f, 90f, 560f, UiKit.Height - 110f));
            GUILayout.Label("Gráficos", UiKit.H2);
            _settings.Quality = GUILayout.Toolbar(_settings.Quality, new[] { "Bajo", "Medio", "Alto" }, UiKit.Button);
            GUILayout.Label("Límite de FPS (60 consume más batería)", UiKit.Small);
            _settings.TargetFps = GUILayout.Toolbar(_settings.TargetFps >= 60 ? 1 : 0, new[] { "30", "60" }, UiKit.Button) == 1 ? 60 : 30;
            _settings.Stadium = GUILayout.Toggle(_settings.Stadium, " Estadio y público", UiKit.Label);
            _settings.AutoReplays = GUILayout.Toggle(_settings.AutoReplays, " Repeticiones automáticas de goles y paradas", UiKit.Label);
            _settings.ShowPerformance = GUILayout.Toggle(_settings.ShowPerformance, " Mostrar contador de FPS", UiKit.Label);
            GUILayout.Space(8f);
            GUILayout.Label("Partido", UiKit.H2);
            float[] halves = { 3f, 4f, 6f, 8f };
            int hi = Array.IndexOf(halves, _settings.HalfMinutes);
            hi = GUILayout.Toolbar(hi < 0 ? 1 : hi, new[] { "3 min", "4 min", "6 min", "8 min" }, UiKit.Button);
            _settings.HalfMinutes = halves[hi];
            GUILayout.Label("Duración de cada parte", UiKit.Small);
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(640f, 90f, 560f, UiKit.Height - 110f));
            GUILayout.Label("Controles táctiles", UiKit.H2);
            GUILayout.Label($"Tamaño de botones: {_settings.ButtonSize * 100f:F0} %", UiKit.Small);
            _settings.ButtonSize = GUILayout.HorizontalSlider(_settings.ButtonSize, 0.08f, 0.2f, GUILayout.Height(30f));
            GUILayout.Label($"Opacidad: {_settings.ButtonOpacity * 100f:F0} %", UiKit.Small);
            _settings.ButtonOpacity = GUILayout.HorizontalSlider(_settings.ButtonOpacity, 0.2f, 1f, GUILayout.Height(30f));
            _settings.LeftHanded = GUILayout.Toggle(_settings.LeftHanded, " Modo zurdo (botones a la izquierda)", UiKit.Label);
            GUILayout.Space(8f);
            GUILayout.Label("Teclado: WASD moverse · Mayús sprint · J pase/entrada/estirada · K tiro · L al hueco/salir · U centro · I pase elevado. Mando compatible.", UiKit.MutedLabel);
            GUILayout.Space(8f);
            GUILayout.Label("Informe de rendimiento (se añade al acabar cada partido):", UiKit.Small);
            GUILayout.Label(PerformanceMonitor.ReportPath, UiKit.MutedLabel);
            GUILayout.EndArea();
        }
    }
}
