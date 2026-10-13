using System;
using System.Collections.Generic;
using System.Linq;

namespace Dorsal.Core.Match
{
    /// <summary>Árbitro y reglamento: faltas, tarjetas, fuera de juego, balón parado, cambios y lesiones.</summary>
    public sealed partial class MatchEngine
    {
        // ---------------------------------------------------------------- faltas y tarjetas

        private void CallFoul(MatchAgent offender, MatchAgent victim, bool fromBehind)
        {
            var defTeam = offender.Team;
            var attTeam = victim.Team;
            offender.Stats.FoulsCommitted++;
            victim.Stats.FoulsWon++;
            defTeam.Fouls++;
            var spot = victim.Position;
            RaiseEvent(MatchEventType.Foul, defTeam, offender.PlayerId, victim.PlayerId, spot);

            // Tarjetas.
            float pYellow = 0.05f + 0.12f * offender.Aggression + (fromBehind ? 0.15f : 0f);
            bool lastMan = IsLastDefender(offender, attTeam) && ToRel(attTeam, spot).X > 10f;
            float pRed = 0.008f + (lastMan ? 0.25f : 0f);
            double roll = _rng.NextDouble();
            if (roll < pRed) SendOff(offender, MatchEventType.RedCard);
            else if (roll < pRed + pYellow) Book(offender);

            // Lesión del jugador que recibe la falta.
            if (Config.EnableInjuries && _rng.Chance(0.02f + 0.03f * (1f - victim.Energy) + (fromBehind ? 0.02f : 0f)))
                InjurePlayer(victim);

            if (Pitch.InPenaltyArea(spot, attTeam.AttackDir))
            {
                RaiseEvent(MatchEventType.Penalty, attTeam, victim.PlayerId, offender.PlayerId, spot);
                var penSpot = new Vec2(attTeam.AttackDir * (Pitch.HalfLength - Pitch.PenaltySpotDistance), 0f);
                SetupSetPiece(SetPieceType.Penalty, attTeam, penSpot);
            }
            else
            {
                SetupSetPiece(SetPieceType.FreeKick, attTeam, ClampToPitch(spot, 0.5f));
            }
        }

        private bool IsLastDefender(MatchAgent d, MatchTeam attTeam)
        {
            float myX = ToRel(attTeam, d.Position).X;
            foreach (var o in d.Team.OnPitchList)
                if (o != d && !o.IsGoalkeeper && ToRel(attTeam, o.Position).X > myX) return false;
            return true;
        }

        private void Book(MatchAgent a)
        {
            a.YellowCards++;
            a.Stats.YellowCards++;
            _stoppageMinutes[Half - 1] += 0.25f;
            if (a.YellowCards >= 2) SendOff(a, MatchEventType.SecondYellow);
            else RaiseEvent(MatchEventType.YellowCard, a.Team, a.PlayerId, 0, a.Position);
        }

        private void SendOff(MatchAgent a, MatchEventType type)
        {
            a.Stats.RedCard = true;
            RaiseEvent(type, a.Team, a.PlayerId, 0, a.Position);
            RemoveFromPitch(a);
            _stoppageMinutes[Half - 1] += 0.5f;
            // Si expulsan al portero, un jugador de campo pasa a la portería.
            if (a.IsGoalkeeper && a.Team.Goalkeeper == null)
            {
                var replacement = a.Team.OnPitchList.OrderBy(x => Domain.Positions.LineOf(x.Role) == Domain.PositionLine.Defence ? 0 : 1).ThenBy(x => x.Index).FirstOrDefault();
                if (replacement != null) replacement.Role = Domain.Position.GK;
            }
        }

        private void RemoveFromPitch(MatchAgent a)
        {
            if (Ball.Owner == a) Ball.Owner = null;
            a.OnPitch = false;
            a.LeftMinute = (int)Math.Ceiling(ClockMinute);
            _pendingSubs.RemoveAll(s => s.outAgent == a);
            RebuildOnPitch();
        }

        private void InjurePlayer(MatchAgent a)
        {
            if (a.Injured) return;
            a.Injured = true;
            a.Stats.Injured = true;
            _stoppageMinutes[Half - 1] += 1f;
            RaiseEvent(MatchEventType.Injury, a.Team, a.PlayerId, 0, a.Position);
        }

        private void CallOffside(MatchAgent a, Vec2 where)
        {
            a.Stats.Offsides++;
            a.Team.Offsides++;
            RaiseEvent(MatchEventType.Offside, a.Team, a.PlayerId, 0, where);
            SetupSetPiece(SetPieceType.FreeKick, Opponent(a.Team), ClampToPitch(where, 0.5f));
        }

        // ---------------------------------------------------------------- balón parado

        private void SetupSetPiece(SetPieceType type, MatchTeam team, Vec2 spot, bool announce = true)
        {
            Phase = MatchPhase.SetPiece;
            _setPieceType = type;
            _setPieceTeam = team;
            _setPieceSpot = spot;
            _phaseTimer = 0f;
            Ball.Owner = null;
            Ball.Position = new Vec3(spot, Pitch.BallRadius);
            Ball.Velocity = new Vec3(0f, 0f, 0f);
            Ball.ShotBy = null;
            Ball.SaveBy = null;
            Ball.BlockBy = null;
            Ball.DivingGk = null;
            Ball.IntendedReceiver = null;
            Ball.PendingPassFrom = null;
            Ball.OffsideFlags.Clear();
            Ball.OffsidePositions.Clear();
            foreach (var a in _onPitch)
            {
                a.RunTimer = 0f;
                a.HoldingInHands = false;
                a.StumbleTimer = 0f;
            }
            _setPieceTaker = ChooseTaker(type, team, spot);
            _possessionTeam = team;
            ExecutePendingSubstitutions();
            if (!_setPieceTaker!.OnPitch) _setPieceTaker = ChooseTaker(type, team, spot);

            if (!announce) return;
            switch (type)
            {
                case SetPieceType.ThrowIn: RaiseEvent(MatchEventType.ThrowIn, team, _setPieceTaker?.PlayerId ?? 0, 0, spot); break;
                case SetPieceType.Corner: RaiseEvent(MatchEventType.Corner, team, _setPieceTaker?.PlayerId ?? 0, 0, spot); break;
                case SetPieceType.GoalKick: RaiseEvent(MatchEventType.GoalKick, team, _setPieceTaker?.PlayerId ?? 0, 0, spot); break;
                case SetPieceType.FreeKick: RaiseEvent(MatchEventType.FreeKick, team, _setPieceTaker?.PlayerId ?? 0, 0, spot); break;
            }
        }

        private MatchAgent? ChooseTaker(SetPieceType type, MatchTeam team, Vec2 spot)
        {
            var players = team.OnPitchList.ToList();
            if (players.Count == 0) return null;
            switch (type)
            {
                case SetPieceType.GoalKick:
                    return team.Goalkeeper ?? players[0];
                case SetPieceType.Penalty:
                    return players.Where(p => !p.IsGoalkeeper).OrderByDescending(p => p.Finishing + p.Composure).ThenBy(p => p.Index).FirstOrDefault() ?? players[0];
                case SetPieceType.Corner:
                    return players.Where(p => !p.IsGoalkeeper).OrderByDescending(p => p.Crossing).ThenBy(p => p.Index).FirstOrDefault() ?? players[0];
                case SetPieceType.KickOff:
                    return players.Where(p => !p.IsGoalkeeper)
                                  .OrderBy(p => Domain.Positions.LineOf(p.Role) == Domain.PositionLine.Attack ? 0 : 1)
                                  .ThenBy(p => Vec2.DistanceSq(p.Position, Vec2.Zero)).ThenBy(p => p.Index).First();
                case SetPieceType.FreeKick:
                {
                    // Lanzador designado si el tiro es directo; si no, el más cercano.
                    if (IsDirectFreeKick(team, spot))
                        return players.Where(p => !p.IsGoalkeeper).OrderByDescending(p => p.LongShots + p.Finishing * 0.5f).ThenBy(p => p.Index).First();
                    goto default;
                }
                default:
                    return players.Where(p => !p.IsGoalkeeper || type == SetPieceType.FreeKick && Pitch.InPenaltyArea(spot, -team.AttackDir))
                                  .OrderBy(p => Vec2.DistanceSq(p.Position, spot)).ThenBy(p => p.Index).FirstOrDefault() ?? players[0];
            }
        }

        private static bool IsDirectFreeKick(MatchTeam team, Vec2 spot)
        {
            float d = Vec2.Distance(spot, OpponentGoal(team));
            return d < 29f && Math.Abs(spot.Y) < 18f;
        }

        private void UpdateSetPiece()
        {
            _phaseTimer += _dt;
            var taker = _setPieceTaker;
            if (taker == null || !taker.OnPitch) taker = _setPieceTaker = ChooseTaker(_setPieceType, _setPieceTeam, _setPieceSpot);
            if (taker == null) return;

            Vec2 playDir = new Vec2(_setPieceTeam.AttackDir, 0f);
            Vec2 takerSpot = _setPieceSpot - playDir * 0.5f;
            if (_setPieceType == SetPieceType.ThrowIn) takerSpot = _setPieceSpot + new Vec2(0f, Math.Sign(_setPieceSpot.Y) * 0.3f);

            foreach (var a in _onPitch)
            {
                a.Target = a == taker ? takerSpot : SetPieceTarget(a);
                a.WantsSprint = Vec2.Distance(a.Position, a.Target) > 8f;
            }
            MoveAgents();
            Ball.Position = new Vec3(_setPieceSpot, Pitch.BallRadius);

            float minDelay = _setPieceType switch
            {
                SetPieceType.KickOff => 0.8f,
                SetPieceType.ThrowIn => 0.8f,
                SetPieceType.Corner => 2.0f,
                SetPieceType.Penalty => 2.0f,
                _ => 1.4f,
            };
            bool ready = _phaseTimer >= minDelay && Vec2.Distance(taker.Position, takerSpot) < 1.0f;
            if (!ready && _phaseTimer > 5f)
            {
                taker.Position = takerSpot;
                ready = true;
            }
            if (ready) ExecuteSetPiece(taker);
        }

        private Vec2 SetPieceTarget(MatchAgent a)
        {
            var team = a.Team;
            bool attacking = team == _setPieceTeam;
            switch (_setPieceType)
            {
                case SetPieceType.KickOff:
                    return KickOffPosition(a, attacking);
                case SetPieceType.Penalty:
                {
                    int goalSign = _setPieceTeam.AttackDir;
                    if (a.IsGoalkeeper && !attacking) return new Vec2(goalSign * (Pitch.HalfLength - 0.3f), 0f);
                    if (a.IsGoalkeeper) return FormationTarget(a, true);
                    // Todos fuera del área, en la frontal.
                    float x = goalSign * (Pitch.HalfLength - Pitch.PenaltyAreaDepth - 2f - (a.Index % 3));
                    float y = ((a.Index % 11) - 5) * 3.2f;
                    return new Vec2(x, y);
                }
                case SetPieceType.Corner:
                {
                    int goalSign = _setPieceTeam.AttackDir;
                    if (a.IsGoalkeeper && !attacking) return new Vec2(goalSign * (Pitch.HalfLength - 0.5f), 0f);
                    if (a.IsGoalkeeper) return FormationTarget(a, true);
                    bool goesForward = attacking
                        ? Domain.Positions.LineOf(a.Role) != Domain.PositionLine.Defence || a.HeightFactor > 0.55f
                        : Domain.Positions.LineOf(a.Role) != Domain.PositionLine.Attack || a.Role == Domain.Position.ST && a.Index % 2 == 0;
                    if (!goesForward) return FormationTarget(a, attacking);
                    int k = a.Index % 7;
                    float bx = goalSign * (Pitch.HalfLength - 4f - (k % 3) * 3.5f);
                    float by = (k - 3) * 2.6f;
                    if (!attacking) bx -= goalSign * 0.8f; // defensores ligeramente por delante
                    return new Vec2(bx, by);
                }
                case SetPieceType.GoalKick:
                {
                    var t = FormationTarget(a, attacking);
                    if (!attacking && Pitch.InPenaltyArea(t, -_setPieceTeam.AttackDir))
                        t = new Vec2(-_setPieceTeam.AttackDir * (Pitch.HalfLength - Pitch.PenaltyAreaDepth - 1.5f), t.Y);
                    return t;
                }
                default:
                {
                    var t = a.IsGoalkeeper ? (Vec2?)null : FormationTarget(a, attacking);
                    if (t == null)
                    {
                        GoalkeeperPositioning(a);
                        return a.Target;
                    }
                    var target = t.Value;
                    if (!attacking && (_setPieceType == SetPieceType.FreeKick || _setPieceType == SetPieceType.ThrowIn))
                    {
                        float minDist = _setPieceType == SetPieceType.FreeKick ? 9.15f : 2f;
                        var away = target - _setPieceSpot;
                        if (away.Length < minDist) target = _setPieceSpot + (away.LengthSq < 0.01f ? new Vec2(-_setPieceTeam.AttackDir, 0f) : away.Normalized) * minDist;
                    }
                    return ClampToPitch(target, 0.5f);
                }
            }
        }

        private void ExecuteSetPiece(MatchAgent taker)
        {
            var team = _setPieceTeam;
            Phase = MatchPhase.Playing;
            Ball.Position = new Vec3(_setPieceSpot, Pitch.BallRadius);
            taker.Position = _setPieceSpot - new Vec2(team.AttackDir, 0f) * 0.5f;

            switch (_setPieceType)
            {
                case SetPieceType.KickOff:
                    EnforceKickOffPositions();
                    GainPossession(taker, fromPass: false);
                    taker.DecisionTimer = 0.05f;
                    break;
                case SetPieceType.ThrowIn:
                    ExecuteThrowIn(taker);
                    break;
                case SetPieceType.Corner:
                    _nextKickNoOffside = true;
                    Cross(taker);
                    break;
                case SetPieceType.GoalKick:
                    _nextKickNoOffside = true;
                    GainPossession(taker, fromPass: false);
                    GoalkeeperDistribute(taker);
                    break;
                case SetPieceType.Penalty:
                    ShootPenalty(taker);
                    break;
                case SetPieceType.FreeKick:
                    if (IsDirectFreeKick(team, _setPieceSpot))
                    {
                        GainPossession(taker, fromPass: false);
                        Shoot(taker, 0.9f, freeKick: true);
                    }
                    else
                    {
                        GainPossession(taker, fromPass: false);
                        taker.DecisionTimer = 0.05f;
                    }
                    break;
            }
        }

        private void ExecuteThrowIn(MatchAgent taker)
        {
            var team = taker.Team;
            MatchAgent? best = null;
            float bestScore = float.MinValue;
            foreach (var t in team.OnPitchList)
            {
                if (t == taker || t.IsGoalkeeper) continue;
                float d = Vec2.Distance(t.Position, taker.Position);
                if (d < 3f || d > 22f) continue;
                float score = NearestOpponentDistance(team, t.Position) * 0.3f - d * 0.05f + ToRel(team, t.Position).X * 0.02f;
                if (score > bestScore) { bestScore = score; best = t; }
            }
            Vec2 target = best?.Position ?? ClampToPitch(taker.Position + new Vec2(team.AttackDir * 10f, -Math.Sign(taker.Position.Y) * 4f), 2f);
            float d2 = Vec2.Distance(taker.Position, target);
            var dir = (target - taker.Position).Normalized.Rotate((float)_rng.Gaussian(0, 4f) * MathF2.Deg2Rad);
            float speed = Math.Min(16f, 5f + d2 * 0.55f);
            GainPossession(taker, fromPass: false);
            ReleaseBall(taker, new Vec3(dir * speed, 3.2f), KickKind.ThrowIn);
            Ball.Position = new Vec3(Ball.Position.XY, 2.0f);
            Ball.IntendedReceiver = best;
            Ball.PendingPassFrom = taker;
            taker.Stats.PassesAttempted++;
            SetAction(taker, AgentAction.Throw);
        }

        private void PlaceForKickOff(MatchTeam kickoffTeam)
        {
            foreach (var a in _onPitch)
            {
                a.Position = KickOffPosition(a, a.Team == kickoffTeam);
                a.Velocity = Vec2.Zero;
                a.Target = a.Position;
            }
        }

        private Vec2 KickOffPosition(MatchAgent a, bool kickingTeam)
        {
            var team = a.Team;
            if (a.IsGoalkeeper) return FromRel(team, new Vec2(-Pitch.HalfLength + 1.5f, 0f));
            var slot = team.Formation.Slots[Math.Max(0, a.SlotIndex)];
            float x = -18f + slot.Depth * 13f;
            float y = slot.Lateral * 24f;
            x = Math.Min(x, -1.2f);
            var rel = new Vec2(x, y);
            if (!kickingTeam && rel.Length < Pitch.CenterCircleRadius + 0.5f)
                rel = rel.Normalized * (Pitch.CenterCircleRadius + 0.5f);
            return FromRel(team, rel);
        }

        private void EnforceKickOffPositions()
        {
            foreach (var a in _onPitch)
            {
                if (a == _setPieceTaker) continue;
                var rel = ToRel(a.Team, a.Position);
                bool kicking = a.Team == _setPieceTeam;
                if (rel.X > -0.5f || (!kicking && rel.Length < Pitch.CenterCircleRadius))
                    a.Position = KickOffPosition(a, kicking);
            }
        }

        // ---------------------------------------------------------------- cambios

        /// <summary>
        /// Solicita un cambio (desde la UI o el entrenador IA). Se ejecuta en el siguiente balón parado.
        /// Devuelve false si no es legal.
        /// </summary>
        public bool RequestSubstitution(int outPlayerId, int inPlayerId)
        {
            var outAgent = _all.FirstOrDefault(a => a.PlayerId == outPlayerId);
            var inAgent = _all.FirstOrDefault(a => a.PlayerId == inPlayerId);
            if (outAgent == null || inAgent == null) return false;
            if (outAgent.Team != inAgent.Team || !outAgent.OnPitch || inAgent.OnPitch || inAgent.HasBeenSubstituted || inAgent.SentOff) return false;
            if (inAgent.EverPlayed) return false;
            var team = outAgent.Team;
            int pendingForTeam = _pendingSubs.Count(s => s.outAgent.Team == team);
            if (team.SubstitutionsUsed + pendingForTeam >= Config.MaxSubstitutions) return false;
            if (_pendingSubs.Any(s => s.outAgent == outAgent || s.inAgent == inAgent)) return false;
            _pendingSubs.Add((outAgent, inAgent));
            return true;
        }

        /// <summary>
        /// Mete a un suplente sustituyendo al jugador de campo más adecuado (misma línea y más cansado).
        /// Devuelve false si no es posible.
        /// </summary>
        public bool SubstituteIn(int benchPlayerId)
        {
            var inAgent = _all.FirstOrDefault(a => a.PlayerId == benchPlayerId);
            if (inAgent == null || inAgent.OnPitch || inAgent.EverPlayed) return false;
            bool keeper = inAgent.Player.Position == Domain.Position.GK;
            var line = Domain.Positions.LineOf(inAgent.Player.Position);
            var outgoing = inAgent.Team.OnPitchList
                .Where(a => a.IsGoalkeeper == keeper && _pendingSubs.All(s => s.outAgent != a))
                .OrderBy(a => Domain.Positions.LineOf(a.Role) == line ? 0 : 1)
                .ThenBy(a => a.Role == inAgent.Player.Position ? 0 : 1)
                .ThenBy(a => a.Energy).ThenBy(a => a.Index)
                .FirstOrDefault();
            return outgoing != null && RequestSubstitution(outgoing.PlayerId, benchPlayerId);
        }

        private void ExecutePendingSubstitutions()
        {
            if (_pendingSubs.Count == 0) return;
            foreach (var (outAgent, inAgent) in _pendingSubs.ToList())
            {
                if (!outAgent.OnPitch) continue;
                var team = outAgent.Team;
                if (team.SubstitutionsUsed >= Config.MaxSubstitutions) continue;
                inAgent.SlotIndex = outAgent.SlotIndex;
                inAgent.Role = outAgent.Role;
                inAgent.Stats.Position = outAgent.Role;
                inAgent.Position = new Vec2(outAgent.Position.X, Math.Sign(outAgent.Position.Y == 0 ? 1 : outAgent.Position.Y) * (Pitch.HalfWidth - 0.5f));
                inAgent.OnPitch = true;
                inAgent.EverPlayed = true;
                inAgent.EnteredMinute = (int)Math.Floor(ClockMinute);
                outAgent.OnPitch = false;
                outAgent.HasBeenSubstituted = true;
                outAgent.LeftMinute = (int)Math.Ceiling(ClockMinute);
                if (Ball.Owner == outAgent) Ball.Owner = null;
                team.SubstitutionsUsed++;
                _stoppageMinutes[Half - 1] += 0.4f;
                RaiseEvent(MatchEventType.Substitution, team, inAgent.PlayerId, outAgent.PlayerId, inAgent.Position);
            }
            _pendingSubs.Clear();
            RebuildOnPitch();
        }

        /// <summary>Entrenador IA: cambia lesionados enseguida y cansados a partir del minuto 58.</summary>
        private void CheckCoachDecisions()
        {
            int minute = (int)ClockMinute;
            if (minute == _lastSubCheckMinute) return;
            _lastSubCheckMinute = minute;
            foreach (var team in new[] { Home, Away })
            {
                foreach (var a in team.OnPitchList.ToList())
                {
                    if (!a.Injured) continue;
                    var sub = BestBenchReplacement(team, a);
                    if (sub != null) RequestSubstitution(a.PlayerId, sub.PlayerId);
                }
                if (!Config.EnableTacticalSubstitutions || minute < 58 || minute % 6 != 0 || Half != 2) continue;
                var tired = team.OnPitchList.Where(a => !a.IsGoalkeeper && a.Energy < 0.62f && _pendingSubs.All(s => s.outAgent != a))
                                .OrderBy(a => a.Energy).ThenBy(a => a.Index).FirstOrDefault();
                if (tired == null) continue;
                var replacement = BestBenchReplacement(team, tired);
                if (replacement != null) RequestSubstitution(tired.PlayerId, replacement.PlayerId);
            }
        }

        private MatchAgent? BestBenchReplacement(MatchTeam team, MatchAgent outgoing)
        {
            MatchAgent? best = null;
            float bestScore = float.MinValue;
            foreach (var b in team.Agents)
            {
                if (b.EverPlayed) continue;
                if (_pendingSubs.Any(s => s.inAgent == b)) continue;
                if (outgoing.IsGoalkeeper != (b.Role == Domain.Position.GK)) continue;
                float score = b.Player.OverallInSlot(outgoing.Role);
                if (score > bestScore) { bestScore = score; best = b; }
            }
            return best;
        }

        // ---------------------------------------------------------------- informe

        private void UpdateMinutesPlayedSnapshot()
        {
            int now = Half == 1 ? 45 : 90;
            foreach (var a in _all)
            {
                if (!a.EverPlayed) continue;
                int start = a.Stats.Started ? 0 : Math.Min(a.EnteredMinute, 90);
                int end = a.OnPitch ? now : Math.Min(a.LeftMinute, 90);
                a.Stats.MinutesPlayed = Math.Max(0, end - start);
            }
        }

        private void FinalizeStats()
        {
            UpdateMinutesPlayedSnapshot();
            foreach (var a in _all)
            {
                if (!a.EverPlayed) continue;
                a.Stats.Rating = RatingCalculator.Compute(a.Stats, a.Team.Goals, Opponent(a.Team).Goals);
            }
        }

        public MatchReport BuildReport()
        {
            int totalPoss = Math.Max(1, Home.PossessionTicks + Away.PossessionTicks);
            var stats = new List<PlayerMatchStats>();
            foreach (var a in _all)
                if (a.EverPlayed) stats.Add(a.Stats);
            return new MatchReport
            {
                HomeClubId = Home.ClubId,
                AwayClubId = Away.ClubId,
                HomeGoals = Home.Goals,
                AwayGoals = Away.Goals,
                HomePossession = (float)Home.PossessionTicks / totalPoss,
                HomeShots = Home.Shots,
                AwayShots = Away.Shots,
                HomeShotsOnTarget = Home.ShotsOnTarget,
                AwayShotsOnTarget = Away.ShotsOnTarget,
                HomeCorners = Home.Corners,
                AwayCorners = Away.Corners,
                HomeFouls = Home.Fouls,
                AwayFouls = Away.Fouls,
                HomeOffsides = Home.Offsides,
                AwayOffsides = Away.Offsides,
                Events = _events.ToList(),
                PlayerStats = stats,
                Seed = Seed,
                EngineVersion = EngineVersion,
                Finished = IsFinished,
            };
        }
    }

    public sealed class MatchReport
    {
        public int HomeClubId { get; set; }
        public int AwayClubId { get; set; }
        public int HomeGoals { get; set; }
        public int AwayGoals { get; set; }
        public float HomePossession { get; set; }
        public int HomeShots { get; set; }
        public int AwayShots { get; set; }
        public int HomeShotsOnTarget { get; set; }
        public int AwayShotsOnTarget { get; set; }
        public int HomeCorners { get; set; }
        public int AwayCorners { get; set; }
        public int HomeFouls { get; set; }
        public int AwayFouls { get; set; }
        public int HomeOffsides { get; set; }
        public int AwayOffsides { get; set; }
        public List<MatchEvent> Events { get; set; } = new List<MatchEvent>();
        public List<PlayerMatchStats> PlayerStats { get; set; } = new List<PlayerMatchStats>();
        public ulong Seed { get; set; }
        public string EngineVersion { get; set; } = "";
        public bool Finished { get; set; }
    }
}
