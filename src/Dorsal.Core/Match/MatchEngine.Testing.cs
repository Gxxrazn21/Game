namespace Dorsal.Core.Match
{
    /// <summary>Ganchos internos para preparar situaciones concretas en las pruebas automáticas.</summary>
    public sealed partial class MatchEngine
    {
        internal void TestGiveBall(MatchAgent a) => GainPossession(a, fromPass: false);

        internal void TestShoot(MatchAgent shooter, float power) => Shoot(shooter, power);

        internal bool TestHumanSavePending => Ball.HumanSavePending;
    }
}
