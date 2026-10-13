using System.Collections.Generic;
using Dorsal.Core.Domain;

namespace Dorsal.Core.Match
{
    /// <summary>Estadísticas individuales de un partido.</summary>
    public sealed class PlayerMatchStats
    {
        public int PlayerId { get; set; }
        public int ClubId { get; set; }
        public TeamSide Side { get; set; }
        public Position Position { get; set; }
        public bool Started { get; set; }
        public int MinutesPlayed { get; set; }
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int Shots { get; set; }
        public int ShotsOnTarget { get; set; }
        public int PassesAttempted { get; set; }
        public int PassesCompleted { get; set; }
        public int KeyPasses { get; set; }
        public int DribblesCompleted { get; set; }
        public int TacklesWon { get; set; }
        public int TacklesLost { get; set; }
        public int Interceptions { get; set; }
        public int Blocks { get; set; }
        public int Saves { get; set; }
        public int GoalsConceded { get; set; }
        public int PossessionLost { get; set; }
        public int FoulsCommitted { get; set; }
        public int FoulsWon { get; set; }
        public int YellowCards { get; set; }
        public bool RedCard { get; set; }
        public int Offsides { get; set; }
        public float DistanceCovered { get; set; }
        public bool Injured { get; set; }

        /// <summary>Nota 1–10 (null si jugó menos de 10 minutos).</summary>
        public float? Rating { get; set; }

        public float PassAccuracy => PassesAttempted == 0 ? 0f : (float)PassesCompleted / PassesAttempted;
    }

    /// <summary>Futbolista dentro del partido. Los atributos se cachean normalizados (0–1).</summary>
    public sealed class MatchAgent
    {
        internal MatchAgent(int index, Player player, MatchTeam team, int slotIndex, Position role, float boost = 0f)
        {
            Index = index;
            Player = player;
            Team = team;
            SlotIndex = slotIndex;
            Role = role;
            var a = player.Attributes;
            Acceleration = Boost(a.N(AttributeId.Acceleration), boost);
            Speed = Boost(a.N(AttributeId.SprintSpeed), boost);
            Finishing = Boost(a.N(AttributeId.Finishing), boost);
            ShotPower = Boost(a.N(AttributeId.ShotPower), boost);
            LongShots = Boost(a.N(AttributeId.LongShots), boost);
            Positioning = Boost(a.N(AttributeId.Positioning), boost);
            ShortPassing = Boost(a.N(AttributeId.ShortPassing), boost);
            LongPassing = Boost(a.N(AttributeId.LongPassing), boost);
            Crossing = Boost(a.N(AttributeId.Crossing), boost);
            Vision = Boost(a.N(AttributeId.Vision), boost);
            Dribbling = Boost(a.N(AttributeId.Dribbling), boost);
            BallControl = Boost(a.N(AttributeId.BallControl), boost);
            Balance = Boost(a.N(AttributeId.Balance), boost);
            Agility = Boost(a.N(AttributeId.Agility), boost);
            Marking = Boost(a.N(AttributeId.Marking), boost);
            Tackling = Boost(a.N(AttributeId.Tackling), boost);
            DefensiveAwareness = Boost(a.N(AttributeId.DefensiveAwareness), boost);
            Strength = Boost(a.N(AttributeId.Strength), boost);
            Stamina = Boost(a.N(AttributeId.Stamina), boost);
            Jumping = Boost(a.N(AttributeId.Jumping), boost);
            Reactions = Boost(a.N(AttributeId.Reactions), boost);
            Composure = Boost(a.N(AttributeId.Composure), boost);
            Aggression = Boost(a.N(AttributeId.Aggression), boost);
            Heading = Boost(a.N(AttributeId.Heading), boost);
            GkDiving = Boost(a.N(AttributeId.GkDiving), boost);
            GkHandling = Boost(a.N(AttributeId.GkHandling), boost);
            GkKicking = Boost(a.N(AttributeId.GkKicking), boost);
            GkPositioning = Boost(a.N(AttributeId.GkPositioning), boost);
            GkReflexes = Boost(a.N(AttributeId.GkReflexes), boost);
            HeightFactor = MathF2.Clamp01((player.HeightCm - 160) / 45f);
            WeakFoot = player.WeakFoot;
            // La forma física previa limita la energía inicial.
            Energy = MathF2.Clamp(player.Condition.Fitness / 100f, 0.5f, 1f);
            Stats = new PlayerMatchStats { PlayerId = player.Id, ClubId = team.ClubId, Side = team.Side, Position = role };
        }

        private static float Boost(float v, float boost) => MathF2.Clamp01(v + boost);

        public int Index { get; }
        public Player Player { get; }
        public int PlayerId => Player.Id;
        public MatchTeam Team { get; }
        public int SlotIndex { get; internal set; }
        public Position Role { get; internal set; }
        public bool IsGoalkeeper => Role == Domain.Position.GK;

        public Vec2 Position { get; internal set; }
        public Vec2 Velocity { get; internal set; }
        public Vec2 Facing { get; internal set; } = new Vec2(1f, 0f);

        /// <summary>Energía 0–1 (fatiga dentro del partido).</summary>
        public float Energy { get; internal set; }

        public bool OnPitch { get; internal set; }
        public bool SentOff { get; internal set; }
        public bool Injured { get; internal set; }
        public bool HasBeenSubstituted { get; internal set; }
        public int YellowCards { get; internal set; }
        public bool IsSprinting { get; internal set; }

        public AgentAction CurrentAction { get; internal set; }

        /// <summary>Tiempo (s) desde que empezó la acción actual.</summary>
        public float ActionTime { get; internal set; }

        public PlayerMatchStats Stats { get; }

        // Atributos normalizados.
        public float Acceleration { get; }
        public float Speed { get; }
        public float Finishing { get; }
        public float ShotPower { get; }
        public float LongShots { get; }
        public float Positioning { get; }
        public float ShortPassing { get; }
        public float LongPassing { get; }
        public float Crossing { get; }
        public float Vision { get; }
        public float Dribbling { get; }
        public float BallControl { get; }
        public float Balance { get; }
        public float Agility { get; }
        public float Marking { get; }
        public float Tackling { get; }
        public float DefensiveAwareness { get; }
        public float Strength { get; }
        public float Stamina { get; }
        public float Jumping { get; }
        public float Reactions { get; }
        public float Composure { get; }
        public float Aggression { get; }
        public float Heading { get; }
        public float GkDiving { get; }
        public float GkHandling { get; }
        public float GkKicking { get; }
        public float GkPositioning { get; }
        public float GkReflexes { get; }
        public float HeightFactor { get; }
        public int WeakFoot { get; }

        // Estado interno de IA y control.
        internal Vec2 Target;
        internal bool WantsSprint;
        internal float DecisionTimer;
        internal float TackleCooldown;
        internal float ControlCooldown;
        internal float StumbleTimer;
        internal float RunTimer;
        internal Vec2 RunTarget;
        internal float HoldTimer;
        internal float DiveTimer = -1f;
        internal Vec2 DiveDir;
        internal float DiveSpeed;
        internal float RushTimer;

        /// <summary>Segundos desde el inicio de la estirada (−1 si no se está estirando). Para animaciones.</summary>
        public float DiveTime => DiveTimer;

        /// <summary>Dirección de la estirada en el plano del campo.</summary>
        public Vec2 DiveDirection => DiveDir;

        public bool IsDiving => DiveTimer >= 0f;
        internal bool HoldingInHands;
        internal int EnteredMinute;
        internal bool EverPlayed;
        internal int LeftMinute = -1;

        public override string ToString() => $"{Player.DisplayName} ({Team.Side} {Role})";
    }

    /// <summary>Contadores internos para ajustar el motor (no forman parte de las estadísticas del partido).</summary>
    public sealed class MatchDiagnostics
    {
        public int PassesKicked;
        public int PassToIntended;
        public int PassToOtherTeammate;
        public int PassIntercepted;
        public int PassDeflectedByOpponent;
        public int PassControlFailedByTeammate;
        public int PassOut;
        public int Tackles;
        public int Shots;
        public int SavesAssigned;
        public int Blocks;
        public int GoalsFromShots;
    }

    public sealed class MatchBall
    {
        public Vec3 Position { get; internal set; }
        public Vec3 Velocity { get; internal set; }

        /// <summary>Jugador en posesión (conducción o en manos del portero).</summary>
        public MatchAgent? Owner { get; internal set; }

        public MatchAgent? LastTouch { get; internal set; }
        public KickKind LastKick { get; internal set; }

        internal MatchAgent? Kicker;
        internal float TimeSinceKick;
        internal MatchAgent? IntendedReceiver;
        internal MatchAgent? PendingPassFrom;
        internal readonly HashSet<int> OffsideFlags = new HashSet<int>();
        internal readonly Dictionary<int, Vec2> OffsidePositions = new Dictionary<int, Vec2>();

        // Resolución del tiro decidida en el momento del disparo.
        internal MatchAgent? ShotBy;
        internal MatchAgent? SaveBy;
        internal bool SaveCatch;
        internal MatchAgent? BlockBy;
        internal MatchAgent? DivingGk;
        internal bool HumanSavePending;
        internal bool IsPenaltyShot;
    }

    public sealed class MatchTeam
    {
        internal MatchTeam(TeamSide side, MatchTeamSetup setup)
        {
            Side = side;
            Setup = setup;
            ClubId = setup.ClubId;
            Name = setup.Name;
            Tactics = setup.Tactics.Clone();
            Formation = Formation.Get(Tactics.Formation);
        }

        public TeamSide Side { get; }
        public MatchTeamSetup Setup { get; }
        public int ClubId { get; }
        public string Name { get; }
        public Tactics Tactics { get; }
        public Formation Formation { get; }

        /// <summary>+1 si ataca hacia X positiva, −1 en caso contrario (cambia en el descanso).</summary>
        public int AttackDir { get; internal set; }

        public int Goals { get; internal set; }
        public int SubstitutionsUsed { get; internal set; }
        public int PossessionTicks { get; internal set; }
        public int Shots { get; internal set; }
        public int ShotsOnTarget { get; internal set; }
        public int Corners { get; internal set; }
        public int Fouls { get; internal set; }
        public int Offsides { get; internal set; }

        public List<MatchAgent> Agents { get; } = new List<MatchAgent>();

        internal MatchAgent? AssistCandidate;

        /// <summary>Jugadores en el campo (lista mantenida por el motor; no modificar).</summary>
        public IReadOnlyList<MatchAgent> OnPitch => OnPitchList;

        internal readonly List<MatchAgent> OnPitchList = new List<MatchAgent>();

        public MatchAgent? Goalkeeper
        {
            get
            {
                foreach (var a in Agents) if (a.OnPitch && a.IsGoalkeeper) return a;
                return null;
            }
        }
    }

    /// <summary>Datos de entrada de un equipo para el partido.</summary>
    public sealed class MatchTeamSetup
    {
        public int ClubId { get; set; }
        public string Name { get; set; } = "";
        public string PrimaryColor { get; set; } = "#FFFFFF";
        public string SecondaryColor { get; set; } = "#000000";
        public Tactics Tactics { get; set; } = new Tactics();

        /// <summary>Once inicial en el orden de los puestos de la formación (índice 0 = portero).</summary>
        public List<Player> Starters { get; set; } = new List<Player>();

        public List<Player> Bench { get; set; } = new List<Player>();
    }
}
