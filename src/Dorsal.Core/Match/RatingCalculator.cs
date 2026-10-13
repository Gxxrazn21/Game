using System;
using Dorsal.Core.Domain;

namespace Dorsal.Core.Match
{
    /// <summary>
    /// Nota individual 1–10 a partir de las estadísticas del partido.
    /// Parte de 6,0 y suma/resta contribuciones según la línea del jugador.
    /// </summary>
    public static class RatingCalculator
    {
        public const int MinMinutesForRating = 10;

        public static float? Compute(PlayerMatchStats s, int teamGoals, int opponentGoals)
        {
            if (s.MinutesPlayed < MinMinutesForRating) return null;
            var line = Positions.LineOf(s.Position);
            float r = 6.0f;

            r += s.Goals * (line == PositionLine.Attack ? 0.9f : 1.1f);
            r += s.Assists * 0.6f;
            r += s.KeyPasses * 0.15f;
            r += s.ShotsOnTarget * 0.08f;
            r -= Math.Max(0, s.Shots - s.ShotsOnTarget) * 0.04f;
            r += s.DribblesCompleted * 0.06f;
            r += s.TacklesWon * 0.1f;
            r -= s.TacklesLost * 0.03f;
            r += s.Interceptions * 0.08f;
            r += s.Blocks * 0.15f;
            r -= s.PossessionLost * 0.03f;
            r -= s.FoulsCommitted * 0.05f;
            r -= s.Offsides * 0.05f;
            r -= s.YellowCards * 0.3f;
            if (s.RedCard) r -= 1.5f;

            // Precisión de pase respecto a un 75 % de referencia.
            if (s.PassesAttempted >= 5)
                r += (s.PassAccuracy - 0.75f) * Math.Min(s.PassesAttempted, 40) * 0.04f;

            if (line == PositionLine.Goalkeeper)
            {
                r += s.Saves * 0.3f;
                r -= s.GoalsConceded * 0.3f;
                if (opponentGoals == 0 && s.MinutesPlayed >= 60) r += 0.6f;
            }
            else if (line == PositionLine.Defence)
            {
                r -= s.GoalsConceded * 0.12f;
                if (opponentGoals == 0 && s.MinutesPlayed >= 60) r += 0.4f;
            }

            // Resultado del equipo.
            if (teamGoals > opponentGoals) r += 0.25f;
            else if (teamGoals < opponentGoals) r -= 0.25f;

            // Las contribuciones pesan menos si se juegan pocos minutos.
            float minutesFactor = Math.Min(1f, s.MinutesPlayed / 60f);
            r = 6.0f + (r - 6.0f) * (0.6f + 0.4f * minutesFactor);

            r = Math.Max(3.0f, Math.Min(10.0f, r));
            return (float)Math.Round(r, 1);
        }
    }
}
