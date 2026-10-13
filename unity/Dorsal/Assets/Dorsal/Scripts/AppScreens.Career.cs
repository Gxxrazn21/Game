using System;
using System.Linq;
using Dorsal.Core.Career;
using Dorsal.Core.Domain;
using Dorsal.Core.Localization;
using Dorsal.Core.World;
using UnityEngine;

namespace Dorsal.Unity
{
    /// <summary>Pantallas del modo carrera.</summary>
    public sealed partial class DorsalApp
    {
        private int _tableTab;

        private string ClubName(int id) => _career.World.Clubs.TryGetValue(id, out var c) ? c.Name : "?";

        private void TopBar(string title)
        {
            GUI.Label(new Rect(40f, 18f, 900f, 50f), title, UiKit.Title);
            var club = _career.UserClub;
            var league = club != null ? _career.World.LeagueOf(club.Id) : null;
            string sub = $"{_career.World.CurrentDate:dd/MM/yyyy} · Temporada {_career.World.SeasonYear}/{(_career.World.SeasonYear + 1) % 100:00}" +
                         (club != null ? $" · {club.Name} ({league?.Name})" : " · Sin club") + $" · Banco {Es.Money(_career.State.Bank)}";
            GUI.Label(new Rect(42f, 62f, 1200f, 30f), sub, UiKit.MutedLabel);
        }

        private bool BackToHub()
        {
            if (!Back()) return false;
            GoToCareerHome();
            return true;
        }

        // ---------------------------------------------------------------- elegir club

        private void DrawChooseClub()
        {
            TopBar(_career.Phase == CareerPhase.FreeAgent ? "Sin equipo: busca club" : "Elige tu primer club");
            if (_career.Phase != CareerPhase.ChoosingClub && _career.Phase != CareerPhase.FreeAgent) { GoToCareerHome(); return; }
            if (GUI.Button(new Rect(UiKit.Width - 190f, 18f, 170f, 52f), "Menú", UiKit.Button)) { Autosave(); Go(AppScreen.MainMenu); }
            float w = UiKit.Width;
            GUILayout.BeginArea(new Rect(40f, 100f, w * 0.5f - 60f, UiKit.Height - 120f));
            GUILayout.Label("Ofertas", UiKit.H2);
            var offers = _career.PendingOffers.ToList();
            if (offers.Count == 0) GUILayout.Label("Ninguna oferta por ahora. Pide una prueba a un club o avanza una semana.", UiKit.MutedLabel);
            foreach (var o in offers)
            {
                var club = _career.World.Clubs[o.ClubId];
                GUILayout.BeginVertical(UiKit.Card);
                GUILayout.Label($"{club.Name} · {_career.World.LeagueOf(club.Id)?.Name}", UiKit.H2);
                GUILayout.Label($"{Es.Offer(o.Kind)} · Rol: {Es.Role(o.Role)} · {Es.Money(o.WageWeekly)}/semana · {o.Years} temporada(s) · nivel del club {club.Level}", UiKit.Label);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Firmar", UiKit.AccentButton))
                {
                    _career.AcceptOffer(o.Id);
                    Autosave();
                    Toast($"¡Has firmado con {club.Name}!");
                    GoToCareerHome();
                }
                if (GUILayout.Button("Rechazar", UiKit.Button)) _career.RejectOffer(o.Id);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            if (_career.Phase == CareerPhase.FreeAgent && GUILayout.Button("Esperar una semana", UiKit.Button))
            {
                _career.AdvanceToNextMatchDay();
                _career.CompleteMatchDay(null);
                Autosave();
            }
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(w * 0.5f, 100f, w * 0.5f - 40f, UiKit.Height - 120f));
            GUILayout.Label("Pruebas", UiKit.H2);
            GUILayout.Label($"Tu media: {_career.User.Overall}. Los clubes valoran también tu potencial si eres joven.", UiKit.MutedLabel);
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var club in _career.TrialCandidates().Take(24))
            {
                GUILayout.BeginHorizontal(UiKit.Card);
                GUILayout.Label($"{club.Name}\n{_career.World.LeagueOf(club.Id)?.Name} · nivel {club.Level}", UiKit.Small);
                if (GUILayout.Button("Probar", UiKit.Button, GUILayout.Width(130f)))
                {
                    var r = _career.RequestTrial(club.Id);
                    Toast(r.Message);
                    Autosave();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- centro de carrera

        private void DrawHub()
        {
            var u = _career.User;
            TopBar(u.FullName);
            float w = UiKit.Width, colW = (w - 100f) / 3f;

            // Columna 1: jugador
            GUILayout.BeginArea(new Rect(40f, 100f, colW, 470f), UiKit.PanelStyle);
            var (pmin, pmax) = _career.UserPotentialRange();
            GUILayout.Label($"{Es.PositionLong(u.Position)} · {u.AgeOn(_career.World.CurrentDate)} años", UiKit.Label);
            GUILayout.Label($"MEDIA {u.Overall}", new GUIStyle(UiKit.Title) { fontSize = 40, normal = { textColor = UiKit.Accent } });
            GUILayout.Label($"Potencial estimado {pmin}-{pmax}", UiKit.MutedLabel);
            var contract = _career.UserContract;
            if (contract != null)
                GUILayout.Label($"{Es.Role(contract.Role)}{(contract.IsLoan ? " (cedido)" : "")} · contrato hasta {contract.EndDate.Year} · {Es.Money(contract.WageWeekly)}/sem", UiKit.Small);
            GUILayout.Space(6f);
            UiKit.LabeledBar("Forma física", u.Condition.Fitness / 100f, $"{u.Condition.Fitness:F0}", UiKit.Good, colW - 30f);
            UiKit.LabeledBar("Forma", (u.Condition.Form + 10f) / 20f, $"{u.Condition.Form:+0;-0;0}", UiKit.Accent, colW - 30f);
            UiKit.LabeledBar("Confianza míster", _career.State.CoachTrust / 100f, $"{_career.State.CoachTrust:F0}", new Color(0.4f, 0.7f, 1f), colW - 30f);
            UiKit.LabeledBar("Popularidad", _career.State.Popularity / 100f, $"{_career.State.Popularity:F0}", new Color(0.9f, 0.75f, 0.3f), colW - 30f);
            if (u.Injury != null) GUILayout.Label($"Lesionado: {u.Injury.Description} ({u.Injury.DaysRemaining} días)", new GUIStyle(UiKit.Label) { normal = { textColor = UiKit.Bad } });
            if (u.SuspensionMatches > 0) GUILayout.Label($"Sancionado: {u.SuspensionMatches} partido(s)", new GUIStyle(UiKit.Label) { normal = { textColor = UiKit.Bad } });
            var st = _career.World.SeasonStats.TryGetValue(u.Id, out var s) ? s : new PlayerSeasonStats();
            GUILayout.Label($"Temporada: {st.Appearances} PJ · {st.Goals} G · {st.Assists} A · nota {st.AverageRating:F2}", UiKit.Small);
            GUILayout.EndArea();

            // Columna 2: próximo partido y objetivos
            GUILayout.BeginArea(new Rect(50f + colW, 100f, colW, 470f), UiKit.PanelStyle);
            var next = _career.NextUserFixture();
            GUILayout.Label("Próximo partido", UiKit.H2);
            if (next != null)
            {
                bool home = next.HomeClubId == u.ClubId;
                int rival = home ? next.AwayClubId : next.HomeClubId;
                GUILayout.Label($"{(home ? "vs" : "en casa de")} {ClubName(rival)}", new GUIStyle(UiKit.Label) { fontSize = 22, fontStyle = FontStyle.Bold });
                GUILayout.Label($"Jornada {next.Round} · {next.Date:dd/MM} · rival {_career.LeaguePositionOf(rival)}º · tu club {_career.LeaguePositionOf(u.ClubId ?? 0)}º", UiKit.MutedLabel);
            }
            else GUILayout.Label("No hay partidos pendientes.", UiKit.MutedLabel);
            GUILayout.Space(6f);
            GUILayout.Label("Objetivos de la temporada", UiKit.H2);
            foreach (var o in _career.State.Objectives)
            {
                float prog = _career.ObjectiveProgress(o);
                bool done = o.Kind == ObjectiveKind.LeaguePosition ? prog > 0 && prog <= o.Target : prog >= o.Target;
                string val = o.Kind == ObjectiveKind.AverageRating ? prog.ToString("F2") : o.Kind == ObjectiveKind.LeaguePosition ? $"{prog:F0}º" : prog.ToString("F0");
                GUILayout.Label($"{(done ? "[OK]" : "[  ]")} {o.Description} ({val})", new GUIStyle(UiKit.Small) { normal = { textColor = done ? UiKit.Good : UiKit.Text } });
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Entrenamiento: {Es.Focus(_career.State.TrainingFocus)} · {Es.Intensity(_career.State.TrainingIntensity)}", UiKit.MutedLabel);
            GUILayout.EndArea();

            // Columna 3: noticias
            GUILayout.BeginArea(new Rect(60f + 2 * colW, 100f, colW, 470f), UiKit.PanelStyle);
            GUILayout.Label("Noticias", UiKit.H2);
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var n in _career.State.News.AsEnumerable().Reverse().Take(25))
                GUILayout.Label($"{n.Date:dd/MM} · {n.Text}", UiKit.Small);
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // Navegación
            int offersCount = _career.PendingOffers.Count();
            GUILayout.BeginArea(new Rect(40f, 590f, w - 80f, 120f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(next != null ? "Avanzar al partido" : "Avanzar", UiKit.AccentButton, GUILayout.Width(260f)))
            {
                if (_career.Phase == CareerPhase.InSeason || _career.Phase == CareerPhase.FreeAgent)
                {
                    _pendingInfo = _career.AdvanceToNextMatchDay();
                    Go(AppScreen.PreMatch);
                }
                else GoToCareerHome();
            }
            if (GUILayout.Button("Entrenar", UiKit.Button)) Go(AppScreen.Training);
            if (GUILayout.Button(offersCount > 0 ? $"Ofertas ({offersCount})" : "Mercado", offersCount > 0 ? UiKit.AccentButton : UiKit.Button)) Go(AppScreen.Offers);
            if (GUILayout.Button("Perfil", UiKit.Button)) Go(AppScreen.Profile);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Calendario", UiKit.Button)) Go(AppScreen.Calendar);
            if (GUILayout.Button("Clasificación", UiKit.Button)) Go(AppScreen.Table);
            if (GUILayout.Button("Historial", UiKit.Button)) Go(AppScreen.History);
            if (GUILayout.Button("Guardar", UiKit.Button)) { Autosave(); Toast("Partida guardada."); }
            if (GUILayout.Button("Ajustes", UiKit.Button)) { _returnFromSettings = AppScreen.Hub; Go(AppScreen.Settings); }
            if (GUILayout.Button("Menú", UiKit.Button)) { Autosave(); Go(AppScreen.MainMenu); }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- entrenamiento

        private void DrawTraining()
        {
            TopBar("Entrenamiento");
            if (BackToHub()) return;
            GUILayout.BeginArea(new Rect(40f, 110f, 760f, UiKit.Height - 130f));
            GUILayout.Label("Enfoque semanal", UiKit.H2);
            var focuses = (TrainingFocus[])Enum.GetValues(typeof(TrainingFocus));
            int fi = Array.IndexOf(focuses, _career.State.TrainingFocus);
            fi = GUILayout.SelectionGrid(fi, focuses.Select(Es.Focus).ToArray(), 4, UiKit.Button);
            GUILayout.Space(10f);
            GUILayout.Label("Intensidad", UiKit.H2);
            int ii = GUILayout.Toolbar((int)_career.State.TrainingIntensity, new[] { "Suave", "Normal", "Intensa" }, UiKit.Button);
            _career.SetTraining(focuses[fi], (TrainingIntensity)ii);
            GUILayout.Space(8f);
            GUILayout.Label("Intensa: más progresión y mejor nota ante el entrenador, pero más cansancio y riesgo de lesión. " +
                            "Suave: recuperas mejor antes de los partidos. El enfoque decide qué atributos crecen más; " +
                            "la mejora depende también de tu edad, tus minutos y tu potencial.", UiKit.MutedLabel);
            var grades = _career.State.WeekTrainingGrades;
            if (grades.Count > 0) GUILayout.Label("Notas de la última semana: " + string.Join("  ", grades.Select(g => g.ToString("F1"))), UiKit.Label);
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- calendario y clasificación

        private void DrawCalendar()
        {
            TopBar("Calendario");
            if (BackToHub()) return;
            int club = _career.User.ClubId ?? 0;
            GUILayout.BeginArea(new Rect(40f, 110f, UiKit.Width - 80f, UiKit.Height - 130f));
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var f in _career.World.Fixtures.Where(x => x.HomeClubId == club || x.AwayClubId == club).OrderBy(x => x.Round))
            {
                bool home = f.HomeClubId == club;
                string res = "";
                Color col = UiKit.Text;
                if (f.Result != null)
                {
                    int mine = home ? f.Result.HomeGoals : f.Result.AwayGoals, theirs = home ? f.Result.AwayGoals : f.Result.HomeGoals;
                    res = $"{f.Result.HomeGoals}-{f.Result.AwayGoals}  {(mine > theirs ? "V" : mine == theirs ? "E" : "D")}";
                    col = mine > theirs ? UiKit.Good : mine == theirs ? UiKit.Text : UiKit.Bad;
                }
                GUILayout.Label($"J{f.Round,2}  {f.Date:dd/MM}  {(home ? "Casa" : "Fuera"),-6} {ClubName(home ? f.AwayClubId : f.HomeClubId),-26} {res}",
                                new GUIStyle(UiKit.Label) { normal = { textColor = col } });
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawTable()
        {
            TopBar("Clasificación");
            if (BackToHub()) return;
            var comps = _career.World.Competitions.OrderBy(c => c.Tier).ToList();
            GUILayout.BeginArea(new Rect(40f, 100f, UiKit.Width - 80f, UiKit.Height - 110f));
            _tableTab = Mathf.Clamp(GUILayout.Toolbar(_tableTab, comps.Select(c => c.Name).ToArray(), UiKit.Button, GUILayout.Width(520f)), 0, comps.Count - 1);
            var comp = comps[_tableTab];
            var rows = _career.TableFor(comp);
            GUILayout.Label($"{"#",2}  {"Club",-28} {"PJ",3} {"G",3} {"E",3} {"P",3} {"GF",4} {"GC",4} {"DG",4} {"Pts",4}", UiKit.MutedLabel);
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                bool mine = r.ClubId == _career.User.ClubId;
                Color col = mine ? UiKit.Accent : i < comp.Rules.PromotionSlots ? UiKit.Good : i >= rows.Count - comp.Rules.RelegationSlots ? UiKit.Bad : UiKit.Text;
                GUILayout.Label($"{i + 1,2}  {ClubName(r.ClubId),-28} {r.Played,3} {r.Won,3} {r.Drawn,3} {r.Lost,3} {r.GoalsFor,4} {r.GoalsAgainst,4} {r.GoalDifference,4} {r.Points,4}",
                                new GUIStyle(UiKit.Label) { normal = { textColor = col }, fontStyle = mine ? FontStyle.Bold : FontStyle.Normal });
            }
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- mercado

        private void DrawOffers()
        {
            TopBar("Mercado y contratos");
            if (BackToHub()) return;
            GUILayout.BeginArea(new Rect(40f, 100f, UiKit.Width - 80f, UiKit.Height - 110f));
            var c = _career.UserContract;
            if (c != null)
            {
                long value = Economy.MarketValue(_career.User, _career.World.CurrentDate, c);
                GUILayout.Label($"Contrato: {ClubName(c.ClubId)}{(c.IsLoan ? $" (cedido por {ClubName(c.ParentClubId ?? 0)})" : "")} · {Es.Role(c.Role)} · {Es.Money(c.WageWeekly)}/sem · hasta 30/06/{c.EndDate.Year}" +
                                (c.ReleaseClause.HasValue ? $" · cláusula {Es.Money(c.ReleaseClause.Value)}" : "") + $" · valor de mercado {Es.Money(value)}", UiKit.Label);
                GUILayout.Label(_career.IsTransferWindowOpenNow ? "Mercado de fichajes ABIERTO" : "Mercado de fichajes cerrado (abre al inicio y a mitad de temporada)", UiKit.MutedLabel);
                if (GUILayout.Button(_career.State.TransferRequested ? "Retirar petición de salida" : "Pedir salir del club", UiKit.Button, GUILayout.Width(360f))) _career.ToggleTransferRequest();
            }
            GUILayout.Space(8f);
            GUILayout.Label("Ofertas recibidas", UiKit.H2);
            var offers = _career.PendingOffers.ToList();
            if (offers.Count == 0) GUILayout.Label("No tienes ofertas pendientes. Rinde bien y aparecerán.", UiKit.MutedLabel);
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var o in offers)
            {
                var club = _career.World.Clubs[o.ClubId];
                GUILayout.BeginVertical(UiKit.Card);
                GUILayout.Label($"{Es.Offer(o.Kind)}: {club.Name} ({_career.World.LeagueOf(club.Id)?.Name}, nivel {club.Level})", UiKit.H2);
                GUILayout.Label($"Rol {Es.Role(o.Role)} · {Es.Money(o.WageWeekly)}/semana · {o.Years} temporada(s)" + (o.Fee > 0 ? $" · traspaso {Es.Money(o.Fee)}" : "") +
                                $" · caduca {o.ExpiresOn:dd/MM}", UiKit.Label);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Aceptar", UiKit.AccentButton))
                {
                    _career.AcceptOffer(o.Id);
                    Autosave();
                    Toast($"Acuerdo cerrado con {club.Name}.");
                    GoToCareerHome();
                }
                if (o.Kind != OfferKind.Loan)
                {
                    if (GUILayout.Button("Pedir +10 %", UiKit.Button)) Toast(_career.Negotiate(o.Id, (long)(o.WageWeekly * 1.1)).Message);
                    if (GUILayout.Button("Pedir +25 %", UiKit.Button)) Toast(_career.Negotiate(o.Id, (long)(o.WageWeekly * 1.25)).Message);
                }
                if (GUILayout.Button("Rechazar", UiKit.Button)) _career.RejectOffer(o.Id);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- perfil e historial

        private void DrawProfile()
        {
            var u = _career.User;
            TopBar($"{u.FullName} · {Es.Position(u.Position)} · media {u.Overall}");
            if (BackToHub()) return;
            var groups = new (string name, AttributeId[] attrs)[]
            {
                ("Ritmo", new[] { AttributeId.Acceleration, AttributeId.SprintSpeed }),
                ("Tiro", new[] { AttributeId.Finishing, AttributeId.ShotPower, AttributeId.LongShots, AttributeId.Positioning }),
                ("Pase", new[] { AttributeId.ShortPassing, AttributeId.LongPassing, AttributeId.Crossing, AttributeId.Vision }),
                ("Regate", new[] { AttributeId.Dribbling, AttributeId.BallControl, AttributeId.Balance, AttributeId.Agility }),
                ("Defensa", new[] { AttributeId.Marking, AttributeId.Tackling, AttributeId.DefensiveAwareness }),
                ("Físico", new[] { AttributeId.Strength, AttributeId.Stamina, AttributeId.Jumping }),
                ("Mental", new[] { AttributeId.Reactions, AttributeId.Composure, AttributeId.Aggression, AttributeId.Heading }),
                ("Portería", new[] { AttributeId.GkDiving, AttributeId.GkHandling, AttributeId.GkKicking, AttributeId.GkPositioning, AttributeId.GkReflexes }),
            };
            float colW = (UiKit.Width - 120f) / 4f;
            for (int g = 0; g < groups.Length; g++)
            {
                var rect = new Rect(40f + (g % 4) * (colW + 13f), 105f + (g / 4) * 270f, colW, 255f);
                GUILayout.BeginArea(rect, UiKit.PanelStyle);
                GUILayout.Label(groups[g].name, UiKit.H2);
                foreach (var a in groups[g].attrs)
                {
                    int v = u.Attributes[a];
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(Es.Attribute(a), UiKit.Small, GUILayout.Width(colW - 90f));
                    GUILayout.Label(v.ToString(), new GUIStyle(UiKit.Label) { fontStyle = FontStyle.Bold, normal = { textColor = UiKit.AttributeColor(v) } });
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndArea();
            }
            var p = u.Personality;
            GUI.Label(new Rect(40f, 650f, UiKit.Width - 80f, 30f),
                $"Personalidad: profesionalidad {p.Professionalism}, ambición {p.Ambition}, temperamento {p.Temperament}, lealtad {p.Loyalty} · " +
                $"{u.HeightCm} cm · pie {(u.PreferredFoot == Foot.Right ? "derecho" : "izquierdo")} (débil {u.WeakFoot}★) · {Es.Nationality(u.Nationality)}", UiKit.MutedLabel);
        }

        private void DrawHistory()
        {
            TopBar("Historial y logros");
            if (BackToHub()) return;
            DrawCareerRecord(110f);
        }

        private void DrawCareerRecord(float top)
        {
            var s = _career.State;
            GUILayout.BeginArea(new Rect(40f, top, UiKit.Width * 0.62f, UiKit.Height - top - 20f));
            GUILayout.Label($"Total: {s.TotalAppearances} partidos · {s.TotalGoals} goles · {s.TotalAssists} asistencias", UiKit.H2);
            GUILayout.Label($"{"Temp.",-8} {"Club",-24} {"PJ",3} {"G",3} {"A",3} {"Nota",5} {"Pos",4} {"Media",5}", UiKit.MutedLabel);
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var r in s.History.AsEnumerable().Reverse())
                GUILayout.Label($"{r.SeasonYear}/{(r.SeasonYear + 1) % 100:00}  {r.ClubName + (r.OnLoan ? "*" : ""),-24} {r.Appearances,3} {r.Goals,3} {r.Assists,3} {r.AverageRating,5:F2} {r.LeaguePosition,4} {r.OverallAtEnd,5}" +
                                (r.Awards.Count > 0 ? "  " + string.Join(", ", r.Awards) : ""), UiKit.Small);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(UiKit.Width * 0.62f + 60f, top, UiKit.Width * 0.38f - 100f, UiKit.Height - top - 20f), UiKit.PanelStyle);
            GUILayout.Label("Logros", UiKit.H2);
            foreach (var a in s.Achievements) GUILayout.Label("• " + a, UiKit.Small);
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- jornada

        private void DrawPreMatch()
        {
            TopBar("Día de partido");
            var info = _pendingInfo;
            var f = info?.Fixture;
            GUILayout.BeginArea(new Rect(40f, 110f, UiKit.Width - 80f, UiKit.Height - 130f));
            if (f == null)
            {
                GUILayout.Label("Tu situación no te permite jugar esta jornada.", UiKit.H2);
                if (GUILayout.Button("Continuar", UiKit.AccentButton, GUILayout.Width(300f)))
                {
                    _lastSummary = _career.CompleteMatchDay(null);
                    Autosave();
                    Go(_lastSummary.SeasonFinished ? AppScreen.SeasonEnd : AppScreen.Hub);
                    if (_career.Phase == CareerPhase.FreeAgent) Go(AppScreen.ChooseClub);
                }
                GUILayout.EndArea();
                return;
            }
            GUILayout.Label($"{ClubName(f.HomeClubId)}  vs  {ClubName(f.AwayClubId)}", new GUIStyle(UiKit.Title) { fontSize = 38 });
            GUILayout.Label($"Jornada {f.Round} · {f.Date:dddd dd/MM}", UiKit.MutedLabel);
            GUILayout.Space(10f);
            var role = info.Role;
            GUILayout.Label($"Decisión del entrenador: {Es.MatchRole(role)}", new GUIStyle(UiKit.H2) { normal = { textColor = role == MatchRole.Starter ? UiKit.Good : role == MatchRole.Substitute ? UiKit.Accent : UiKit.Bad } });
            if (!string.IsNullOrEmpty(info.Objective)) GUILayout.Label("Objetivo: " + info.Objective, UiKit.Label);
            if (!string.IsNullOrEmpty(info.Instruction)) GUILayout.Label("Instrucción táctica: " + info.Instruction, UiKit.Label);
            if (info.TrainingGrades.Count > 0) GUILayout.Label("Entrenamientos de la semana: " + string.Join("  ", info.TrainingGrades.Select(g => g.ToString("F1"))), UiKit.MutedLabel);
            foreach (var e in info.Events) GUILayout.Label(e, new GUIStyle(UiKit.Label) { normal = { textColor = UiKit.Bad } });
            GUILayout.Space(16f);
            GUILayout.BeginHorizontal();
            bool canPlay = role == MatchRole.Starter || role == MatchRole.Substitute;
            if (canPlay && GUILayout.Button(role == MatchRole.Starter ? "Jugar el partido" : "Ir al banquillo (jugar si sales)", UiKit.AccentButton, GUILayout.Width(380f))) StartUserMatch(watch: false);
            if (GUILayout.Button("Ver partido", UiKit.Button, GUILayout.Width(240f))) StartUserMatch(watch: true);
            if (GUILayout.Button("Simular", UiKit.Button, GUILayout.Width(200f))) SimulateUserMatch();
            GUILayout.EndHorizontal();
            if (_career.User.Position == Position.GK && canPlay)
                GUILayout.Label("Modo portero: el portero se coloca solo; tú decides ESTIRADA (dirección con el joystick) y SALIR. Lee el disparo y elige bien el lado.", UiKit.MutedLabel);
            GUILayout.EndArea();
        }

        private void DrawPostMatch()
        {
            TopBar("Final del partido");
            var sm = _lastSummary;
            GUILayout.BeginArea(new Rect(40f, 110f, UiKit.Width - 80f, UiKit.Height - 130f));
            if (sm?.Report != null)
                GUILayout.Label($"{ClubName(sm.Report.HomeClubId)} {sm.Report.HomeGoals} - {sm.Report.AwayGoals} {ClubName(sm.Report.AwayClubId)}", new GUIStyle(UiKit.Title) { fontSize = 38 });
            var us = sm?.UserStats;
            if (us != null && us.MinutesPlayed > 0)
            {
                float rating = us.Rating ?? 0f;
                GUILayout.Label(us.Rating.HasValue ? $"Tu nota: {rating:F1}" : "Sin nota (pocos minutos)", new GUIStyle(UiKit.Title) { normal = { textColor = UiKit.RatingColor(rating) } });
                GUILayout.Label(us.Position == Position.GK
                    ? $"{us.MinutesPlayed}' · {us.Saves} paradas · {us.GoalsConceded} goles encajados · pases {us.PassesCompleted}/{us.PassesAttempted}"
                    : $"{us.MinutesPlayed}' · {us.Goals} goles · {us.Assists} asistencias · {us.Shots} tiros · pases {us.PassesCompleted}/{us.PassesAttempted} · {us.TacklesWon} entradas", UiKit.Label);
            }
            else GUILayout.Label($"No jugaste ({Es.MatchRole(sm?.Role ?? MatchRole.NotSelected)}).", UiKit.H2);
            if (sm != null)
            {
                float d = sm.TrustAfter - sm.TrustBefore;
                GUILayout.Label($"Confianza del entrenador: {sm.TrustBefore:F0} → {sm.TrustAfter:F0} ({d:+0.0;-0.0;0})", new GUIStyle(UiKit.Label) { normal = { textColor = d >= 0 ? UiKit.Good : UiKit.Bad } });
                if (!string.IsNullOrEmpty(sm.Objective)) GUILayout.Label($"Objetivo: {sm.Objective} → {(sm.ObjectiveMet ? "CUMPLIDO" : "no cumplido")}", UiKit.Label);
                GUILayout.Label($"El entrenador: \"{sm.CoachComment}\"", UiKit.MutedLabel);
                GUILayout.Label($"Ingresos: {Es.Money(sm.Earnings)} · Experiencia +{sm.ExperienceGained}", UiKit.MutedLabel);
            }
            GUILayout.Space(16f);
            if (GUILayout.Button("Continuar", UiKit.AccentButton, GUILayout.Width(300f)))
            {
                if (sm != null && sm.SeasonFinished) Go(AppScreen.SeasonEnd);
                else GoToCareerHome();
            }
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- temporada

        private void DrawSeasonEnd()
        {
            TopBar("Fin de temporada");
            var summary = _career.LastSeasonSummary;
            var rec = summary?.UserRecord ?? _career.State.History.LastOrDefault();
            GUILayout.BeginArea(new Rect(40f, 110f, UiKit.Width * 0.55f, UiKit.Height - 130f));
            if (rec != null)
            {
                GUILayout.Label($"{rec.ClubName}: {rec.LeaguePosition}º en {rec.LeagueName}", UiKit.H2);
                GUILayout.Label($"{rec.Appearances} partidos ({rec.Starts} titular) · {rec.Goals} goles · {rec.Assists} asistencias · nota media {rec.AverageRating:F2}", UiKit.Label);
                GUILayout.Label($"Objetivos cumplidos: {rec.ObjectivesCompleted}/{rec.ObjectivesTotal} · media actual {rec.OverallAtEnd}", UiKit.Label);
                foreach (var a in rec.Awards) GUILayout.Label("Premio: " + a, new GUIStyle(UiKit.H2) { normal = { textColor = new Color(1f, 0.85f, 0.3f) } });
            }
            if (summary != null)
            {
                GUILayout.Space(8f);
                if (summary.Promoted.Count > 0) GUILayout.Label("Ascienden: " + string.Join(", ", summary.Promoted.Select(ClubName)), new GUIStyle(UiKit.Label) { normal = { textColor = UiKit.Good } });
                if (summary.Relegated.Count > 0) GUILayout.Label("Descienden: " + string.Join(", ", summary.Relegated.Select(ClubName)), new GUIStyle(UiKit.Label) { normal = { textColor = UiKit.Bad } });
            }
            GUILayout.Space(16f);
            if (_career.Phase == CareerPhase.SeasonEnded)
            {
                if (GUILayout.Button($"Empezar la temporada {_career.World.SeasonYear + 1}/{(_career.World.SeasonYear + 2) % 100:00}", UiKit.AccentButton, GUILayout.Width(460f)))
                {
                    _career.StartNextSeason();
                    Autosave();
                    GoToCareerHome();
                }
                if (_career.User.AgeOn(_career.World.CurrentDate) >= 33 && GUILayout.Button("Retirarme", UiKit.Button, GUILayout.Width(460f)))
                {
                    _career.Retire();
                    Autosave();
                    Go(AppScreen.Retired);
                }
            }
            else if (GUILayout.Button("Continuar", UiKit.AccentButton, GUILayout.Width(300f))) GoToCareerHome();
            GUILayout.EndArea();

            if (summary != null)
            {
                GUILayout.BeginArea(new Rect(UiKit.Width * 0.6f, 110f, UiKit.Width * 0.4f - 40f, UiKit.Height - 130f), UiKit.PanelStyle);
                GUILayout.Label("Premios de la temporada", UiKit.H2);
                foreach (var a in summary.Awards) GUILayout.Label($"{a.Name} ({a.Competition})", UiKit.Small);
                GUILayout.EndArea();
            }
        }

        private void DrawRetired()
        {
            TopBar($"{_career.User.FullName}: carrera terminada");
            if (GUI.Button(new Rect(UiKit.Width - 190f, 18f, 170f, 52f), "Menú", UiKit.Button)) Go(AppScreen.MainMenu);
            DrawCareerRecord(110f);
        }
    }
}
