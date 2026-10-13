using System;
using System.Collections.Generic;
using System.Linq;
using Dorsal.Core.Domain;
using Dorsal.Core.World;

namespace Dorsal.Core.Career
{
    public sealed class NegotiationResult
    {
        public bool Accepted { get; set; }
        public bool Withdrawn { get; set; }
        public long CounterWage { get; set; }
        public string Message { get; set; } = "";
    }

    public sealed partial class CareerSession
    {
        private const int MaxNegotiationRounds = 3;

        /// <summary>¿Está abierta la ventana de traspasos en esta jornada?</summary>
        public bool IsTransferWindowOpen(int round)
        {
            int total = Math.Max(1, TotalRounds);
            int mid = total / 2;
            return round <= 3 || (round > mid && round <= mid + 3);
        }

        public bool IsTransferWindowOpenNow => IsTransferWindowOpen(State.RoundsPlayed + 1);

        private void RunMarketWeek(int round, MatchDayInfo info)
        {
            bool window = IsTransferWindowOpen(round);
            if (window && (round == 1 || round == TotalRounds / 2 + 1))
            {
                var moves = TransferAI.RunWindow(World, _rng, State.UserPlayerId, 2);
                foreach (var m in moves.Take(5)) AddNews(m, "mercado");
            }
            if (!User.ClubId.HasValue) return;
            if (window) GenerateTransferOffers();
            if (window) GenerateLoanOffer();
            GenerateRenewalOffer();
        }

        private void GenerateTransferOffers()
        {
            var current = UserClub!;
            var contract = UserContract;
            if (contract == null || contract.IsLoan) return;
            if (PendingOffers.Any(o => o.Kind == OfferKind.Transfer)) return;
            float form = User.Condition.Form;
            float share = User.RecentMinutes.Count == 0 ? 0.5f : (float)User.RecentMinutes.Average() / 90f;
            bool benched = share < 0.3f && State.RoundsPlayed >= 3;
            float p = 0.06f + Math.Max(0f, form) * 0.015f + (User.Overall - current.Level) * 0.012f + (State.TransferRequested ? 0.2f : 0f) + (benched ? 0.15f : 0f);
            p = Math.Max(0f, Math.Min(0.5f, p));
            if (!_rng.Chance(p)) return;

            int age = User.AgeOn(World.CurrentDate);
            // Si apenas juega, también interesan clubes de menos nivel donde sería titular.
            var buyers = World.Clubs.Values
                .Where(c => c.Id != current.Id && User.Overall >= c.Level - 7 &&
                            (c.Level >= current.Level - 2 || (benched && c.Level >= User.Overall - 12 && WouldStart(c))))
                .OrderBy(c => c.Id).ToList();
            if (buyers.Count == 0) return;
            var buyer = buyers[_rng.Range(0, buyers.Count)];
            long value = Economy.MarketValue(User, World.CurrentDate, contract);
            long fee = Economy.RoundNice((long)(value * (1.0 + _rng.NextDouble() * 0.35)));
            if (contract.ReleaseClause.HasValue) fee = Math.Min(fee, contract.ReleaseClause.Value);
            if (buyer.TransferBudget < fee) return;

            // El club vendedor decide primero.
            double required = contract.Role == SquadRole.Star ? 1.5 : contract.Role == SquadRole.Starter ? 1.2 : 1.0;
            if (State.TransferRequested) required *= 0.85;
            bool clauseMet = contract.ReleaseClause.HasValue && fee >= contract.ReleaseClause.Value;
            if (!clauseMet && fee < value * required)
            {
                AddNews($"{current.Name} rechaza una oferta de {buyer.Name} por {User.DisplayName}.", "mercado");
                return;
            }
            var role = Economy.RoleFor(User.Overall, buyer.Level, age);
            var offer = AddOffer(OfferKind.Transfer, buyer, fee, role, age <= 23 ? 4 : 3, 14);
            offer.WageWeekly = Math.Max(offer.WageWeekly, (long)(contract.WageWeekly * 1.1));
            offer.MaxWage = Math.Max(offer.MaxWage, (long)(offer.WageWeekly * 1.2));
            AddNews($"{buyer.Name} y {current.Name} acuerdan el traspaso de {User.DisplayName}. Falta su respuesta.", "mercado");
        }

        private void GenerateLoanOffer()
        {
            var contract = UserContract;
            if (contract == null || contract.IsLoan || State.RoundsPlayed < 4) return;
            if (PendingOffers.Any(o => o.Kind == OfferKind.Loan)) return;
            int age = User.AgeOn(World.CurrentDate);
            float share = User.RecentMinutes.Count == 0 ? 0f : (float)User.RecentMinutes.Average() / 90f;
            if (age > 23 || share >= 0.35f || !_rng.Chance(0.35)) return;
            var current = UserClub!;
            var clubs = World.Clubs.Values
                .Where(c => c.Id != current.Id && c.Level <= User.Overall + 8 && WouldStart(c))
                .OrderBy(c => c.Id).ToList();
            if (clubs.Count == 0) return;
            var club = clubs[_rng.Range(0, clubs.Count)];
            var offer = AddOffer(OfferKind.Loan, club, 0, SquadRole.Starter, 1, 14);
            offer.WageWeekly = contract.WageWeekly;
            offer.MaxWage = contract.WageWeekly;
            AddNews($"{club.Name} pide la cesión de {User.DisplayName} hasta final de temporada.", "mercado");
        }

        /// <summary>¿Sería titular el usuario en ese club? (entre los mejores de su posición).</summary>
        public bool WouldStart(Club club)
        {
            var pos = User.Position;
            int slots = Formation.Get(club.Tactics.Formation).Slots.Count(s => s.Position == pos || Positions.Familiarity(s.Position, pos) >= 0.9f);
            if (slots == 0) slots = 1;
            int mine = User.OverallInSlot(pos);
            int better = World.SquadOf(club.Id).Count(p => p.Id != User.Id && p.OverallInSlot(pos) > mine);
            return better < slots;
        }

        private void GenerateRenewalOffer()
        {
            var contract = UserContract;
            if (contract == null || contract.IsLoan) return;
            var seasonEnd = WorldGenerator.SeasonEnd(World.SeasonYear);
            if (contract.EndDate > seasonEnd) return;
            if (State.RoundsPlayed < TotalRounds * 0.55) return;
            if (State.Offers.Any(o => o.Kind == OfferKind.Renewal && o.CreatedOn.Year >= World.SeasonYear && (o.Status == OfferStatus.Pending || o.Status == OfferStatus.Rejected))) return;
            if (State.CoachTrust < 35f) return;
            var club = UserClub!;
            int age = User.AgeOn(World.CurrentDate);
            var role = Economy.RoleFor(User.Overall, club.Level, age);
            var offer = AddOffer(OfferKind.Renewal, club, 0, role, age <= 26 ? 4 : 2, 30);
            offer.WageWeekly = Economy.RoundNice((long)(Math.Max(offer.WageWeekly, contract.WageWeekly) * (1 + State.CoachTrust / 250f)));
            offer.MaxWage = (long)(offer.WageWeekly * (1.15 + _rng.NextDouble() * 0.2));
            AddNews($"{club.Name} ofrece la renovación a {User.DisplayName}.", "mercado");
        }

        private void ExpireOffers()
        {
            foreach (var o in State.Offers.Where(o => o.Status == OfferStatus.Pending && o.ExpiresOn < World.CurrentDate))
                o.Status = OfferStatus.Expired;
        }

        public void RejectOffer(int offerId)
        {
            var o = PendingOffer(offerId);
            o.Status = OfferStatus.Rejected;
            if (o.Kind == OfferKind.Renewal) State.CoachTrust = Math.Max(0f, State.CoachTrust - 3f);
            AddNews($"{User.DisplayName} rechaza la oferta de {World.Clubs[o.ClubId].Name}.", "mercado");
        }

        /// <summary>Negociación salarial: el club acepta si no supera su máximo; si no, contraoferta (3 rondas como máximo).</summary>
        public NegotiationResult Negotiate(int offerId, long requestedWage)
        {
            var o = PendingOffer(offerId);
            var result = new NegotiationResult();
            o.NegotiationRound++;
            if (requestedWage <= o.MaxWage)
            {
                o.WageWeekly = Math.Max(o.WageWeekly, requestedWage);
                result.Accepted = true;
                result.Message = $"{World.Clubs[o.ClubId].Name} acepta tus condiciones: {o.WageWeekly} cr/semana.";
            }
            else if (o.NegotiationRound >= MaxNegotiationRounds || requestedWage > o.MaxWage * 2)
            {
                o.Status = OfferStatus.Withdrawn;
                result.Withdrawn = true;
                result.Message = $"{World.Clubs[o.ClubId].Name} retira la oferta: tus exigencias son excesivas.";
                AddNews(result.Message, "mercado");
            }
            else
            {
                o.WageWeekly = Math.Min(o.MaxWage, (o.WageWeekly + o.MaxWage) / 2 + (o.MaxWage - o.WageWeekly) / 4);
                result.CounterWage = o.WageWeekly;
                result.Message = $"Contraoferta: {o.WageWeekly} cr/semana.";
            }
            SyncRngToState();
            return result;
        }

        /// <summary>Acepta una oferta pendiente y aplica sus consecuencias (traspaso, cesión, renovación o fichaje).</summary>
        public void AcceptOffer(int offerId)
        {
            RequirePhase(CareerPhase.ChoosingClub, CareerPhase.InSeason, CareerPhase.FreeAgent, CareerPhase.SeasonEnded);
            var o = PendingOffer(offerId);
            var club = World.Clubs[o.ClubId];
            var start = World.CurrentDate;
            var end = WorldGenerator.ContractEnd(start, o.Years);

            switch (o.Kind)
            {
                case OfferKind.Renewal:
                {
                    var c = UserContract ?? throw new InvalidOperationException("No hay contrato que renovar.");
                    c.EndDate = WorldGenerator.ContractEnd(start, o.Years + 1);
                    c.WageWeekly = o.WageWeekly;
                    c.Role = o.Role;
                    c.AppearanceBonus = o.WageWeekly / 10;
                    c.GoalBonus = o.WageWeekly / 5;
                    State.CoachTrust = Math.Min(100f, State.CoachTrust + 3f);
                    AddNews($"{User.DisplayName} renueva con {club.Name} hasta {c.EndDate.Year}.", "mercado");
                    break;
                }
                case OfferKind.Loan:
                {
                    var parent = UserContract ?? throw new InvalidOperationException("Sin contrato con el club propietario.");
                    if (club.Id == parent.ClubId) throw new InvalidOperationException("No se puede ceder al mismo club.");
                    State.ParentContract = parent.Clone();
                    var loan = new Contract
                    {
                        PlayerId = User.Id,
                        ClubId = club.Id,
                        StartDate = start,
                        EndDate = WorldGenerator.SeasonEnd(World.SeasonYear),
                        WageWeekly = o.WageWeekly,
                        AppearanceBonus = parent.AppearanceBonus,
                        GoalBonus = parent.GoalBonus,
                        Role = o.Role,
                        IsLoan = true,
                        ParentClubId = parent.ClubId,
                    };
                    World.TransferPlayer(User.Id, club.Id, 0, loan, TransferType.Loan);
                    StartAtNewClub();
                    AddNews($"{User.DisplayName} llega cedido a {club.Name}.", "mercado");
                    break;
                }
                default:
                {
                    var contract = new Contract
                    {
                        PlayerId = User.Id,
                        ClubId = club.Id,
                        StartDate = start,
                        EndDate = end,
                        WageWeekly = o.WageWeekly,
                        AppearanceBonus = o.WageWeekly / 10,
                        GoalBonus = o.WageWeekly / 5,
                        Role = o.Role,
                        ReleaseClause = o.Kind == OfferKind.Transfer ? Economy.RoundNice(o.Fee * 3) : (long?)null,
                    };
                    var type = o.Kind == OfferKind.Transfer ? TransferType.Transfer : TransferType.FreeAgentSigning;
                    World.TransferPlayer(User.Id, club.Id, o.Fee, contract, type);
                    State.ParentContract = null;
                    StartAtNewClub();
                    AddNews(o.Kind == OfferKind.Transfer
                        ? $"¡Bombazo! {User.DisplayName} ficha por {club.Name} por {o.Fee:N0} cr."
                        : $"{User.DisplayName} firma con {club.Name} hasta {contract.EndDate.Year}.", "mercado");
                    if (o.Kind == OfferKind.Transfer) Unlock("Primer traspaso");
                    break;
                }
            }
            o.Status = OfferStatus.Accepted;
            foreach (var other in PendingOffers.Where(x => x.Id != o.Id && x.Kind != OfferKind.Renewal).ToList())
                other.Status = OfferStatus.Withdrawn;
            if (o.Kind != OfferKind.Renewal)
                foreach (var r in PendingOffers.Where(x => x.Kind == OfferKind.Renewal).ToList()) r.Status = OfferStatus.Withdrawn;

            if (State.Phase == CareerPhase.ChoosingClub || State.Phase == CareerPhase.FreeAgent)
            {
                State.Phase = CareerPhase.InSeason;
                if (State.Objectives.Count == 0 || State.Objectives.All(x => x.Evaluated)) GenerateObjectives();
            }
            SyncRngToState();
        }

        private void StartAtNewClub()
        {
            State.CoachTrust = 45f + Math.Min(15f, User.Reputation / 5f);
            State.TransferRequested = false;
            if (State.Phase == CareerPhase.InSeason) GenerateObjectives();
        }

        private TransferOffer PendingOffer(int id)
        {
            var o = State.Offers.FirstOrDefault(x => x.Id == id) ?? throw new ArgumentException($"Oferta inexistente {id}.");
            if (o.Status != OfferStatus.Pending) throw new InvalidOperationException("La oferta ya no está disponible.");
            return o;
        }

        /// <summary>Ofertas para un agente libre (tras quedarse sin contrato).</summary>
        private void GenerateFreeAgentOffers()
        {
            int age = User.AgeOn(World.CurrentDate);
            var clubs = World.Clubs.Values.Where(c => User.Overall >= c.Level - 7).OrderBy(c => c.Id).ToList();
            int n = Math.Min(clubs.Count, _rng.Range(1, 4));
            var picked = new HashSet<int>();
            for (int i = 0; i < n * 3 && picked.Count < n; i++)
            {
                var c = clubs[_rng.Range(0, clubs.Count)];
                if (!picked.Add(c.Id)) continue;
                AddOffer(OfferKind.FreeAgent, c, 0, Economy.RoleFor(User.Overall, c.Level, age), age >= 32 ? 1 : 2, 45);
            }
            if (picked.Count > 0) AddNews($"{picked.Count} clubes se interesan por {User.DisplayName}, que está sin equipo.", "mercado");
        }
    }

    /// <summary>Mercado de la IA: cubre necesidades de plantilla respetando presupuestos y tamaños.</summary>
    public static class TransferAI
    {
        private static readonly (string name, Position[] positions, int min)[] Groups =
        {
            ("porteros", new[] { Position.GK }, 2),
            ("centrales", new[] { Position.CB }, 3),
            ("laterales", new[] { Position.LB, Position.RB }, 3),
            ("medios", new[] { Position.CDM, Position.CM, Position.CAM }, 5),
            ("bandas", new[] { Position.LM, Position.RM, Position.LW, Position.RW }, 3),
            ("delanteros", new[] { Position.ST }, 2),
        };

        public const int MinSquad = 20;
        public const int MaxSquad = 28;

        public static List<string> RunWindow(GameWorld world, Random.DeterministicRandom rng, int userPlayerId, int maxPerClub)
        {
            var news = new List<string>();
            var clubs = world.Clubs.Values.OrderBy(c => c.Id).ToList();
            rng.Shuffle(clubs);
            foreach (var buyer in clubs)
            {
                int signings = 0;
                foreach (var g in Groups)
                {
                    if (signings >= maxPerClub || buyer.SquadPlayerIds.Count >= MaxSquad) break;
                    var mine = world.SquadOf(buyer.Id).Where(p => g.positions.Contains(p.Position)).ToList();
                    bool deficit = mine.Count < g.min;
                    bool upgrade = !deficit && rng.Chance(0.15) && mine.Count > 0 && mine.Max(p => p.Overall) < buyer.Level - 3;
                    if (!deficit && !upgrade) continue;
                    int minOvr = deficit ? buyer.Level - 10 : mine.Max(p => p.Overall) + 1;

                    var candidates = world.Players.Values
                        .Where(p => !p.Retired && p.Id != userPlayerId && g.positions.Contains(p.Position) && p.Overall >= minOvr && p.Overall <= buyer.Level + 6)
                        .Where(p => p.ClubId != buyer.Id)
                        .Where(p => !world.Contracts.TryGetValue(p.Id, out var c) || !c.IsLoan)
                        .OrderByDescending(p => p.Overall).ThenBy(p => p.Id)
                        .Take(25).ToList();
                    foreach (var target in candidates)
                    {
                        long fee = 0;
                        if (target.ClubId.HasValue)
                        {
                            var seller = world.Clubs[target.ClubId.Value];
                            if (seller.SquadPlayerIds.Count <= MinSquad) continue;
                            if (world.SquadOf(seller.Id).Count(p => g.positions.Contains(p.Position)) <= g.min) continue;
                            if (seller.Level > buyer.Level + 3) continue; // los grandes no venden a los pequeños
                            long value = Economy.MarketValue(target, world.CurrentDate, world.Contracts.TryGetValue(target.Id, out var tc) ? tc : null);
                            fee = Economy.RoundNice((long)(value * rng.Range(1.05, 1.3)));
                            if (fee > buyer.TransferBudget * 0.7) continue;
                        }
                        int age = target.AgeOn(world.CurrentDate);
                        var role = Economy.RoleFor(target.Overall, buyer.Level, age);
                        var contract = new Contract
                        {
                            PlayerId = target.Id,
                            ClubId = buyer.Id,
                            StartDate = world.CurrentDate,
                            EndDate = WorldGenerator.ContractEnd(world.CurrentDate, age <= 26 ? rng.Range(2, 5) : rng.Range(1, 3)),
                            WageWeekly = Economy.FairWage(target, buyer.Level, role),
                            Role = role,
                        };
                        contract.AppearanceBonus = contract.WageWeekly / 10;
                        contract.GoalBonus = contract.WageWeekly / 5;
                        var fromName = target.ClubId.HasValue ? world.Clubs[target.ClubId.Value].Name : null;
                        world.TransferPlayer(target.Id, buyer.Id, fee, contract, target.ClubId.HasValue ? TransferType.Transfer : TransferType.FreeAgentSigning);
                        news.Add(fromName != null
                            ? $"{buyer.Name} ficha a {target.DisplayName} ({fromName}) por {fee:N0} cr."
                            : $"{buyer.Name} incorpora al agente libre {target.DisplayName}.");
                        signings++;
                        break;
                    }
                }
            }
            return news;
        }

        /// <summary>Deja libres a los sobrantes de plantillas demasiado grandes (nunca al usuario).</summary>
        public static void TrimSquads(GameWorld world, int userPlayerId)
        {
            foreach (var club in world.Clubs.Values.OrderBy(c => c.Id))
            {
                while (club.SquadPlayerIds.Count > MaxSquad)
                {
                    var surplus = world.SquadOf(club.Id)
                        .Where(p => p.Id != userPlayerId && !(world.Contracts.TryGetValue(p.Id, out var c) && c.IsLoan))
                        .Where(p => p.Position != Position.GK || world.SquadOf(club.Id).Count(x => x.Position == Position.GK) > 2)
                        .OrderBy(p => p.Overall).ThenByDescending(p => p.Id).FirstOrDefault();
                    if (surplus == null) break;
                    world.ReleasePlayer(surplus.Id);
                }
            }
        }
    }
}
