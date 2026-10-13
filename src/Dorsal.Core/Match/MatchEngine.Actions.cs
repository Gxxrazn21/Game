using System;

namespace Dorsal.Core.Match
{
    /// <summary>Ejecución de acciones con balón: pases, tiros, centros, despejes, entradas y control.</summary>
    public sealed partial class MatchEngine
    {
        private const float Gravity = 9.81f;
        private const float RollDecel = 2.2f;
        private const float RollLinear = 0.06f;
        private const float MaxKickSpeed = 32f;

        // ---------------------------------------------------------------- pases

        private void KickPass(MatchAgent from, Vec2 target, bool lofted, MatchAgent? receiver, KickKind kind)
        {
            float d = Vec2.Distance(from.Position, target);
            float skill = lofted ? from.LongPassing : (d > 25f ? MathF2.Lerp(from.ShortPassing, from.LongPassing, 0.5f) : from.ShortPassing);
            if (kind == KickKind.Cross || kind == KickKind.Corner) skill = from.Crossing;
            if (kind == KickKind.GkDistribution) skill = MathF2.Lerp(from.GkKicking, from.ShortPassing, 0.3f);
            if (kind == KickKind.ThrowIn) skill = Math.Max(skill, 0.6f);

            float pressure = Pressure(from);
            float sigmaDeg = (1.2f + 8.5f * (1f - skill)) * (1f + 0.8f * pressure) * (1f + 0.4f * (1f - from.Energy));
            if (d > 30f) sigmaDeg *= 1.2f;
            sigmaDeg *= WeakFootFactor(from);

            var dir = (target - from.Position).Normalized;
            if (dir.LengthSq < 0.5f) dir = from.Facing;
            dir = dir.Rotate((float)_rng.Gaussian(0, sigmaDeg) * MathF2.Deg2Rad);
            float distErr = 1f + (float)_rng.Gaussian(0, 0.05f + 0.12f * (1f - skill));
            float dd = Math.Max(2f, d * distErr);

            Vec3 vel;
            if (!lofted)
            {
                float arrive = 7f + 3f * (from.Team.Tactics.Tempo / 100f);
                float v0 = GroundPassSpeed(dd, arrive);
                vel = new Vec3(dir * v0, 0f);
            }
            else
            {
                float theta = (kind == KickKind.Cross || kind == KickKind.Corner ? 24f : 32f) * MathF2.Deg2Rad;
                float v0 = (float)Math.Sqrt(dd * Gravity / Math.Sin(2 * theta)) * 1.05f;
                v0 = Math.Min(v0, MaxKickSpeed);
                vel = new Vec3(dir * (v0 * (float)Math.Cos(theta)), v0 * (float)Math.Sin(theta));
            }

            ReleaseBall(from, vel, kind);
            Ball.IntendedReceiver = receiver;
            Ball.PendingPassFrom = from;
            from.Stats.PassesAttempted++;
            Diagnostics.PassesKicked++;
            SetAction(from, kind == KickKind.Cross || kind == KickKind.Corner ? AgentAction.Cross : (kind == KickKind.ThrowIn ? AgentAction.Throw : AgentAction.Pass));
            if (!_nextKickNoOffside && kind != KickKind.ThrowIn && kind != KickKind.Corner) FlagOffsides(from);
            _nextKickNoOffside = false;
        }

        /// <summary>Velocidad inicial para que un pase raso recorra <paramref name="distance"/> y llegue a <paramref name="arrive"/> m/s (rodadura con término lineal).</summary>
        private static float GroundPassSpeed(float distance, float arrive)
        {
            float v0 = (float)Math.Sqrt(arrive * arrive + 2f * RollDecel * distance);
            for (int i = 0; i < 3; i++)
            {
                float avg = (v0 + arrive) * 0.5f;
                v0 = (float)Math.Sqrt(arrive * arrive + 2f * (RollDecel + RollLinear * avg) * distance);
            }
            return Math.Min(v0, 27f);
        }

        private float WeakFootFactor(MatchAgent a)
        {
            // Probabilidad de tener que usar el pie malo; penaliza la precisión según la valoración de pie débil.
            if (_rng.Chance(0.25f)) return 1f + (5 - a.WeakFoot) * 0.12f;
            return 1f;
        }

        private void Cross(MatchAgent a)
        {
            var team = a.Team;
            // Un defensor encima puede bloquear el centro; suele acabar en córner.
            if (Phase == MatchPhase.Playing)
            {
                foreach (var o in Opponent(team).OnPitchList)
                {
                    if (o.IsGoalkeeper || Vec2.Distance(o.Position, a.Position) > 2.2f) continue;
                    if (!_rng.Chance(0.15f + 0.25f * o.DefensiveAwareness)) break;
                    SetAction(a, AgentAction.Cross);
                    float side = Math.Sign(a.Position.Y) == 0 ? 1f : Math.Sign(a.Position.Y);
                    ReleaseBall(o, new Vec3(team.AttackDir * _rng.Range(5f, 10f), side * _rng.Range(1f, 4f), _rng.Range(0.5f, 2.5f)), KickKind.Deflection);
                    o.Stats.Blocks++;
                    o.ControlCooldown = 0.4f;
                    return;
                }
            }
            MatchAgent? best = null;
            float bestScore = float.MinValue;
            foreach (var t in team.OnPitchList)
            {
                if (t == a || !Pitch.InPenaltyArea(t.Position, team.AttackDir)) continue;
                float score = t.Heading + t.Jumping * 0.5f + t.HeightFactor * 0.5f - NearestOpponentDistance(team, t.Position) * -0.05f;
                if (score > bestScore) { bestScore = score; best = t; }
            }
            Vec2 point = best != null
                ? best.Position + best.Velocity * 0.6f
                : FromRel(team, new Vec2(Pitch.HalfLength - 9f, _rng.Range(-6f, 6f)));
            team.AssistCandidate = a;
            KickPass(a, ClampToPitch(point, 1f), true, best, KickKind.Cross);
        }

        private void Clear(MatchAgent a)
        {
            var team = a.Team;
            float side = Math.Sign(ToRel(team, a.Position).Y);
            if (side == 0f) side = _rng.Chance(0.5) ? 1f : -1f;
            Vec2 rel = new Vec2(_rng.Range(5f, 25f), side * _rng.Range(10f, 28f));
            Vec2 target = ClampToPitch(FromRel(team, rel), 2f);
            KickPass(a, target, true, null, KickKind.Clearance);
            a.Stats.PassesAttempted--; // un despeje no cuenta como pase
            Ball.PendingPassFrom = null;
        }

        private void GoalkeeperDistribute(MatchAgent gk)
        {
            gk.HoldingInHands = false;
            var team = gk.Team;
            MatchAgent? best = null;
            float bestScore = float.MinValue;
            bool goLong = team.Tactics.Tempo > 65 || _rng.Chance(0.25);
            foreach (var t in team.OnPitchList)
            {
                if (t == gk) continue;
                float d = Vec2.Distance(gk.Position, t.Position);
                float space = NearestOpponentDistance(team, t.Position);
                float relX = ToRel(team, t.Position).X;
                float score = goLong ? relX * 0.05f + space * 0.1f : space * 0.25f - d * 0.03f;
                if (!goLong && d > 35f) continue;
                if (score > bestScore) { bestScore = score; best = t; }
            }
            if (best == null)
            {
                Clear(gk);
                return;
            }
            float dist = Vec2.Distance(gk.Position, best.Position);
            KickPass(gk, best.Position + best.Velocity * 0.5f, dist > 28f, best, KickKind.GkDistribution);
        }

        // ---------------------------------------------------------------- tiros

        private void Shoot(MatchAgent s, float power, bool header = false, bool freeKick = false)
        {
            var team = s.Team;
            var gk = Opponent(team).Goalkeeper;
            int goalSign = team.AttackDir;
            float goalX = Pitch.HalfLength * goalSign;
            var ballPos = Ball.Position;

            // Apuntar al lado contrario de donde está el portero.
            float gkY = gk?.Position.Y ?? 0f;
            float side = gkY > ballPos.Y * 0.25f ? -1f : 1f;
            if (_rng.Chance(0.25)) side = -side;
            float aimY = side * (Pitch.GoalHalfWidth - 0.45f - (float)_rng.NextDouble() * 1.4f);
            float aimZ = header ? _rng.Range(0.3f, 1.6f) : (_rng.Chance(0.6) ? _rng.Range(0.15f, 0.8f) : _rng.Range(0.8f, 2.1f));

            float d = Vec2.Distance(ballPos.XY, new Vec2(goalX, 0f));
            float skill = header ? s.Heading : (d > 20f ? MathF2.Lerp(s.Finishing, s.LongShots, 0.7f) : s.Finishing);
            float pressure = Pressure(s);
            float sigmaDeg = (2.2f + 9f * (1f - skill)) * (1f + 0.9f * pressure) * (1f + 0.35f * (1f - s.Composure)) * (1f + 0.25f * (1f - s.Energy));
            if (!header) sigmaDeg *= WeakFootFactor(s);
            sigmaDeg *= 1f + Math.Max(0f, d - 12f) / 22f;
            if (freeKick) sigmaDeg *= 1.25f;

            float speed = header ? 10f + 7f * s.Heading : (17f + 13f * s.ShotPower) * (0.75f + 0.25f * power);
            var aim = new Vec2(goalX, aimY);
            var dirH = (aim - ballPos.XY).Normalized.Rotate((float)_rng.Gaussian(0, sigmaDeg) * MathF2.Deg2Rad);
            float horiz = Vec2.Distance(ballPos.XY, aim);
            float t = Math.Max(0.15f, horiz / speed);
            float vz = (aimZ - ballPos.Z + 0.5f * Gravity * t * t) / t;
            vz += speed * (float)Math.Tan(_rng.Gaussian(0, sigmaDeg * 0.6f) * MathF2.Deg2Rad);
            var vel = new Vec3(dirH * speed, vz);

            ReleaseBall(s, vel, header ? KickKind.Header : KickKind.Shot);
            SetAction(s, header ? AgentAction.Header : AgentAction.Shoot);
            s.Stats.Shots++;
            team.Shots++;
            Diagnostics.Shots++;
            if (team.AssistCandidate != null && team.AssistCandidate != s) team.AssistCandidate.Stats.KeyPasses++;
            Ball.ShotBy = s;
            Ball.IntendedReceiver = null;
            Ball.PendingPassFrom = null;
            RaiseEvent(MatchEventType.Shot, team, s.PlayerId, 0, ballPos.XY);
            ResolveShot(s, gk, goalSign, speed, penalty: false);
        }

        private void ShootPenalty(MatchAgent s)
        {
            var team = s.Team;
            var gk = Opponent(team).Goalkeeper;
            int goalSign = team.AttackDir;
            float goalX = Pitch.HalfLength * goalSign;
            float pMiss = 0.04f + 0.08f * (1f - s.Composure);
            float gkSkill = gk == null ? 0f : (gk.GkDiving + gk.GkReflexes) * 0.5f;
            float pSave = gk == null ? 0f : 0.12f + 0.16f * gkSkill - 0.08f * (s.Finishing + s.Composure) * 0.5f;
            double roll = _rng.NextDouble();
            bool miss = roll < pMiss;
            bool save = !miss && roll < pMiss + pSave;

            float side = _rng.Chance(0.5) ? 1f : -1f;
            float aimY = miss ? side * (Pitch.GoalHalfWidth + _rng.Range(0.3f, 1.2f)) : side * _rng.Range(1.2f, Pitch.GoalHalfWidth - 0.4f);
            float aimZ = miss && _rng.Chance(0.5) ? Pitch.GoalHeight + 0.4f : _rng.Range(0.2f, 1.6f);
            if (miss && aimZ > Pitch.GoalHeight) aimY = side * _rng.Range(0f, Pitch.GoalHalfWidth);
            float speed = 20f + 8f * s.ShotPower;
            var start = Ball.Position;
            var aim = new Vec2(goalX, aimY);
            var dirH = (aim - start.XY).Normalized;
            float t = Vec2.Distance(start.XY, aim) / speed;
            float vz = (aimZ - start.Z + 0.5f * Gravity * t * t) / t;
            ReleaseBall(s, new Vec3(dirH * speed, vz), KickKind.Shot);
            SetAction(s, AgentAction.Shoot);
            s.Stats.Shots++;
            team.Shots++;
            Ball.ShotBy = s;
            Ball.IsPenaltyShot = true;
            Ball.IntendedReceiver = null;
            Ball.PendingPassFrom = null;

            if (gk != null)
            {
                Ball.DivingGk = gk;
                SetAction(gk, AgentAction.Dive);
                if (save)
                {
                    Ball.SaveBy = gk;
                    Ball.SaveCatch = _rng.Chance(0.3 + 0.3 * gk.GkHandling);
                }
            }
            if (!miss)
            {
                s.Stats.ShotsOnTarget++;
                team.ShotsOnTarget++;
            }
            else
            {
                RaiseEvent(MatchEventType.PenaltyMissed, team, s.PlayerId, 0, start.XY);
            }
        }

        /// <summary>
        /// Resuelve en el momento del disparo si el tiro va a puerta, si lo bloquea un defensor y si lo para el portero.
        /// La física del balón y la animación del portero ejecutan después ese resultado.
        /// </summary>
        private void ResolveShot(MatchAgent s, MatchAgent? gk, int goalSign, float speed, bool penalty)
        {
            var team = s.Team;
            var start = Ball.Position;
            var vel = Ball.Velocity;
            float goalX = Pitch.HalfLength * goalSign;
            if (Math.Abs(vel.X) < 0.1f || Math.Sign(vel.X) != goalSign) return;

            float tCross = (goalX - start.X) / vel.X;
            float yCross = start.Y + vel.Y * tCross;
            float zCross = start.Z + vel.Z * tCross - 0.5f * Gravity * tCross * tCross;
            if (zCross < 0f) zCross = 0.15f; // botó antes de la línea
            bool onTarget = Math.Abs(yCross) < Pitch.GoalHalfWidth - 0.05f && zCross < Pitch.GoalHeight - 0.05f;
            if (!onTarget) return;

            s.Stats.ShotsOnTarget++;
            team.ShotsOnTarget++;

            // Bloqueo por un defensor cercano a la trayectoria.
            if (!penalty)
            {
                var endXY = new Vec2(goalX, yCross);
                foreach (var o in Opponent(team).OnPitchList)
                {
                    if (o.IsGoalkeeper) continue;
                    float dist = Vec2.DistanceToSegment(o.Position, start.XY, endXY, out float tt);
                    if (tt < 0.05f || tt > 0.7f || dist > 1.3f) continue;
                    float p = (0.32f + 0.25f * o.DefensiveAwareness) * (1f - dist / 1.3f);
                    if (_rng.Chance(p))
                    {
                        Diagnostics.Blocks++;
                        Ball.BlockBy = o;
                        return;
                    }
                }
            }

            if (gk == null) return;
            Ball.DivingGk = gk;
            // Tiempo hasta el plano del portero y punto de cruce.
            float tg = Math.Abs(vel.X) > 0.1f ? (gk.Position.X - start.X) / vel.X : tCross;
            tg = MathF2.Clamp(tg, 0.05f, tCross);
            float yg = start.Y + vel.Y * tg;
            float zg = Math.Max(0.1f, start.Z + vel.Z * tg - 0.5f * Gravity * tg * tg);
            float lateral = (float)Math.Sqrt((yg - gk.Position.Y) * (yg - gk.Position.Y) + Math.Max(0f, zg - 1.1f) * Math.Max(0f, zg - 1.1f));
            float reaction = 0.16f + 0.22f * (1f - gk.GkReflexes);
            float available = Math.Max(0f, tg - reaction);
            float timeFactor = MathF2.Clamp(available / 0.45f, 0f, 1.3f);
            float reachCap = 1.0f + (1.6f + 1.2f * gk.GkDiving) * timeFactor;
            float pSave;
            if (lateral <= reachCap)
            {
                float r = lateral / reachCap;
                pSave = 0.98f - 0.36f * r * r;
            }
            else pSave = Math.Max(0.03f, 0.5f - (lateral - reachCap) * 0.6f);
            pSave *= 0.8f + 0.2f * gk.GkPositioning;
            pSave *= MathF2.Clamp(1.25f - speed / 60f, 0.75f, 1f);
            pSave = MathF2.Clamp(pSave, 0.03f, 0.95f);

            if (DebugLog != null) Log($"SHOT {s} d={Vec2.Distance(start.XY, new Vec2(goalX, 0)):F1} speed={speed:F1} yCross={yCross:F2} zCross={zCross:F2} gkY={gk.Position.Y:F2} lateral={lateral:F2} tg={tg:F2} reachCap={reachCap:F2} pSave={pSave:F2}");
            SetAction(gk, AgentAction.Dive);
            if (_rng.Chance(pSave))
            {
                Diagnostics.SavesAssigned++;
                Ball.SaveBy = gk;
                Ball.SaveCatch = _rng.Chance(MathF2.Clamp(0.25f + 0.55f * gk.GkHandling - speed / 80f, 0.1f, 0.85f));
            }
        }

        // ---------------------------------------------------------------- entradas

        private void TryTackle(MatchAgent d, MatchAgent carrier)
        {
            d.TackleCooldown = 1.1f + 0.4f * (1f - d.Reactions);
            Diagnostics.Tackles++;
            SetAction(d, AgentAction.Tackle);
            var toDefender = (d.Position - carrier.Position).Normalized;
            bool fromBehind = Vec2.Dot(carrier.Facing, toDefender) < -0.35f;

            float def = d.Tackling * 0.7f + d.Strength * 0.15f + d.DefensiveAwareness * 0.15f;
            float att = carrier.Dribbling * 0.6f + carrier.Balance * 0.25f + carrier.Strength * 0.15f;
            float pWin = MathF2.Clamp(0.45f + 0.6f * (def - att), 0.12f, 0.85f);
            float pFoul = MathF2.Clamp(0.04f + 0.11f * d.Aggression + 0.12f * (1f - d.Tackling) + (fromBehind ? 0.18f : 0f), 0.02f, 0.45f);

            double roll = _rng.NextDouble();
            if (roll < pFoul)
            {
                CallFoul(d, carrier, fromBehind);
                return;
            }
            if (roll < pFoul + pWin * (1f - pFoul))
            {
                d.Stats.TacklesWon++;
                carrier.Stats.PossessionLost++;
                if (_rng.Chance(0.55))
                {
                    GainPossession(d, fromPass: false);
                }
                else
                {
                    // Balón suelto tras la entrada.
                    var dir = (carrier.Facing + toDefender * -0.5f).Normalized.Rotate(_rng.Range(-1.2f, 1.2f));
                    ReleaseBall(d, new Vec3(dir * _rng.Range(3f, 7f), 0f), KickKind.Deflection);
                    carrier.ControlCooldown = 0.5f;
                }
            }
            else
            {
                d.Stats.TacklesLost++;
                carrier.Stats.DribblesCompleted++;
                d.StumbleTimer = 0.7f + 0.3f * (1f - d.Agility);
                SetAction(d, AgentAction.Stumble);
            }
        }

        // ---------------------------------------------------------------- posesión

        private void ReleaseBall(MatchAgent kicker, Vec3 velocity, KickKind kind)
        {
            if (Ball.Owner != null) Ball.Owner.HoldingInHands = false;
            Ball.Owner = null;
            Ball.Velocity = velocity;
            Ball.Kicker = kicker;
            Ball.LastTouch = kicker;
            Ball.LastKick = kind;
            Ball.TimeSinceKick = 0f;
            Ball.ShotBy = null;
            Ball.SaveBy = null;
            Ball.BlockBy = null;
            Ball.DivingGk = null;
            Ball.IsPenaltyShot = false;
            Ball.IntendedReceiver = null;
            Ball.PendingPassFrom = null;
            Ball.OffsideFlags.Clear();
            Ball.OffsidePositions.Clear();
            kicker.ControlCooldown = 0.3f;
            _possessionTeam = kicker.Team;
        }

        private void GainPossession(MatchAgent a, bool fromPass, bool inHands = false)
        {
            var team = a.Team;
            // Fuera de juego: el primer toque de un compañero señalado cuando se dio el pase.
            if (Ball.OffsideFlags.Contains(a.Index) && Ball.Kicker != null && Ball.Kicker.Team == team)
            {
                var pos = Ball.OffsidePositions.TryGetValue(a.Index, out var p) ? p : a.Position;
                CallOffside(a, pos);
                return;
            }

            var passer = Ball.PendingPassFrom;
            if (passer != null)
            {
                if (passer.Team == team && passer != a)
                {
                    if (a == Ball.IntendedReceiver) Diagnostics.PassToIntended++; else Diagnostics.PassToOtherTeammate++;
                    passer.Stats.PassesCompleted++;
                    team.AssistCandidate = passer;
                }
                else if (passer.Team != team)
                {
                    Diagnostics.PassIntercepted++;
                    if (DebugLog != null) Log($"  INTERCEPTED by {a} t={Ball.TimeSinceKick:F2} ballSpeed={Ball.Velocity.Length:F1} z={Ball.Position.Z:F1} dRecv={(Ball.IntendedReceiver != null ? Vec2.Distance(Ball.IntendedReceiver.Position, a.Position) : -1):F1}");
                    a.Stats.Interceptions++;
                    passer.Stats.PossessionLost++;
                }
            }
            if (_possessionTeam != team) Opponent(team).AssistCandidate = null;

            Ball.Owner = a;
            Ball.LastTouch = a;
            Ball.Velocity = new Vec3(0f, 0f, 0f);
            Ball.Kicker = null;
            Ball.IntendedReceiver = null;
            Ball.PendingPassFrom = null;
            Ball.ShotBy = null;
            Ball.SaveBy = null;
            Ball.BlockBy = null;
            Ball.DivingGk = null;
            Ball.IsPenaltyShot = false;
            Ball.OffsideFlags.Clear();
            Ball.OffsidePositions.Clear();
            _possessionTeam = team;
            a.HoldingInHands = inHands;
            a.RunTimer = 0f;
            // Tiempo de reacción antes de la primera decisión con balón.
            a.DecisionTimer = inHands ? 0f : 0.12f + 0.3f * (1f - a.Reactions) + (fromPass ? 0.1f * (1f - a.BallControl) : 0f);
            if (inHands)
            {
                a.HoldTimer = 1.0f + (float)_rng.NextDouble() * 1.2f;
                SetAction(a, AgentAction.Catch);
            }
            // Mientras decide, sigue en la dirección de ataque.
            a.Target = a.Position + (OpponentGoal(team) - a.Position).Normalized * 2f;
        }

        private void FlagOffsides(MatchAgent passer)
        {
            if (!Config.EnableOffside) return;
            var team = passer.Team;
            float line = OffsideLineRel(team);
            foreach (var t in team.OnPitchList)
            {
                if (t == passer) continue;
                float x = ToRel(team, t.Position).X;
                if (x > 0f && x > line + 0.15f)
                {
                    Ball.OffsideFlags.Add(t.Index);
                    Ball.OffsidePositions[t.Index] = t.Position;
                }
            }
        }
    }
}
