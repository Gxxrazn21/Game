using System;
using System.Collections.Generic;
using System.Linq;
using Dorsal.Core.Random;

namespace Dorsal.Core.Match
{
    /// <summary>
    /// Motor de partido determinista de paso fijo. No depende de Unity: la capa de presentación
    /// llama a <see cref="Step"/> en su FixedUpdate, lee posiciones/eventos y envía la entrada del jugador.
    /// La misma clase sirve para el partido jugable, la vista desde el banquillo y la simulación rápida.
    /// </summary>
    public sealed partial class MatchEngine
    {
        public const string EngineVersion = "0.1.0";

        private readonly DeterministicRandom _rng;
        private readonly List<MatchAgent> _all = new List<MatchAgent>();
        private readonly List<MatchAgent> _onPitch = new List<MatchAgent>();
        private readonly List<MatchEvent> _events = new List<MatchEvent>();
        private readonly float _dt;
        private readonly float _fatigueScale;

        private MatchTeam? _possessionTeam;
        private MatchTeam _firstHalfKickoffTeam;
        private float _halfElapsed;
        private float _addedMinutes;
        private bool _addedComputed;
        private readonly float[] _stoppageMinutes = new float[2];
        private float _phaseTimer;
        private SetPieceType _setPieceType;
        private MatchTeam _setPieceTeam;
        private Vec2 _setPieceSpot;
        private MatchAgent? _setPieceTaker;
        private bool _nextKickNoOffside;
        private MatchTeam? _concedingTeam;
        private readonly List<(MatchAgent outAgent, MatchAgent inAgent)> _pendingSubs = new List<(MatchAgent, MatchAgent)>();
        private int _lastSubCheckMinute = -1;
        private float _looseBallStillTime;

        public MatchEngine(MatchTeamSetup home, MatchTeamSetup away, MatchConfig? config = null, ulong seed = 1)
        {
            Config = config ?? new MatchConfig();
            Config.Validate();
            ValidateSetup(home, "local");
            ValidateSetup(away, "visitante");
            _rng = new DeterministicRandom(seed);
            Seed = seed;
            _dt = 1f / Config.TickRate;
            _fatigueScale = 270f / Config.HalfDurationSeconds;

            Home = new MatchTeam(TeamSide.Home, home) { AttackDir = 1 };
            Away = new MatchTeam(TeamSide.Away, away) { AttackDir = -1 };
            CreateAgents(Home);
            CreateAgents(Away);
            RebuildOnPitch();

            Ball = new MatchBall();
            Half = 1;
            _firstHalfKickoffTeam = Home;
            _setPieceTeam = Home;
            PlaceForKickOff(Home);
            SetupSetPiece(SetPieceType.KickOff, Home, Vec2.Zero, announce: false);
            RaiseEvent(MatchEventType.KickOff, Home, 0, 0, Vec2.Zero);
        }

        public MatchConfig Config { get; }
        public MatchDiagnostics Diagnostics { get; } = new MatchDiagnostics();
        public ulong Seed { get; }
        public MatchTeam Home { get; }
        public MatchTeam Away { get; }
        public MatchBall Ball { get; }
        public MatchPhase Phase { get; private set; }
        public SetPieceType CurrentSetPiece => _setPieceType;
        public int Half { get; private set; }
        public int Tick { get; private set; }
        public bool IsFinished => Phase == MatchPhase.FullTime;
        public IReadOnlyList<MatchAgent> Agents => _all;
        public IReadOnlyList<MatchEvent> Events => _events;

        /// <summary>Jugador controlado por el usuario (null = todos por IA, p. ej. vista TV o simulación).</summary>
        public int? ControlledPlayerId { get; set; }

        public MatchAgent? ControlledAgent =>
            ControlledPlayerId.HasValue ? _all.FirstOrDefault(a => a.PlayerId == ControlledPlayerId.Value && a.OnPitch) : null;

        /// <summary>Minuto de juego dentro de la parte (0–45+), continuo.</summary>
        public float HalfMinute => _halfElapsed / Config.HalfDurationSeconds * 45f;

        /// <summary>Minuto mostrado en el marcador (0–90+).</summary>
        public float ClockMinute => HalfMinute + (Half == 2 ? 45f : 0f);

        /// <summary>Minutos añadidos anunciados en la parte actual (0 hasta que se anuncian).</summary>
        public int AddedMinutes => _addedComputed ? (int)_addedMinutes : 0;

        public MatchTeam? PossessionTeam => _possessionTeam;

        public event Action<MatchEvent>? EventRaised;

        /// <summary>Traza opcional para ajustar la IA (null en producción).</summary>
        public Action<string>? DebugLog { get; set; }

        private void Log(string msg) => DebugLog?.Invoke($"[{ClockMinute:F1}] {msg}");

        /// <summary>Avanza un paso fijo. La entrada se aplica al agente controlado si está en el campo.</summary>
        public void Step(PlayerInput? input = null)
        {
            if (IsFinished) return;
            Tick++;
            switch (Phase)
            {
                case MatchPhase.HalfTime:
                    StartSecondHalf();
                    return;
                case MatchPhase.GoalCelebration:
                    UpdateCelebration();
                    break;
                case MatchPhase.SetPiece:
                    UpdateSetPiece();
                    break;
                case MatchPhase.Playing:
                    PlayTick(input);
                    break;
            }
            AdvanceClock();
        }

        /// <summary>Simula hasta el final sin entrada humana y devuelve el informe.</summary>
        public MatchReport RunToEnd()
        {
            int guard = 0;
            int maxTicks = (int)(Config.HalfDurationSeconds * 2.5f * Config.TickRate) + 10000;
            while (!IsFinished && guard++ < maxTicks) Step();
            if (!IsFinished) throw new InvalidOperationException("El partido no terminó dentro del límite de pasos.");
            return BuildReport();
        }

        public int Score(TeamSide side) => side == TeamSide.Home ? Home.Goals : Away.Goals;

        public MatchTeam TeamOf(TeamSide side) => side == TeamSide.Home ? Home : Away;

        private MatchTeam Opponent(MatchTeam t) => t == Home ? Away : Home;

        private static void ValidateSetup(MatchTeamSetup s, string label)
        {
            if (s.Starters.Count != 11) throw new ArgumentException($"El equipo {label} necesita 11 titulares (tiene {s.Starters.Count}).");
            var ids = s.Starters.Concat(s.Bench).Select(p => p.Id).ToList();
            if (ids.Distinct().Count() != ids.Count) throw new ArgumentException($"El equipo {label} tiene jugadores repetidos.");
        }

        private void CreateAgents(MatchTeam team)
        {
            var slots = team.Formation.Slots;
            for (int i = 0; i < team.Setup.Starters.Count; i++)
            {
                var agent = new MatchAgent(_all.Count, team.Setup.Starters[i], team, i, slots[i].Position, Boost(team))
                {
                    OnPitch = true,
                    EverPlayed = true,
                };
                agent.Stats.Started = true;
                team.Agents.Add(agent);
                _all.Add(agent);
            }
            foreach (var p in team.Setup.Bench)
            {
                var agent = new MatchAgent(_all.Count, p, team, -1, p.Position, Boost(team));
                team.Agents.Add(agent);
                _all.Add(agent);
            }
        }

        private float Boost(MatchTeam team) => team.Side == TeamSide.Home ? Config.HomeAdvantage : 0f;

        private void RebuildOnPitch()
        {
            _onPitch.Clear();
            Home.OnPitchList.Clear();
            Away.OnPitchList.Clear();
            foreach (var a in _all)
            {
                if (!a.OnPitch) continue;
                _onPitch.Add(a);
                a.Team.OnPitchList.Add(a);
            }
        }

        private void RaiseEvent(MatchEventType type, MatchTeam team, int playerId, int otherId, Vec2 pos)
        {
            int minute = (int)Math.Floor(HalfMinute) + 1 + (Half == 2 ? 45 : 0);
            if (type == MatchEventType.KickOff) minute = Half == 2 ? 46 : 0;
            var e = new MatchEvent(minute, type, team.Side, playerId, otherId, pos) { Half = Half };
            _events.Add(e);
            EventRaised?.Invoke(e);
        }

        private void AdvanceClock()
        {
            if (Phase == MatchPhase.HalfTime || Phase == MatchPhase.FullTime) return;
            _halfElapsed += _dt;
            float minute = HalfMinute;

            if (!_addedComputed && minute >= 45f)
            {
                float s = _stoppageMinutes[Half - 1];
                _addedMinutes = Half == 1
                    ? Math.Max(1f, Math.Min(4f, (float)Math.Round(0.5f + s)))
                    : Math.Max(2f, Math.Min(8f, (float)Math.Round(1.5f + s)));
                _addedComputed = true;
            }

            if (_addedComputed && minute >= 45f + _addedMinutes)
            {
                // Un penalti señalado se lanza aunque se cumpla el tiempo.
                if (Phase == MatchPhase.SetPiece && _setPieceType == SetPieceType.Penalty) return;
                if (Ball.ShotBy != null) return; // dejar terminar la jugada del tiro en curso
                EndHalf();
            }
        }

        private void EndHalf()
        {
            UpdateMinutesPlayedSnapshot();
            if (Half == 1)
            {
                RaiseEvent(MatchEventType.HalfTime, Home, 0, 0, Vec2.Zero);
                Phase = MatchPhase.HalfTime;
            }
            else
            {
                RaiseEvent(MatchEventType.FullTime, Home, 0, 0, Vec2.Zero);
                Phase = MatchPhase.FullTime;
                FinalizeStats();
            }
        }

        private void StartSecondHalf()
        {
            Half = 2;
            _halfElapsed = 0f;
            _addedComputed = false;
            _addedMinutes = 0f;
            Home.AttackDir = -Home.AttackDir;
            Away.AttackDir = -Away.AttackDir;
            // Recuperación parcial en el descanso.
            foreach (var a in _all) a.Energy = Math.Min(1f, a.Energy + 0.08f);
            ExecutePendingSubstitutions();
            var kickoffTeam = Opponent(_firstHalfKickoffTeam);
            PlaceForKickOff(kickoffTeam);
            SetupSetPiece(SetPieceType.KickOff, kickoffTeam, Vec2.Zero, announce: false);
            RaiseEvent(MatchEventType.KickOff, kickoffTeam, 0, 0, Vec2.Zero);
        }

        private void PlayTick(PlayerInput? input)
        {
            if (Ball.Owner != null) _possessionTeam = Ball.Owner.Team;
            if (_possessionTeam != null) _possessionTeam.PossessionTicks++;

            UpdateAgentTimers();
            PrepareTickContext();

            var controlled = input.HasValue ? ControlledAgent : null;
            // Orden fijo de iteración: garantiza el determinismo.
            for (int i = 0; i < _onPitch.Count; i++)
            {
                var a = _onPitch[i];
                if (!a.OnPitch) continue;
                if (a == controlled) ApplyHumanInput(a, input!.Value);
                else AiTick(a);
                if (Phase != MatchPhase.Playing) break; // una acción provocó un balón parado
            }

            if (Phase != MatchPhase.Playing) return;
            MoveAgents();
            UpdateBall();
            ResolveBallContacts();
            if (Phase == MatchPhase.Playing) CheckBallOutOfPlay();
            if (Phase == MatchPhase.Playing) CheckStuckBall();
            CheckCoachDecisions();
        }

        private void UpdateAgentTimers()
        {
            foreach (var a in _onPitch)
            {
                a.ActionTime += _dt;
                if (a.ActionTime > 0.8f && a.CurrentAction != AgentAction.Celebrate) a.CurrentAction = AgentAction.None;
                if (a.TackleCooldown > 0f) a.TackleCooldown -= _dt;
                if (a.ControlCooldown > 0f) a.ControlCooldown -= _dt;
                if (a.StumbleTimer > 0f) a.StumbleTimer -= _dt;
                if (a.RunTimer > 0f) a.RunTimer -= _dt;
            }
            Ball.TimeSinceKick += _dt;
        }

        private void CheckStuckBall()
        {
            if (Ball.Owner == null && Ball.Velocity.Length < 0.2f) _looseBallStillTime += _dt;
            else _looseBallStillTime = 0f;
            if (_looseBallStillTime > 6f)
            {
                // Salvaguarda: si nadie recoge el balón, lo controla el más cercano.
                var nearest = _onPitch.OrderBy(a => Vec2.DistanceSq(a.Position, Ball.Position.XY)).ThenBy(a => a.Index).First();
                nearest.Position = Ball.Position.XY;
                GainPossession(nearest, fromPass: false);
                _looseBallStillTime = 0f;
            }
        }

        private void SetAction(MatchAgent a, AgentAction action)
        {
            a.CurrentAction = action;
            a.ActionTime = 0f;
        }

        /// <summary>Relativo al equipo: X positiva hacia la portería rival.</summary>
        private static Vec2 ToRel(MatchTeam t, Vec2 p) => new Vec2(p.X * t.AttackDir, p.Y * t.AttackDir);

        private static Vec2 FromRel(MatchTeam t, Vec2 rel) => new Vec2(rel.X * t.AttackDir, rel.Y * t.AttackDir);

        private static Vec2 OpponentGoal(MatchTeam t) => Pitch.GoalCenter(t.AttackDir);

        private static Vec2 OwnGoal(MatchTeam t) => Pitch.GoalCenter(-t.AttackDir);
    }
}
