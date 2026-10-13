using System;
using Dorsal.Core.Domain;

namespace Dorsal.Core.World
{
    /// <summary>Valor de mercado y salarios (moneda ficticia: "créditos").</summary>
    public static class Economy
    {
        public static long MarketValue(Player p, DateTime date, Contract? contract)
        {
            int ovr = p.Overall;
            int age = p.AgeOn(date);
            double baseValue = 1000.0 * Math.Pow(Math.Max(1, ovr - 40), 2.6);
            double ageF = age <= 21 ? 1.4 : age <= 24 ? 1.2 : age <= 28 ? 1.0 : age <= 31 ? 0.7 : 0.4;
            double potF = 1.0 + Math.Max(0, p.Potential - ovr) / 30.0;
            double contractF = contract == null ? 0.5 : 0.6 + 0.2 * Math.Min(2.0, contract.YearsRemaining(date));
            return RoundNice((long)(baseValue * ageF * potF * contractF));
        }

        /// <summary>Salario semanal razonable para un jugador en un club de cierto nivel.</summary>
        public static long FairWage(Player p, int clubLevel, SquadRole role)
        {
            int ovr = p.Overall;
            double w = 20.0 * Math.Pow(Math.Max(1, ovr - 38), 2.0);
            w *= 0.7 + 0.3 * Math.Max(0.5, (clubLevel - 50) / 20.0);
            w *= role switch { SquadRole.Prospect => 0.6, SquadRole.Rotation => 0.9, SquadRole.Starter => 1.15, _ => 1.5 };
            return RoundNice((long)Math.Max(300, w));
        }

        public static SquadRole RoleFor(int playerOverall, int clubLevel, int age)
        {
            int diff = playerOverall - clubLevel;
            if (diff >= 5) return SquadRole.Star;
            if (diff >= 0) return SquadRole.Starter;
            if (age <= 20 && diff < -6) return SquadRole.Prospect;
            return diff >= -5 ? SquadRole.Rotation : SquadRole.Prospect;
        }

        public static long RoundNice(long v)
        {
            if (v < 1000) return Math.Max(0, v / 10 * 10);
            if (v < 100_000) return v / 100 * 100;
            if (v < 10_000_000) return v / 10_000 * 10_000;
            return v / 100_000 * 100_000;
        }
    }
}
