using System;

namespace Dorsal.Core.Match
{
    /// <summary>Física del balón, contactos (control, intercepción, cabezazo, parada, bloqueo) y salidas del campo.</summary>
    public sealed partial class MatchEngine
    {
        private const float AirDrag = 0.012f;
        private const float Restitution = 0.55f;

        private void UpdateBall()
        {
            if (Ball.Owner != null)
            {
                var o = Ball.Owner;
                if (o.HoldingInHands)
                {
                    Ball.Position = new Vec3(o.Position + o.Facing * 0.3f, 1.1f);
                }
                else
                {
                    Ball.Position = new Vec3(o.Position + o.Facing * 0.55f, Pitch.BallRadius);
                }
                Ball.Velocity = new Vec3(o.Velocity, 0f);
                return;
            }
            var pos = Ball.Position;
            var vel = Ball.Velocity;
            const int sub = 2;
            for (int i = 0; i < sub; i++) IntegrateBall(ref pos, ref vel, _dt / sub);
            Ball.Position = pos;
            Ball.Velocity = vel;
        }

        /// <summary>Integración semi-implícita: gravedad, rozamiento del aire, bote y rodadura.</summary>
        private static void IntegrateBall(ref Vec3 pos, ref Vec3 vel, float h)
        {
            bool airborne = pos.Z > Pitch.BallRadius + 0.01f || vel.Z > 0.01f;
            if (airborne)
            {
                vel.Z -= Gravity * h;
                float speed = vel.Length;
                float k = 1f - AirDrag * speed * h;
                if (k < 0f) k = 0f;
                vel = vel * k;
            }
            else
            {
                var vxy = new Vec2(vel.X, vel.Y);
                float s = vxy.Length;
                float ns = Math.Max(0f, s - (RollDecel + RollLinear * s) * h);
                vxy = s > 1e-4f ? vxy * (ns / s) : Vec2.Zero;
                vel = new Vec3(vxy, 0f);
            }

            pos = pos + vel * h;
            if (pos.Z < Pitch.BallRadius)
            {
                pos.Z = Pitch.BallRadius;
                if (vel.Z < 0f)
                {
                    vel.Z = -vel.Z * Restitution;
                    if (vel.Z < 1.0f) vel.Z = 0f;
                    vel.X *= 0.88f;
                    vel.Y *= 0.88f;
                }
            }
        }

        /// <summary>Punto (Y) por el que el balón cruzará el plano X dado, para el portero.</summary>
        private Vec2 PredictCrossingAt(float x)
        {
            var v = Ball.Velocity;
            var p = Ball.Position;
            if (Math.Abs(v.X) < 0.1f) return new Vec2(x, p.Y);
            float t = (x - p.X) / v.X;
            if (t < 0f) t = 0f;
            return new Vec2(x, MathF2.Clamp(p.Y + v.Y * t, -Pitch.GoalHalfWidth - 1f, Pitch.GoalHalfWidth + 1f));
        }

        // ---------------------------------------------------------------- contactos

        private void ResolveBallContacts()
        {
            if (Ball.Owner != null) return;
            var bp = Ball.Position;

            // Un tiro que pierde fuerza sin salir ni llegar al portero vuelve a ser un balón suelto.
            if (Ball.ShotBy != null && Ball.Velocity.Length < 5f)
            {
                Ball.ShotBy = null;
                Ball.HumanSavePending = false;
                Ball.SaveBy = null;
                Ball.BlockBy = null;
                Ball.DivingGk = null;
            }

            // Tiro en curso: solo intervienen el bloqueador y el portero designados.
            if (Ball.ShotBy != null)
            {
                var dir = Ball.Velocity.XY.Normalized;
                if (Ball.BlockBy != null && Vec2.Dot(Ball.BlockBy.Position - bp.XY, dir) <= 0.3f)
                {
                    var blocker = Ball.BlockBy;
                    blocker.Stats.Blocks++;
                    RaiseEvent(MatchEventType.ShotBlocked, blocker.Team, blocker.PlayerId, Ball.ShotBy.PlayerId, bp.XY);
                    var shooter = Ball.ShotBy;
                    Vec3 defl;
                    if (_rng.Chance(0.35))
                    {
                        // Desvío hacia la línea de fondo: suele acabar en córner.
                        float side = Math.Sign(bp.Y) == 0 ? (_rng.Chance(0.5) ? 1f : -1f) : Math.Sign(bp.Y);
                        defl = new Vec3(dir.X * _rng.Range(8f, 14f), side * _rng.Range(4f, 9f), _rng.Range(0.5f, 3f));
                    }
                    else
                    {
                        var newDir = (-dir).Rotate(_rng.Range(-1.4f, 1.4f));
                        defl = new Vec3(newDir * (Ball.Velocity.Length * _rng.Range(0.2f, 0.45f)), _rng.Range(0f, 3f));
                    }
                    ReleaseBall(blocker, defl, KickKind.Deflection);
                    blocker.ControlCooldown = 0.4f;
                    shooter.ControlCooldown = 0.3f;
                    return;
                }
                if (Ball.HumanSavePending && Ball.DivingGk != null)
                {
                    var hgk = Ball.DivingGk;
                    float gkRelX = hgk.Position.X * Ball.ShotBy.Team.AttackDir;
                    float ballRelX = bp.X * Ball.ShotBy.Team.AttackDir;
                    if (ballRelX >= gkRelX - 0.3f) ResolveHumanSave(hgk);
                    return;
                }
                if (Ball.SaveBy != null)
                {
                    var gk = Ball.SaveBy;
                    float gkRelX = gk.Position.X * Ball.ShotBy.Team.AttackDir;
                    float ballRelX = bp.X * Ball.ShotBy.Team.AttackDir;
                    if (ballRelX >= gkRelX - 0.3f) PerformSave(gk);
                }
                return;
            }

            if (Ball.TimeSinceKick < 0.08f) return;

            MatchAgent? best = null;
            float bestD = float.MaxValue;
            bool bestIsHeader = false;
            for (int i = 0; i < _onPitch.Count; i++)
            {
                var a = _onPitch[i];
                if (!a.OnPitch || a.ControlCooldown > 0f || a.StumbleTimer > 0f) continue;
                if (a == Ball.Kicker && Ball.TimeSinceKick < 0.4f) continue;
                float d = Vec2.Distance(a.Position, bp.XY);
                bool header = false;
                bool canHands = a.IsGoalkeeper && Pitch.InPenaltyArea(a.Position, -a.Team.AttackDir);
                float reach;
                if (bp.Z <= 0.9f) reach = 0.55f + 0.35f * a.Agility;
                else if (canHands && bp.Z <= 2.6f + 0.3f * a.GkDiving) reach = 0.9f + 0.6f * a.GkDiving;
                else if (bp.Z <= 1.9f + 0.6f * a.Jumping + 0.3f * a.HeightFactor) { reach = 0.6f + 0.3f * a.Jumping; header = true; }
                else continue;
                if (d > reach) continue;
                if (d < bestD) { bestD = d; best = a; bestIsHeader = header; }
            }
            if (best == null) return;

            if (bestIsHeader && !(best.IsGoalkeeper && Pitch.InPenaltyArea(best.Position, -best.Team.AttackDir)))
            {
                PerformHeader(best);
                return;
            }

            float ballSpeed = Ball.Velocity.Length;
            float relSpeed = (Ball.Velocity.XY - best.Velocity).Length;
            bool keeperHands = best.IsGoalkeeper && Pitch.InPenaltyArea(best.Position, -best.Team.AttackDir);
            float skill = keeperHands ? MathF2.Lerp(best.GkHandling, best.BallControl, 0.2f) : best.BallControl;
            float p = 1.12f - ballSpeed / (24f + 30f * skill) - 0.008f * best.Velocity.Length;
            if (bp.Z > 0.5f) p -= 0.1f * (1f - skill);
            p = MathF2.Clamp(p, 0.15f, 0.97f);
            bool isOpponentOfPasser = Ball.PendingPassFrom != null && Ball.PendingPassFrom.Team != best.Team;
            if (isOpponentOfPasser) p *= 0.7f + 0.3f * best.DefensiveAwareness;
            if (keeperHands) p = Math.Max(p, 0.6f + 0.35f * best.GkHandling);

            if (_rng.Chance(p))
            {
                if (Ball.LastKick == KickKind.Shot || Ball.LastKick == KickKind.Header) { }
                GainPossession(best, fromPass: Ball.PendingPassFrom != null, inHands: keeperHands && bp.Z > 0.3f || keeperHands && relSpeed > 6f);
            }
            else
            {
                // Mal control: el balón rebota.
                var dir = Ball.Velocity.XY.Normalized.Rotate(_rng.Range(-1.1f, 1.1f));
                if (dir.LengthSq < 0.5f) dir = best.Facing.Rotate(_rng.Range(-1.5f, 1.5f));
                float speed = Math.Max(1.5f, Ball.Velocity.XY.Length * _rng.Range(0.25f, 0.5f));
                var passFrom = Ball.PendingPassFrom;
                if (passFrom != null)
                {
                    if (passFrom.Team == best.Team) Diagnostics.PassControlFailedByTeammate++;
                    else Diagnostics.PassDeflectedByOpponent++;
                }
                ReleaseBall(best, new Vec3(dir * speed, Math.Max(0f, Ball.Velocity.Z * 0.3f)), KickKind.Deflection);
                best.ControlCooldown = 0.55f;
                if (passFrom != null && passFrom.Team != best.Team) best.Stats.Interceptions++; // corta aunque no controle
            }
        }

        private void PerformHeader(MatchAgent a)
        {
            var team = a.Team;
            SetAction(a, AgentAction.Header);
            var passFrom = Ball.PendingPassFrom;
            if (Ball.OffsideFlags.Contains(a.Index) && Ball.Kicker != null && Ball.Kicker.Team == team)
            {
                var pos = Ball.OffsidePositions.TryGetValue(a.Index, out var p) ? p : a.Position;
                CallOffside(a, pos);
                return;
            }
            if (passFrom != null)
            {
                if (passFrom.Team == team && passFrom != a)
                {
                    passFrom.Stats.PassesCompleted++;
                    team.AssistCandidate = passFrom;
                }
                else if (passFrom.Team != team) a.Stats.Interceptions++;
            }
            _possessionTeam = team;

            float goalDist = Vec2.Distance(a.Position, OpponentGoal(team));
            Vec2 rel = ToRel(team, a.Position);
            if (goalDist < 14f && Math.Abs(rel.Y) < 12f)
            {
                Shoot(a, 1f, header: true);
                return;
            }
            if (rel.X < -Pitch.HalfLength + 30f)
            {
                // Despeje de cabeza hacia fuera (a veces, por la línea de fondo).
                var outDir = new Vec2(team.AttackDir, Math.Sign(a.Position.Y) * 0.8f).Normalized.Rotate(_rng.Range(-0.4f, 0.4f));
                if (Pitch.InPenaltyArea(a.Position, -team.AttackDir) && _rng.Chance(0.3))
                    outDir = new Vec2(-team.AttackDir * 0.6f, Math.Sign(a.Position.Y == 0 ? 1 : a.Position.Y)).Normalized;
                ReleaseBall(a, new Vec3(outDir * (9f + 6f * a.Heading), 4f + 3f * a.Heading), KickKind.Header);
                a.ControlCooldown = 0.4f;
                return;
            }
            // Pase de cabeza al compañero más cercano en dirección de ataque.
            MatchAgent? best = null;
            float bestScore = float.MinValue;
            foreach (var t in team.OnPitchList)
            {
                if (t == a) continue;
                float d = Vec2.Distance(a.Position, t.Position);
                if (d > 18f) continue;
                float score = -d * 0.1f + ToRel(team, t.Position).X * 0.05f + NearestOpponentDistance(team, t.Position) * 0.1f;
                if (score > bestScore) { bestScore = score; best = t; }
            }
            if (best != null)
            {
                var dir = (best.Position - a.Position).Normalized.Rotate((float)_rng.Gaussian(0, (6f + 14f * (1f - a.Heading)) * MathF2.Deg2Rad));
                float speed = Math.Min(14f, 4f + Vec2.Distance(a.Position, best.Position) * 0.6f);
                ReleaseBall(a, new Vec3(dir * speed, 2f), KickKind.Header);
                Ball.IntendedReceiver = best;
                Ball.PendingPassFrom = a;
                a.Stats.PassesAttempted++;
            }
            else
            {
                var dir = new Vec2(team.AttackDir, 0f).Rotate(_rng.Range(-0.8f, 0.8f));
                ReleaseBall(a, new Vec3(dir * 8f, 3f), KickKind.Header);
            }
            a.ControlCooldown = 0.4f;
        }

        private void PerformSave(MatchAgent gk)
        {
            var shooter = Ball.ShotBy!;
            gk.Stats.Saves++;
            RaiseEvent(MatchEventType.ShotSaved, gk.Team, gk.PlayerId, shooter.PlayerId, Ball.Position.XY);
            if (Ball.SaveCatch)
            {
                Ball.ShotBy = null;
                gk.Position = new Vec2(gk.Position.X, MathF2.Clamp(Ball.Position.Y, -Pitch.GoalHalfWidth - 1.5f, Pitch.GoalHalfWidth + 1.5f));
                GainPossession(gk, fromPass: false, inHands: true);
            }
            else
            {
                // Despeje del portero: a córner (por encima o junto al poste) o de vuelta al campo.
                var v = Ball.Velocity;
                float side = Math.Sign(Ball.Position.Y);
                if (side == 0f) side = _rng.Chance(0.5) ? 1f : -1f;
                Vec3 newVel;
                if (_rng.Chance(0.45))
                    newVel = new Vec3(Math.Sign(v.X) * _rng.Range(4f, 8f), side * _rng.Range(3f, 7f), _rng.Range(2f, 6f));
                else
                    newVel = new Vec3(-Math.Sign(v.X) * _rng.Range(3f, 8f), side * _rng.Range(4f, 11f), _rng.Range(0.5f, 4f));
                ReleaseBall(gk, newVel, KickKind.Deflection);
                gk.ControlCooldown = 0.7f;
                SetAction(gk, AgentAction.Dive);
            }
        }

        // ---------------------------------------------------------------- salidas del campo y goles

        private void CheckBallOutOfPlay()
        {
            if (Ball.Owner != null)
            {
                // Si el poseedor saca el balón del campo conduciendo, el último toque es suyo.
                var op = Ball.Position;
                if (Math.Abs(op.X) <= Pitch.HalfLength && Math.Abs(op.Y) <= Pitch.HalfWidth) return;
                var owner = Ball.Owner;
                ReleaseBall(owner, new Vec3(owner.Velocity, 0f), KickKind.Deflection);
            }
            var p = Ball.Position;
            if (Ball.PendingPassFrom != null && (Math.Abs(p.X) > Pitch.HalfLength || Math.Abs(p.Y) > Pitch.HalfWidth)) Diagnostics.PassOut++;

            if (Math.Abs(p.X) > Pitch.HalfLength)
            {
                int goalSign = Math.Sign(p.X);
                // Interpolación del punto de cruce con la línea de meta.
                var v = Ball.Velocity;
                float back = Math.Abs(v.X) > 1e-3f ? (Math.Abs(p.X) - Pitch.HalfLength) / Math.Abs(v.X) : 0f;
                float yCross = p.Y - v.Y * back;
                float zCross = p.Z - v.Z * back;
                var defending = Home.AttackDir == -goalSign ? Home : Away; // equipo que defiende esa portería
                var attacking = Opponent(defending);

                if (Math.Abs(yCross) < Pitch.GoalHalfWidth && zCross < Pitch.GoalHeight)
                {
                    if (Ball.ShotBy != null) Diagnostics.GoalsFromShots++;
                    ScoreGoal(attacking, new Vec2(goalSign * Pitch.HalfLength, yCross));
                    return;
                }

                // Tiro desviado: estadística y evento.
                if (Ball.ShotBy != null && Ball.ShotBy.Team == attacking)
                    RaiseEvent(MatchEventType.ShotOffTarget, attacking, Ball.ShotBy.PlayerId, 0, new Vec2(goalSign * Pitch.HalfLength, yCross));

                var lastTeam = Ball.LastTouch?.Team ?? attacking;
                if (lastTeam == defending)
                {
                    attacking.Corners++;
                    var corner = new Vec2(goalSign * (Pitch.HalfLength - 0.5f), Math.Sign(yCross == 0 ? 1 : yCross) * (Pitch.HalfWidth - 0.5f));
                    SetupSetPiece(SetPieceType.Corner, attacking, corner);
                }
                else
                {
                    var spot = new Vec2(goalSign * (Pitch.HalfLength - Pitch.GoalAreaDepth), Math.Sign(yCross) * 4f);
                    SetupSetPiece(SetPieceType.GoalKick, defending, spot);
                }
                return;
            }

            if (Math.Abs(p.Y) > Pitch.HalfWidth)
            {
                var lastTeam = Ball.LastTouch?.Team ?? Home;
                var spot = new Vec2(MathF2.Clamp(p.X, -Pitch.HalfLength + 1f, Pitch.HalfLength - 1f), Math.Sign(p.Y) * (Pitch.HalfWidth - 0.2f));
                SetupSetPiece(SetPieceType.ThrowIn, Opponent(lastTeam), spot);
            }
        }

        private void ScoreGoal(MatchTeam scoringTeam, Vec2 where)
        {
            var conceding = Opponent(scoringTeam);
            scoringTeam.Goals++;
            var last = Ball.LastTouch;
            var gk = conceding.Goalkeeper;
            if (gk != null) gk.Stats.GoalsConceded++;
            foreach (var d in conceding.OnPitchList)
                if (!d.IsGoalkeeper && Domain.Positions.LineOf(d.Role) == Domain.PositionLine.Defence) d.Stats.GoalsConceded++;

            if (last != null && last.Team == scoringTeam)
            {
                last.Stats.Goals++;
                var assister = scoringTeam.AssistCandidate;
                int assistId = 0;
                if (assister != null && assister != last)
                {
                    assister.Stats.Assists++;
                    assistId = assister.PlayerId;
                }
                RaiseEvent(MatchEventType.Goal, scoringTeam, last.PlayerId, assistId, where);
                SetAction(last, AgentAction.Celebrate);
            }
            else
            {
                RaiseEvent(MatchEventType.OwnGoal, scoringTeam, last?.PlayerId ?? 0, 0, where);
            }

            _stoppageMinutes[Half - 1] += 0.5f;
            scoringTeam.AssistCandidate = null;
            conceding.AssistCandidate = null;
            _concedingTeam = conceding;
            Ball.Owner = null;
            Ball.Velocity = new Vec3(0f, 0f, 0f);
            Ball.ShotBy = null;
            Ball.SaveBy = null;
            Ball.BlockBy = null;
            Ball.DivingGk = null;
            Phase = MatchPhase.GoalCelebration;
            _phaseTimer = 0f;
        }

        private void UpdateCelebration()
        {
            _phaseTimer += _dt;
            foreach (var a in _onPitch)
            {
                a.Target = KickOffPosition(a, a.Team == _concedingTeam);
                a.WantsSprint = false;
            }
            MoveAgents();
            if (_phaseTimer >= 3f)
            {
                ExecutePendingSubstitutions();
                var team = _concedingTeam ?? Home;
                PlaceForKickOff(team);
                SetupSetPiece(SetPieceType.KickOff, team, Vec2.Zero, announce: false);
            }
        }
    }
}
