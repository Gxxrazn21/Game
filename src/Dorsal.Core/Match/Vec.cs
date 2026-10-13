using System;

namespace Dorsal.Core.Match
{
    /// <summary>Vector 2D en metros sobre el plano del campo (X a lo largo, Y a lo ancho).</summary>
    public struct Vec2 : IEquatable<Vec2>
    {
        public float X;
        public float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static readonly Vec2 Zero = new Vec2(0f, 0f);

        public float Length => (float)Math.Sqrt(X * X + Y * Y);
        public float LengthSq => X * X + Y * Y;

        public Vec2 Normalized
        {
            get
            {
                float l = Length;
                return l > 1e-6f ? new Vec2(X / l, Y / l) : Zero;
            }
        }

        /// <summary>Perpendicular (giro de 90° a la izquierda).</summary>
        public Vec2 Perp => new Vec2(-Y, X);

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator *(float s, Vec2 a) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator /(Vec2 a, float s) => new Vec2(a.X / s, a.Y / s);

        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;
        public static float Distance(Vec2 a, Vec2 b) => (a - b).Length;
        public static float DistanceSq(Vec2 a, Vec2 b) => (a - b).LengthSq;
        public static Vec2 Lerp(Vec2 a, Vec2 b, float t) => a + (b - a) * t;

        public Vec2 ClampLength(float max)
        {
            float l = Length;
            return l > max && l > 1e-6f ? this * (max / l) : this;
        }

        public Vec2 Rotate(float radians)
        {
            float c = (float)Math.Cos(radians), s = (float)Math.Sin(radians);
            return new Vec2(X * c - Y * s, X * s + Y * c);
        }

        /// <summary>Distancia de un punto a un segmento y parámetro t∈[0,1] del punto más cercano.</summary>
        public static float DistanceToSegment(Vec2 p, Vec2 a, Vec2 b, out float t)
        {
            Vec2 ab = b - a;
            float len2 = ab.LengthSq;
            t = len2 < 1e-6f ? 0f : Math.Max(0f, Math.Min(1f, Dot(p - a, ab) / len2));
            return Distance(p, a + ab * t);
        }

        public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object? obj) => obj is Vec2 v && Equals(v);
        public override int GetHashCode() => (X.GetHashCode() * 397) ^ Y.GetHashCode();
        public override string ToString() => $"({X:F1}, {Y:F1})";
    }

    /// <summary>Vector 3D: X/Y sobre el campo, Z altura.</summary>
    public struct Vec3
    {
        public float X;
        public float Y;
        public float Z;

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vec3(Vec2 xy, float z)
        {
            X = xy.X;
            Y = xy.Y;
            Z = z;
        }

        public Vec2 XY => new Vec2(X, Y);
        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);

        public override string ToString() => $"({X:F1}, {Y:F1}, {Z:F1})";
    }

    internal static class MathF2
    {
        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public const float Deg2Rad = (float)(Math.PI / 180.0);
    }
}
