using System;
using System.Collections.Generic;

namespace Dorsal.Core.Match
{
    /// <summary>IA de jugadores: posicionamiento, presión, desmarques y decisiones con balón.</summary>
    public sealed partial class MatchEngine
    {
        private const float PredictStep = 0.1f;
        private const int PredictSamples = 30;

        private readonly Vec3[] _ballPrediction = new Vec3[PredictSamples];
        private MatchAgent? _homeChaser, _awayChaser;
        private Vec2 _homeChasePoint, _awayChasePoint;
        private MatchAgent? _homePresser1, _homePresser2, _awayPresser1, _awayPresser2;
        private float _homeOffsideLineRel, _awayOffsideLineRel;

        private readonly List<(BallOption opt, float util, MatchAgent? target, Vec2 point, bool lofted)> _options =
            new List<(BallOption opt, float util, MatchAgent? target, Vec2 point, bool lofted)>(32);

        private enum BallOption
        {
            Shoot,
            Pass,
            ThroughPass,
            Dribble,
            Cross,
            Clear,
            Hold,
        }

        // ---------------------------------------------------------------- contexto por tick

        private void PrepareTickContext()
        {
            _homeOffsideLineRel = OffsideLineRel(Home);
            _awayOffsideLineRel = OffsideLineRel(Away);

            _homeChaser = _awayChaser = null;
            if (Ball.Owner == null)
            {
                PredictBall();
                (_homeChaser, _homeChasePoint) = FindChaser(Home);
                (_awayChaser, _awayChasePoint) = FindChaser(Away);
            }

            _homePresser1 = _homePresser2 = _awayPresser1 = _awayPresser2 = null;
            if (Ball.Owner != null && !Ball.Owner.HoldingInHands)
            {
                var carrier = Ball.Owner;
                var defTeam = Opponent(carrier.Team);
                var (p1, p2) = FindPressers(defTeam, carrier);
                if (defTeam == Home) { _homePresser1 = p1; _homePresser2 = p2; }
                else { _awayPresser1 = p1; _awayPresser2 = p2; }
            }
        }

        /// <summary>Línea de fuera de juego para los atacantes de <paramref name="attacking"/>, en coordenadas relativas.</summary>
        private float OffsideLineRel(MatchTeam attacking)
        {
            var def = Opponent(attacking);
            float last = float.MinValue, second = float.MinValue;
            foreach (var d in def.OnPitchList)
            {
                float x = ToRel(attacking, d.Position).X;
                if (x > last) { second = last; last = x; }
                else if (x > second) second = x;
            }
            float ballX = ToRel(attacking, Ball.Position.XY).X;
            return Math.Max(0f, Math.Max(second, ballX));
        }

        private float OffsideLine(MatchTeam attacking) => attacking == Home ? _homeOffsideLineRel : _awayOffsideLineRel;

        private void PredictBall()
        {
            var pos = Ball.Position;
            var vel = Ball.Velocity;
            const int sub = 2;
            float h = PredictStep / sub;
            for (int i = 0; i < PredictSamples; i++)
            {
                for (int s = 0; s < sub; s++) IntegrateBall(ref pos, ref vel, h);
                _ballPrediction[i] = pos;
            }
        }

        private (MatchAgent?, Vec2) FindChaser(MatchTeam team)
        {
            MatchAgent? best = null;
            Vec2 bestPoint = Ball.Position.XY;
            float bestT = float.MaxValue;
            foreach (var a in team.OnPitchList)
            {
                if (a.StumbleTimer > 0f) continue;
                float reaction = 0.15f + 0.25f * (1f - a.Reactions) + ReadDelayRemaining(a);
                float vmax = TopSpeed(a, sprint: true) * 0.92f;
                float reach = 0.7f;
                for (int i = 0; i < PredictSamples; i++)
                {
                    var p = _ballPrediction[i];
                    float t = (i + 1) * PredictStep;
                    if (p.Z > 2.3f) continue;
                    if (a.IsGoalkeeper && !Pitch.InPenaltyArea(p.XY, -team.AttackDir)) continue;
                    float d = Vec2.Distance(a.Position, p.XY) - reach;
                    if (d <= vmax * Math.Max(0f, t - reaction))
                    {
                        if (t < bestT - 1e-4f) { bestT = t; best = a; bestPoint = p.XY; }
                        break;
                    }
                }
            }
            if (best == null)
            {
                // Nadie llega en 3 s: el más cercano al punto final de la trayectoria.
                var end = _ballPrediction[PredictSamples - 1].XY;
                float bestD = float.MaxValue;
                foreach (var a in team.OnPitchList)
                {
                    if (a.IsGoalkeeper && !Pitch.InPenaltyArea(end, -team.AttackDir)) continue;
                    float d = Vec2.DistanceSq(a.Position, end);
                    if (d < bestD) { bestD = d; best = a; bestPoint = end; }
                }
            }
            return (best, ClampToPitch(bestPoint, 1f));
        }

        /// <summary>
        /// Tiempo que le queda a un rival para "leer" un pase recién golpeado. Sin este retardo los defensores
        /// reaccionarían en el mismo instante del golpeo y cortarían casi todo.
        /// </summary>
        private float ReadDelayRemaining(MatchAgent a)
        {
            if (Ball.Kicker == null || Ball.Kicker.Team == a.Team || Ball.PendingPassFrom == null) return 0f;
            float read = 0.3f + 0.35f * (1f - a.DefensiveAwareness) + 0.1f * (1f - a.Reactions);
            return Math.Max(0f, read - Ball.TimeSinceKick);
        }

        private (MatchAgent?, MatchAgent?) FindPressers(MatchTeam defTeam, MatchAgent carrier)
        {
            MatchAgent? p1 = null, p2 = null;
            float d1 = float.MaxValue, d2 = float.MaxValue;
            foreach (var a in defTeam.OnPitchList)
            {
                if (a.IsGoalkeeper) continue;
                float d = Vec2.DistanceSq(a.Position, carrier.Position);
                if (d < d1) { p2 = p1; d2 = d1; p1 = a; d1 = d; }
                else if (d < d2) { p2 = a; d2 = d; }
            }
            float carrierRelX = ToRel(defTeam, carrier.Position).X; // relativo al defensor: negativo = su campo
            bool secondPress = defTeam.Tactics.Pressing >= 60 || carrierRelX < -Pitch.HalfLength + 30f;
            return (p1, secondPress ? p2 : null);
        }

        // ---------------------------------------------------------------- tick de IA

        private void AiTick(MatchAgent a)
        {
            if (a.StumbleTimer > 0f)
            {
                a.WantsSprint = false;
                return;
            }

            a.DecisionTimer -= _dt;

            if (Ball.Owner == a)
            {
                if (a.HoldingInHands)
                {
                    a.Target = a.Position;
                    a.HoldTimer -= _dt;
                    if (a.HoldTimer <= 0f) GoalkeeperDistribute(a);
                    return;
                }
                if (a.DecisionTimer <= 0f) DecideWithBall(a);
                return;
            }

            // Portero en plena estirada: no decide nada más.
            if (a.DiveTimer >= 0f) return;

            // Persecución de balón suelto (actualizada cada tick).
            if (Ball.Owner == null)
            {
                var chaser = a.Team == Home ? _homeChaser : _awayChaser;
                if ((chaser == a && ReadDelayRemaining(a) <= 0f) || Ball.IntendedReceiver == a)
                {
                    a.Target = chaser == a ? (a.Team == Home ? _homeChasePoint : _awayChasePoint) : InterceptPointFor(a);
                    a.WantsSprint = true;
                    return;
                }
            }
            else if (Ball.Owner.Team != a.Team)
            {
                // Presión al poseedor.
                bool p1 = a == (a.Team == Home ? _homePresser1 : _awayPresser1);
                bool p2 = a == (a.Team == Home ? _homePresser2 : _awayPresser2);
                if (p1 || p2)
                {
                    PressCarrier(a, Ball.Owner, primary: p1);
                    return;
                }
            }

            if (a.DecisionTimer <= 0f)
            {
                DecideOffBall(a);
                a.DecisionTimer = 0.2f + 0.15f * (1f - a.Reactions) + (float)_rng.NextDouble() * 0.1f;
            }
        }

        private Vec2 InterceptPointFor(MatchAgent a)
        {
            float vmax = TopSpeed(a, sprint: true) * 0.92f;
            for (int i = 0; i < PredictSamples; i++)
            {
                var p = _ballPrediction[i];
                float t = (i + 1) * PredictStep;
                if (Vec2.Distance(a.Position, p.XY) - 0.7f <= vmax * t) return ClampToPitch(p.XY, 1f);
            }
            return ClampToPitch(_ballPrediction[PredictSamples - 1].XY, 1f);
        }

        private void PressCarrier(MatchAgent a, MatchAgent carrier, bool primary)
        {
            var ownGoal = OwnGoal(a.Team);
            if (primary)
            {
                a.Target = carrier.Position + carrier.Velocity * 0.25f + (ownGoal - carrier.Position).Normalized * 0.7f;
                a.WantsSprint = true;
                float d = Vec2.Distance(a.Position, carrier.Position);
                if (d < 1.7f && a.TackleCooldown <= 0f && carrier.ControlCooldown <= 0.25f)
                {
                    float pressing = a.Team.Tactics.Pressing / 100f;
                    float rate = 1.1f * (0.5f + pressing + 0.5f * a.Aggression);
                    if (Pitch.InPenaltyArea(carrier.Position, -a.Team.AttackDir)) rate *= 0.6f;
                    if (_rng.Chance(rate * _dt * 4f)) TryTackle(a, carrier);
                    else a.TackleCooldown = 0.25f; // sigue encimando sin entrar
                }
            }
            else
            {
                // Segundo presionador: cierra la línea de pase más cercana.
                MatchAgent? outlet = null;
                float best = float.MaxValue;
                foreach (var t in carrier.Team.OnPitchList)
                {
                    if (t == carrier || t.IsGoalkeeper) continue;
                    float d = Vec2.DistanceSq(t.Position, carrier.Position);
                    if (d < best) { best = d; outlet = t; }
                }
                a.Target = outlet == null ? carrier.Position : Vec2.Lerp(carrier.Position, outlet.Position, 0.45f);
                a.WantsSprint = Vec2.Distance(a.Position, a.Target) > 4f;
            }
        }

        private void DecideOffBall(MatchAgent a)
        {
            if (a.IsGoalkeeper)
            {
                GoalkeeperPositioning(a);
                return;
            }

            var team = a.Team;
            bool attacking = _possessionTeam == team;
            Vec2 target = FormationTarget(a, attacking);
            Vec2 rel = ToRel(team, target);

            if (attacking)
            {
                var carrier = Ball.Owner != null && Ball.Owner.Team == team ? Ball.Owner : null;
                float offsideLine = OffsideLine(team);
                if (a.RunTimer > 0f)
                {
                    target = a.RunTarget;
                    a.Target = target;
                    a.WantsSprint = true;
                    return;
                }
                if (carrier != null && CanMakeRun(a))
                {
                    float myRelX = ToRel(team, a.Position).X;
                    float carrierRelX = ToRel(team, carrier.Position).X;
                    float tempo = team.Tactics.Tempo / 100f;
                    float chance = (0.08f + 0.22f * a.Positioning) * (0.6f + 0.8f * tempo);
                    if (carrierRelX > -15f && myRelX <= offsideLine && myRelX > offsideLine - 12f && _rng.Chance(chance))
                    {
                        float runX = Math.Min(Pitch.HalfLength - 6f, offsideLine + _rng.Range(6f, 14f));
                        float runY = MathF2.Clamp(ToRel(team, a.Position).Y + _rng.Range(-8f, 8f), -Pitch.HalfWidth + 4f, Pitch.HalfWidth - 4f);
                        a.RunTarget = FromRel(team, new Vec2(runX, runY));
                        a.RunTimer = 2.2f + _rng.Range(0f, 0.8f);
                        a.Target = a.RunTarget;
                        a.WantsSprint = true;
                        return;
                    }
                }
                // Apoyo al poseedor si está lejos.
                if (carrier != null && Vec2.Distance(a.Position, carrier.Position) > 20f && IsAmongNearest(a, carrier, 3))
                    target = Vec2.Lerp(target, carrier.Position, 0.3f);

                // No quedarse en fuera de juego.
                rel = ToRel(team, target);
                if (rel.X > offsideLine - 0.6f) rel.X = offsideLine - 0.6f;
                target = FromRel(team, rel);
            }
            else
            {
                // Marcaje zonal: vigilar al atacante más próximo a mi zona.
                MatchAgent? mark = null;
                float best = 12f * 12f;
                foreach (var o in Opponent(team).OnPitchList)
                {
                    if (o.IsGoalkeeper) continue;
                    float d = Vec2.DistanceSq(o.Position, target);
                    if (d < best) { best = d; mark = o; }
                }
                if (mark != null)
                {
                    var goalSide = mark.Position + (OwnGoal(team) - mark.Position).Normalized * 1.6f;
                    target = Vec2.Lerp(target, goalSide, 0.35f + 0.35f * a.Marking);
                }
            }

            a.Target = ClampToPitch(target, 0.5f);
            a.WantsSprint = Vec2.Distance(a.Position, a.Target) > 9f;
        }

        private static bool CanMakeRun(MatchAgent a)
        {
            switch (a.Role)
            {
                case Domain.Position.ST:
                case Domain.Position.LW:
                case Domain.Position.RW:
                case Domain.Position.CAM:
                case Domain.Position.LM:
                case Domain.Position.RM:
                    return true;
                default:
                    return false;
            }
        }

        private bool IsAmongNearest(MatchAgent a, MatchAgent to, int n)
        {
            float d = Vec2.DistanceSq(a.Position, to.Position);
            int closer = 0;
            foreach (var t in a.Team.OnPitchList)
            {
                if (t == a || t == to || t.IsGoalkeeper) continue;
                if (Vec2.DistanceSq(t.Position, to.Position) < d) closer++;
            }
            return closer < n;
        }

        /// <summary>Posición de la formación desplazada según balón, fase y tácticas.</summary>
        private Vec2 FormationTarget(MatchAgent a, bool attacking)
        {
            var team = a.Team;
            var slot = team.Formation.Slots[Math.Max(0, a.SlotIndex)];
            Vec2 ballRel = ToRel(team, Ball.Position.XY);
            float line = (team.Tactics.DefensiveLine - 50) / 50f;
            float width01 = team.Tactics.Width / 100f;

            float anchor, depth, width, ballShiftY;
            if (attacking)
            {
                anchor = MathF2.Clamp(ballRel.X * 0.65f + 4f + 6f * line, -20f, 28f);
                depth = 19f;
                width = Pitch.HalfWidth * (0.7f + 0.25f * width01);
                ballShiftY = 0.2f;
            }
            else
            {
                anchor = MathF2.Clamp(ballRel.X * 0.55f - 6f + 8f * line, -30f, 16f);
                depth = 14f;
                width = 20f + 6f * width01;
                ballShiftY = 0.35f;
            }

            float x = anchor + slot.Depth * depth;
            x = MathF2.Clamp(x, -Pitch.HalfLength + 5f, Pitch.HalfLength - 4f);
            float y = slot.Lateral * width + ballRel.Y * ballShiftY;
            y = MathF2.Clamp(y, -Pitch.HalfWidth + 1.5f, Pitch.HalfWidth - 1.5f);
            return FromRel(team, new Vec2(x, y));
        }

        private void GoalkeeperPositioning(MatchAgent gk)
        {
            var goal = OwnGoal(gk.Team);
            var ball = Ball.Position.XY;
            float dist = Vec2.Distance(goal, ball);
            float depth = 1.0f + MathF2.Clamp(dist * 0.09f, 0f, 9f) * (0.7f + 0.3f * gk.GkPositioning);
            var dir = (ball - goal).Normalized;
            if (dir.LengthSq < 0.5f) dir = new Vec2(gk.Team.AttackDir, 0f);
            gk.Target = goal + dir * depth;
            gk.WantsSprint = Vec2.Distance(gk.Position, gk.Target) > 6f;
        }

        // ---------------------------------------------------------------- decisiones con balón

        private void DecideWithBall(MatchAgent a)
        {
            var team = a.Team;
            Vec2 rel = ToRel(team, a.Position);
            float pressure = Pressure(a);
            var opts = _options;
            opts.Clear();

            // Tiro
            float goalDist = Vec2.Distance(a.Position, OpponentGoal(team));
            if (goalDist < 33f && !a.IsGoalkeeper)
            {
                float skill = goalDist > 20f ? MathF2.Lerp(a.Finishing, a.LongShots, 0.7f) : a.Finishing;
                float xg = ShotQuality(team, a.Position) * (0.55f + 0.6f * skill) * (1f - 0.35f * pressure);
                float util = xg * 7.5f + (goalDist < 20f ? 0.15f : 0f);
                opts.Add((BallOption.Shoot, util, null, Vec2.Zero, false));
            }

            // Pases
            float threatHere = Threat(rel);
            float lossCost = 0.25f + 0.4f * MathF2.Clamp01((Pitch.HalfLength * 0.4f - rel.X) / Pitch.HalfLength);
            float maxPass = 30f + 25f * a.LongPassing;
            foreach (var t in team.OnPitchList)
            {
                if (t == a) continue;
                if (t.IsGoalkeeper && (rel.X > -Pitch.HalfLength + 30f || pressure < 0.4f)) continue;
                if (IsOffsideFlaggedNow(t) && _rng.Chance(0.4f + 0.55f * a.Vision)) continue;
                float lead = Vec2.Distance(a.Position, t.Position) / 16f;
                Vec2 point = ClampToPitch(t.Position + t.Velocity * Math.Min(lead, 0.8f), 1.5f);
                float d = Vec2.Distance(a.Position, point);
                if (d < 4f || d > maxPass) continue;
                bool lofted = d > 32f || (d > 16f && LaneRisk(a, point, lofted: false) > 0.35f);
                float succ = PassSuccessEstimate(a, point, lofted, pressure);
                Vec2 pRel = ToRel(team, point);
                float gain = Threat(pRel) - threatHere;
                float space = MathF2.Clamp01(NearestOpponentDistance(team, point) / 8f);
                if (succ < 0.4f) continue;
                float value = succ * succ * (0.24f + gain * 2.4f + 0.2f * space) - (1f - succ) * lossCost;
                if (pRel.X < rel.X - 5f) value -= 0.05f * (1f - pressure); // sin presión, preferir progresar
                opts.Add((BallOption.Pass, value, t, point, lofted));
            }

            // Pase al hueco a quien está desmarcándose
            foreach (var t in team.OnPitchList)
            {
                if (t == a || t.RunTimer <= 0f) continue;
                Vec2 point = ClampToPitch(Vec2.Lerp(t.Position, t.RunTarget, 0.7f), 2f);
                float d = Vec2.Distance(a.Position, point);
                if (d < 8f || d > maxPass) continue;
                float succ = PassSuccessEstimate(a, point, d > 28f, pressure) * (0.65f + 0.35f * a.Vision);
                float gain = Threat(ToRel(team, point)) - threatHere;
                if (succ < 0.35f) continue;
                float value = succ * succ * (0.3f + gain * 3.0f) - (1f - succ) * lossCost;
                opts.Add((BallOption.ThroughPass, value, t, point, d > 28f));
            }

            // Conducción
            if (!a.IsGoalkeeper)
            {
                Vec2 fwd = (OpponentGoal(team) - a.Position).Normalized;
                Vec2 drib = ClampToPitch(a.Position + fwd * 7f, 2f);
                float spaceAhead = NearestOpponentDistance(team, a.Position + fwd * 4f);
                float gain = Threat(ToRel(team, drib)) - threatHere;
                float util = (0.2f + 0.55f * a.Dribbling) * (1f - 0.7f * pressure) * (0.25f + gain * 2.5f + 0.04f * Math.Min(spaceAhead, 10f));
                opts.Add((BallOption.Dribble, util, null, drib, false));
            }

            // Centro
            if (rel.X > Pitch.HalfLength - 24f && Math.Abs(rel.Y) > 13f && !a.IsGoalkeeper)
            {
                int inBox = 0;
                foreach (var t in team.OnPitchList)
                    if (t != a && Pitch.InPenaltyArea(t.Position, team.AttackDir)) inBox++;
                if (inBox > 0) opts.Add((BallOption.Cross, 0.2f + 0.12f * inBox * (0.5f + a.Crossing), null, Vec2.Zero, true));
            }

            // Despeje
            if (rel.X < -Pitch.HalfLength + 22f && pressure > 0.55f)
                opts.Add((BallOption.Clear, 0.45f * pressure, null, Vec2.Zero, true));

            // Proteger
            opts.Add((BallOption.Hold, 0.1f, null, Vec2.Zero, false));

            // Elección: la mejor con algo de ruido (menos ruido con más visión).
            float noise = 0.04f + 0.12f * (1f - a.Vision) + 0.06f * pressure;
            int bestIdx = 0;
            float bestScore = float.MinValue;
            for (int i = 0; i < opts.Count; i++)
            {
                float s = opts[i].util + (float)_rng.Gaussian(0, noise);
                if (s > bestScore) { bestScore = s; bestIdx = i; }
            }
            var choice = opts[bestIdx];
            if (DebugLog != null && rel.X > 15f)
            {
                float shootU = -9f, bestPass = -9f, drib = -9f;
                foreach (var o in opts)
                {
                    if (o.opt == BallOption.Shoot) shootU = o.util;
                    else if (o.opt == BallOption.Pass || o.opt == BallOption.ThroughPass) bestPass = Math.Max(bestPass, o.util);
                    else if (o.opt == BallOption.Dribble) drib = o.util;
                }
                Log($"DECIDE {a} relX={rel.X:F0} y={rel.Y:F0} press={pressure:F2} -> {choice.opt} (shoot={shootU:F2} pass={bestPass:F2} drib={drib:F2})");
            }

            switch (choice.opt)
            {
                case BallOption.Shoot:
                    Shoot(a, power: 0.8f + 0.2f * (float)_rng.NextDouble());
                    break;
                case BallOption.Pass:
                    if (DebugLog != null) Log($"PASS {a} -> {choice.target} d={Vec2.Distance(a.Position, choice.point):F1} lofted={choice.lofted} est={PassSuccessEstimate(a, choice.point, choice.lofted, pressure):F2} risk={LaneRisk(a, choice.point, choice.lofted):F2} press={pressure:F2} util={choice.util:F2} relX={rel.X:F0}");
                    KickPass(a, choice.point, choice.lofted, choice.target, choice.lofted ? KickKind.LoftedPass : KickKind.Pass);
                    break;
                case BallOption.ThroughPass:
                    KickPass(a, choice.point, choice.lofted, choice.target, KickKind.ThroughPass);
                    break;
                case BallOption.Cross:
                    Cross(a);
                    break;
                case BallOption.Clear:
                    Clear(a);
                    break;
                case BallOption.Dribble:
                    a.Target = choice.point;
                    a.WantsSprint = NearestOpponentDistance(team, a.Position) > 3.5f;
                    a.DecisionTimer = 0.3f + 0.2f * (float)_rng.NextDouble();
                    break;
                default:
                    // Proteger el balón: alejarse del rival más cercano.
                    var away = a.Position - NearestOpponentPosition(team, a.Position);
                    a.Target = ClampToPitch(a.Position + away.Normalized * 2.5f, 2f);
                    a.WantsSprint = false;
                    a.DecisionTimer = 0.35f;
                    break;
            }
        }

        // ---------------------------------------------------------------- evaluación

        /// <summary>Presión sobre un jugador (0–1) por rivales cercanos.</summary>
        private float Pressure(MatchAgent a)
        {
            float p = 0f;
            foreach (var o in Opponent(a.Team).OnPitchList)
            {
                float d = Vec2.Distance(o.Position, a.Position);
                if (d < 6f) p += 1f - d / 6f;
            }
            return MathF2.Clamp01(p);
        }

        /// <summary>Peligro de una posición (coordenadas relativas), 0–~1.2.</summary>
        private static float Threat(Vec2 rel)
        {
            float x01 = MathF2.Clamp01((rel.X + Pitch.HalfLength) / (2f * Pitch.HalfLength));
            float central = 1f - MathF2.Clamp01(Math.Abs(rel.Y) / Pitch.HalfWidth);
            float t = (float)Math.Pow(x01, 2.2) * (0.55f + 0.45f * central);
            if (rel.X > Pitch.HalfLength - Pitch.PenaltyAreaDepth && Math.Abs(rel.Y) < Pitch.PenaltyAreaHalfWidth) t += 0.15f * central;
            return t;
        }

        /// <summary>Estimación de la probabilidad de gol de un tiro desde <paramref name="pos"/> (para decidir, no para resolver).</summary>
        private static float ShotQuality(MatchTeam team, Vec2 pos)
        {
            var goal = OpponentGoal(team);
            float d = Vec2.Distance(pos, goal);
            var post1 = new Vec2(goal.X, Pitch.GoalHalfWidth);
            var post2 = new Vec2(goal.X, -Pitch.GoalHalfWidth);
            var v1 = (post1 - pos).Normalized;
            var v2 = (post2 - pos).Normalized;
            float angle = (float)Math.Acos(MathF2.Clamp(Vec2.Dot(v1, v2), -1f, 1f));
            float angleF = MathF2.Clamp(angle / 0.64f, 0f, 1.4f);
            return MathF2.Clamp(0.95f * (float)Math.Exp(-d / 7f) * (float)Math.Sqrt(angleF), 0.003f, 0.75f);
        }

        private float LaneRisk(MatchAgent from, Vec2 target, bool lofted)
        {
            float risk = 0f;
            float ballSpeed = lofted ? 15f : 10f;
            foreach (var o in Opponent(from.Team).OnPitchList)
            {
                float d = Vec2.DistanceToSegment(o.Position, from.Position, target, out float t);
                if (lofted && t < 0.82f) continue; // un pase elevado solo se corta cerca del destino
                float tb = Vec2.Distance(from.Position, from.Position + (target - from.Position) * t) / ballSpeed;
                float to = d / 7.5f + 0.25f;
                if (to < tb) risk += 0.55f * (1f - to / Math.Max(tb, 0.01f));
                else risk += 0.12f * (float)Math.Exp(-d / 1.8f);
            }
            return risk;
        }

        private float PassSuccessEstimate(MatchAgent from, Vec2 target, bool lofted, float pressure)
        {
            float d = Vec2.Distance(from.Position, target);
            float skill = lofted ? from.LongPassing : (d > 25f ? MathF2.Lerp(from.ShortPassing, from.LongPassing, 0.5f) : from.ShortPassing);
            float risk = LaneRisk(from, target, lofted);
            float skillErr = (1f - skill) * 0.35f * (d / 30f);
            return MathF2.Clamp(1f - risk - skillErr - pressure * 0.15f, 0.02f, 0.98f);
        }

        private float NearestOpponentDistance(MatchTeam team, Vec2 p)
        {
            float best = float.MaxValue;
            foreach (var o in Opponent(team).OnPitchList)
            {
                float d = Vec2.Distance(o.Position, p);
                if (d < best) best = d;
            }
            return best;
        }

        private Vec2 NearestOpponentPosition(MatchTeam team, Vec2 p)
        {
            float best = float.MaxValue;
            Vec2 pos = p + new Vec2(-team.AttackDir, 0f);
            foreach (var o in Opponent(team).OnPitchList)
            {
                float d = Vec2.DistanceSq(o.Position, p);
                if (d < best) { best = d; pos = o.Position; }
            }
            return pos;
        }

        /// <summary>¿Estaría en fuera de juego si recibiera un pase ahora?</summary>
        private bool IsOffsideFlaggedNow(MatchAgent t)
        {
            if (!Config.EnableOffside) return false;
            float x = ToRel(t.Team, t.Position).X;
            return x > 0f && x > OffsideLine(t.Team) + 0.2f;
        }

        // ---------------------------------------------------------------- control humano

        private void ApplyHumanInput(MatchAgent a, PlayerInput input)
        {
            if (a.DiveTimer >= 0f) return; // durante la estirada no hay control
            var move = input.Move.ClampLength(1f);
            a.Target = a.Position + move * 6f;
            a.WantsSprint = input.Sprint && move.LengthSq > 0.01f;
            if (move.LengthSq < 0.0025f) a.Target = a.Position;

            if (a.IsGoalkeeper && Ball.Owner != a)
            {
                if (a.RushTimer > 0f)
                {
                    a.RushTimer -= _dt;
                    a.Target = Ball.Owner == null ? InterceptPointFor(a) : Ball.Owner.Position;
                    a.WantsSprint = true;
                }
                else if (move.LengthSq < 0.0025f)
                {
                    // Colocación asistida: sin joystick, el portero se coloca solo; el usuario decide estiradas y salidas.
                    GoalkeeperPositioning(a);
                }
            }
            if (a.IsGoalkeeper && Ball.Owner == a && a.HoldingInHands)
            {
                a.HoldTimer -= _dt;
                if (a.HoldTimer < -6f) { GoalkeeperDistribute(a); return; } // no se puede retener indefinidamente
            }

            if (input.Action == InputAction.None) return;
            Vec2 dir = move.LengthSq > 0.01f ? move.Normalized : a.Facing;
            var team = a.Team;

            if (a.IsGoalkeeper && Ball.Owner != a)
            {
                if (input.Action == InputAction.Dive || input.Action == InputAction.Tackle && Ball.Owner == null)
                {
                    float dist = 1.0f + 1.8f * a.GkDiving;
                    if (move.LengthSq > 0.04f) StartDive(a, move.Normalized, dist / 0.45f);
                    else StartDive(a, Vec2.Zero, 0f); // blocaje en el sitio
                    return;
                }
                if (input.Action == InputAction.KeeperRush) { a.RushTimer = 1.2f; return; }
            }
            if (a.IsGoalkeeper && Ball.Owner == a && input.Action == InputAction.Shoot)
            {
                // El portero no tira: el botón de tiro es el saque largo.
                input.Action = InputAction.LoftedPass;
            }

            if (Ball.Owner == a)
            {
                switch (input.Action)
                {
                    case InputAction.Shoot:
                        Shoot(a, MathF2.Clamp(input.Power <= 0f ? 0.85f : input.Power, 0.2f, 1f));
                        break;
                    case InputAction.Cross:
                        Cross(a);
                        break;
                    case InputAction.Pass:
                    case InputAction.LoftedPass:
                    case InputAction.ThroughPass:
                    {
                        var receiver = BestReceiverInDirection(a, dir, input.Action == InputAction.ThroughPass);
                        if (receiver == null) break;
                        bool lofted = input.Action == InputAction.LoftedPass;
                        Vec2 point = input.Action == InputAction.ThroughPass
                            ? ClampToPitch(receiver.Position + (OpponentGoal(team) - receiver.Position).Normalized * 6f + receiver.Velocity * 0.5f, 2f)
                            : ClampToPitch(receiver.Position + receiver.Velocity * 0.4f, 1.5f);
                        KickPass(a, point, lofted || Vec2.Distance(a.Position, point) > 34f, receiver,
                                 input.Action == InputAction.ThroughPass ? KickKind.ThroughPass : (lofted ? KickKind.LoftedPass : KickKind.Pass));
                        break;
                    }
                }
            }
            else if (input.Action == InputAction.Tackle && Ball.Owner != null && Ball.Owner.Team != team)
            {
                if (Vec2.Distance(a.Position, Ball.Owner.Position) < 2.0f && a.TackleCooldown <= 0f) TryTackle(a, Ball.Owner);
            }
        }

        private MatchAgent? BestReceiverInDirection(MatchAgent a, Vec2 dir, bool forwardOnly)
        {
            MatchAgent? best = null;
            float bestScore = float.MinValue;
            foreach (var t in a.Team.OnPitchList)
            {
                if (t == a) continue;
                var to = t.Position - a.Position;
                float d = to.Length;
                if (d < 3f || d > 50f) continue;
                float align = Vec2.Dot(to / d, dir);
                if (align < 0.3f) continue;
                if (forwardOnly && ToRel(a.Team, t.Position).X < ToRel(a.Team, a.Position).X) continue;
                float score = align * 2f - d / 40f;
                if (score > bestScore) { bestScore = score; best = t; }
            }
            return best;
        }

        // ---------------------------------------------------------------- locomoción

        private float TopSpeed(MatchAgent a, bool sprint)
        {
            float top = 6.6f + 2.7f * a.Speed;
            if (!sprint) top *= 0.62f;
            top *= 0.8f + 0.2f * a.Energy;
            if (Ball.Owner == a) top *= 0.8f + 0.12f * a.Dribbling;
            if (a.Injured) top *= 0.75f;
            if (a.StumbleTimer > 0f) top *= 0.35f;
            return top;
        }

        private Vec2[] _separation = new Vec2[32];

        private void MoveAgents()
        {
            // Separación ligera para no amontonarse (pares simétricos: n·(n−1)/2 comprobaciones).
            if (_separation.Length < _onPitch.Count) _separation = new Vec2[_onPitch.Count * 2];
            for (int i = 0; i < _onPitch.Count; i++)
            {
                var pi = _onPitch[i].Position;
                for (int j = i + 1; j < _onPitch.Count; j++)
                {
                    Vec2 diff = pi - _onPitch[j].Position;
                    float d2 = diff.X * diff.X + diff.Y * diff.Y;
                    if (d2 < 0.81f && d2 > 1e-6f)
                    {
                        float d = (float)Math.Sqrt(d2);
                        Vec2 push = diff / d * (0.9f - d) * 2.5f;
                        _separation[i] += push;
                        _separation[j] -= push;
                    }
                }
            }
            for (int i = 0; i < _onPitch.Count; i++)
            {
                var a = _onPitch[i];
                if (!a.OnPitch) continue;
                if (a.DiveTimer >= 0f)
                {
                    // Estirada: impulso lateral 0,45 s, en el suelo hasta 1,25 s y se levanta.
                    a.DiveTimer += _dt;
                    a.Velocity = a.DiveTimer < 0.45f ? a.DiveDir * a.DiveSpeed : Vec2.Zero;
                    a.Position = ClampToPitch(a.Position + a.Velocity * _dt, -3f);
                    if (a.DiveTimer >= 1.25f) a.DiveTimer = -1f;
                    _separation[i] = Vec2.Zero;
                    continue;
                }
                Vec2 toTarget = a.Target - a.Position;
                float dist = toTarget.Length;
                float maxSpeed = TopSpeed(a, a.WantsSprint);
                float speed = Math.Min(maxSpeed, dist * 2.2f);
                Vec2 desired = dist > 0.05f ? toTarget / dist * speed : Vec2.Zero;

                desired += _separation[i];
                _separation[i] = Vec2.Zero;

                float accel = 4f + 4.5f * a.Acceleration + 1.5f * a.Agility;
                Vec2 dv = (desired - a.Velocity).ClampLength(accel * _dt);
                a.Velocity += dv;
                Vec2 step = a.Velocity * _dt;
                a.Position = ClampToPitch(a.Position + step, -3f);
                a.Stats.DistanceCovered += step.Length;

                float v = a.Velocity.Length;
                if (v > 0.3f) a.Facing = a.Velocity / v;
                else
                {
                    var toBall = Ball.Position.XY - a.Position;
                    if (toBall.LengthSq > 0.01f) a.Facing = toBall.Normalized;
                }

                a.IsSprinting = a.WantsSprint && v > TopSpeed(a, false) * 1.05f;
                UpdateEnergy(a, v);
            }
        }

        private void UpdateEnergy(MatchAgent a, float speed)
        {
            float staminaF = 1.55f - a.Stamina; // 0.55 (resistente) … 1.55 (poco resistente)
            float drain;
            if (a.IsSprinting) drain = 0.0034f;
            else if (speed > 3.5f) drain = 0.0011f;
            else drain = -0.00015f; // recuperación al trote suave
            if (drain > 0f) drain *= staminaF;
            a.Energy = MathF2.Clamp(a.Energy - drain * _dt * _fatigueScale, 0.15f, 1f);
        }

        private static Vec2 ClampToPitch(Vec2 p, float margin) =>
            new Vec2(MathF2.Clamp(p.X, -Pitch.HalfLength + margin, Pitch.HalfLength - margin),
                     MathF2.Clamp(p.Y, -Pitch.HalfWidth + margin, Pitch.HalfWidth - margin));
    }
}
