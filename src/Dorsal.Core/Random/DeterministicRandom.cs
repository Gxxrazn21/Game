using System;

namespace Dorsal.Core.Random
{
    /// <summary>
    /// Generador pseudoaleatorio determinista (xoshiro128**), sembrado con SplitMix64.
    /// Su estado es serializable para que una carrera guardada continúe exactamente igual.
    /// No usar System.Random: su algoritmo puede cambiar entre runtimes (Unity/Mono/.NET).
    /// </summary>
    public sealed class DeterministicRandom
    {
        private uint _s0, _s1, _s2, _s3;
        private double? _spareGaussian;

        public DeterministicRandom(ulong seed)
        {
            ulong x = seed;
            ulong a = SplitMix64(ref x);
            ulong b = SplitMix64(ref x);
            _s0 = (uint)a;
            _s1 = (uint)(a >> 32);
            _s2 = (uint)b;
            _s3 = (uint)(b >> 32);
            if ((_s0 | _s1 | _s2 | _s3) == 0) _s0 = 1;
        }

        private DeterministicRandom(uint[] state)
        {
            SetState(state);
        }

        public static DeterministicRandom FromState(uint[] state) => new DeterministicRandom(state);

        public uint[] GetState() => new[] { _s0, _s1, _s2, _s3 };

        public void SetState(uint[] state)
        {
            if (state == null || state.Length != 4) throw new ArgumentException("El estado debe tener 4 elementos.", nameof(state));
            if ((state[0] | state[1] | state[2] | state[3]) == 0) throw new ArgumentException("El estado no puede ser todo ceros.", nameof(state));
            _s0 = state[0]; _s1 = state[1]; _s2 = state[2]; _s3 = state[3];
            _spareGaussian = null;
        }

        /// <summary>Deriva un generador independiente (p. ej. uno por partido) sin alterar la secuencia del padre más allá de una extracción.</summary>
        public DeterministicRandom Fork() => new DeterministicRandom(((ulong)NextUInt() << 32) | NextUInt());

        public uint NextUInt()
        {
            uint result = RotL(_s1 * 5, 7) * 9;
            uint t = _s1 << 9;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = RotL(_s3, 11);
            return result;
        }

        /// <summary>Real uniforme en [0, 1).</summary>
        public double NextDouble() => (NextUInt() >> 8) * (1.0 / 16777216.0);

        public float NextFloat() => (float)NextDouble();

        /// <summary>Entero uniforme en [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            uint span = (uint)(maxExclusive - minInclusive);
            // Rechazo para evitar sesgo de módulo.
            uint limit = uint.MaxValue - uint.MaxValue % span;
            uint r;
            do { r = NextUInt(); } while (r >= limit);
            return minInclusive + (int)(r % span);
        }

        public float Range(float min, float max) => min + (max - min) * NextFloat();

        public double Range(double min, double max) => min + (max - min) * NextDouble();

        public bool Chance(double probability) => NextDouble() < probability;

        /// <summary>Normal estándar (Box-Muller).</summary>
        public double NextGaussian()
        {
            if (_spareGaussian.HasValue)
            {
                double s = _spareGaussian.Value;
                _spareGaussian = null;
                return s;
            }
            double u1;
            do { u1 = NextDouble(); } while (u1 <= double.Epsilon);
            double u2 = NextDouble();
            double mag = Math.Sqrt(-2.0 * Math.Log(u1));
            _spareGaussian = mag * Math.Sin(2.0 * Math.PI * u2);
            return mag * Math.Cos(2.0 * Math.PI * u2);
        }

        public double Gaussian(double mean, double stdDev) => mean + stdDev * NextGaussian();

        public T Pick<T>(System.Collections.Generic.IReadOnlyList<T> items)
        {
            if (items.Count == 0) throw new ArgumentException("Lista vacía.", nameof(items));
            return items[Range(0, items.Count)];
        }

        public void Shuffle<T>(System.Collections.Generic.IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        private static uint RotL(uint x, int k) => (x << k) | (x >> (32 - k));

        private static ulong SplitMix64(ref ulong x)
        {
            ulong z = x += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
