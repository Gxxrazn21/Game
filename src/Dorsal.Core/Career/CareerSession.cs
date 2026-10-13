using System;
using System.Collections.Generic;
using System.Linq;
using Dorsal.Core.Domain;
using Dorsal.Core.Random;
using Dorsal.Core.Simulation;
using Dorsal.Core.World;

namespace Dorsal.Core.Career
{
    public sealed class TrialResult
    {
        public bool Success { get; set; }
        public float Score { get; set; }
        public string Message { get; set; } = "";
        public TransferOffer? Offer { get; set; }
    }

    /// <summary>
    /// Fachada del modo carrera: todas las operaciones que la interfaz puede pedir.
    /// El estado completo está en <see cref="World"/> y <see cref="State"/> (ambos serializables);
    /// toda la aleatoriedad sale de un único generador cuyo estado se guarda, para que la carrera sea reproducible.
    /// </summary>
    public sealed partial class CareerSession
    {
        private readonly DeterministicRandom _rng;

        private CareerSession(GameWorld world, CareerState state, DeterministicRandom rng)
        {
            World = world;
            State = state;
            _rng = rng;
        }

        public GameWorld World { get; }
        public CareerState State { get; }

        public Player User => World.Players[State.UserPlayerId];

        public Club? UserClub => User.ClubId.HasValue ? World.Clubs[User.ClubId.Value] : null;

        public Contract? UserContract => World.Contracts.TryGetValue(State.UserPlayerId, out var c) ? c : null;

        public CareerPhase Phase => State.Phase;

        // ---------------------------------------------------------------- creación y carga

        public static CareerSession NewCareer(PlayerCreationRequest request, ulong seed, CareerSettings? settings = null)
        {
            var world = WorldGenerator.Generate(WorldGenerator.CareerDefaults(seed));
            var rng = new DeterministicRandom(seed ^ 0x5DEECE66DUL);
            var user = PlayerCreator.Create(request, world.SeasonStartDate, rng);
            world.AddPlayer(user);
            var state = new CareerState
            {
                UserPlayerId = user.Id,
                Phase = CareerPhase.ChoosingClub,
                CareerStartDate = world.CurrentDate,
                Settings = settings ?? new CareerSettings(),
            };
            var session = new CareerSession(world, state, rng);
            session.GenerateInitialOffers();
            session.AddNews($"{user.FullName} inicia su carrera profesional. Varios clubes siguen sus pasos.", "carrera");
            session.SyncRngToState();
            return session;
        }

        /// <summary>Reconstruye la sesión a partir de un guardado.</summary>
        public static CareerSession Restore(GameWorld world, CareerState state)
        {
            if (!world.Players.ContainsKey(state.UserPlayerId)) throw new InvalidOperationException("El guardado no contiene al jugador del usuario.");
            return new CareerSession(world, state, DeterministicRandom.FromState(state.RngState));
        }

        /// <summary>Copia el estado del generador al estado persistente (llamar antes de guardar).</summary>
        public void SyncRngToState() => State.RngState = _rng.GetState();

        // ---------------------------------------------------------------- inicio: club o pruebas

        private void GenerateInitialOffers()
        {
            var lower = World.Competitions.OrderByDescending(c => c.Tier).First();
            var candidates = lower.ClubIds.Select(id => World.Clubs[id]).OrderBy(c => c.Level).ThenBy(c => c.Id).ToList();
            int count = Math.Min(3, candidates.Count);
            var picked = new List<Club>();
            while (picked.Count < count)
            {
                var c = candidates[_rng.Range(0, candidates.Count)];
                if (!picked.Contains(c)) picked.Add(c);
            }
            foreach (var club in picked) AddOffer(OfferKind.InitialContract, club, 0, SquadRole.Prospect, 3, 30);
        }

        /// <summary>Clubes donde el usuario puede pedir una prueba (todos los que aún no ha probado).</summary>
        public IEnumerable<Club> TrialCandidates() =>
            World.Clubs.Values.Where(c => !State.TrialsUsed.Contains(c.Id) && c.Id != User.ClubId).OrderBy(c => World.LeagueOf(c.Id)?.Tier ?? 9).ThenByDescending(c => c.Level);

        /// <summary>Prueba con un club: el resultado depende del nivel del jugador frente al del club.</summary>
        public TrialResult RequestTrial(int clubId)
        {
            RequirePhase(CareerPhase.ChoosingClub, CareerPhase.FreeAgent);
            if (State.TrialsUsed.Contains(clubId)) throw new InvalidOperationException("Ya hiciste una prueba con este club.");
            var club = World.Clubs[clubId];
            State.TrialsUsed.Add(clubId);
            int age = User.AgeOn(World.CurrentDate);
            // Los clubes valoran el potencial en los jóvenes.
            float effective = User.Overall + (age <= 21 ? (User.Potential - User.Overall) * 0.35f : 0f);
            float threshold = club.Level - 6f;
            float score = (float)(effective - threshold + _rng.Gaussian(0, 3));
            var result = new TrialResult { Score = score };
            if (score >= 0f)
            {
                var role = Economy.RoleFor(User.Overall, club.Level, age);
                result.Success = true;
                result.Offer = AddOffer(Phase == CareerPhase.ChoosingClub ? OfferKind.InitialContract : OfferKind.FreeAgent, club, 0, role, age <= 21 ? 3 : 2, 21);
                result.Message = $"¡Prueba superada! {club.Name} te ofrece un contrato.";
                AddNews($"{User.FullName} convence en la prueba con {club.Name}.", "mercado");
            }
            else
            {
                result.Message = score > -4f
                    ? $"{club.Name} no te ve preparado todavía, pero seguirá tus pasos."
                    : $"La prueba con {club.Name} no salió bien.";
            }
            SyncRngToState();
            return result;
        }

        // ---------------------------------------------------------------- utilidades

        public IEnumerable<TransferOffer> PendingOffers => State.Offers.Where(o => o.Status == OfferStatus.Pending);

        public int TotalRounds => World.Fixtures.Where(f => f.CompetitionId == (World.LeagueOf(UserClubOrAny())?.Id ?? 0)).Select(f => f.Round).DefaultIfEmpty(0).Max();

        private int UserClubOrAny() => User.ClubId ?? World.Clubs.Keys.Min();

        public Fixture? NextUserFixture()
        {
            if (!User.ClubId.HasValue) return null;
            int club = User.ClubId.Value;
            return World.Fixtures.Where(f => !f.IsPlayed && (f.HomeClubId == club || f.AwayClubId == club)).OrderBy(f => f.Round).FirstOrDefault();
        }

        public List<TableRow> TableFor(Competition comp) => LeagueTable.Compute(comp, World.Fixtures);

        public int LeaguePositionOf(int clubId)
        {
            var comp = World.LeagueOf(clubId);
            if (comp == null) return 0;
            return TableFor(comp).FindIndex(r => r.ClubId == clubId) + 1;
        }

        public (int min, int max) UserPotentialRange() => PlayerCreator.VisiblePotentialRange(User, World.CurrentDate);

        public void SetTraining(TrainingFocus focus, TrainingIntensity intensity)
        {
            State.TrainingFocus = focus;
            State.TrainingIntensity = intensity;
        }

        public void ToggleTransferRequest()
        {
            State.TransferRequested = !State.TransferRequested;
            AddNews(State.TransferRequested ? $"{User.FullName} comunica a su club que quiere salir." : $"{User.FullName} retira su petición de salida.", "mercado");
            if (State.TransferRequested) State.CoachTrust = Math.Max(0f, State.CoachTrust - 6f);
        }

        private void AddNews(string text, string category)
        {
            State.News.Add(new NewsItem { Date = World.CurrentDate, Text = text, Category = category });
            while (State.News.Count > 60) State.News.RemoveAt(0);
        }

        internal TransferOffer AddOffer(OfferKind kind, Club club, long fee, SquadRole role, int years, int daysValid)
        {
            long wage = Economy.FairWage(User, club.Level, role);
            if (kind == OfferKind.InitialContract) wage = Math.Max(300, wage / 2);
            var offer = new TransferOffer
            {
                Id = State.NextOfferId++,
                Kind = kind,
                ClubId = club.Id,
                Fee = fee,
                WageWeekly = wage,
                Years = years,
                Role = role,
                Status = OfferStatus.Pending,
                CreatedOn = World.CurrentDate,
                ExpiresOn = World.CurrentDate.AddDays(daysValid),
                MaxWage = (long)(wage * (1.15 + _rng.NextDouble() * 0.25)),
            };
            State.Offers.Add(offer);
            return offer;
        }

        private void RequirePhase(params CareerPhase[] allowed)
        {
            if (!allowed.Contains(State.Phase))
                throw new InvalidOperationException($"Operación no disponible en la fase {State.Phase}.");
        }

        private void Unlock(string achievement)
        {
            if (State.Achievements.Contains(achievement)) return;
            State.Achievements.Add(achievement);
            AddNews($"Logro desbloqueado: {achievement}.", "logro");
        }
    }
}
