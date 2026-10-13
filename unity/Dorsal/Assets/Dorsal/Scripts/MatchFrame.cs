using Dorsal.Core.Match;
using UnityEngine;

namespace Dorsal.Unity
{
    /// <summary>Instantánea visual del partido en un paso (para interpolar y para las repeticiones).</summary>
    public sealed class MatchFrame
    {
        public readonly Vector3[] Pos;
        public readonly Quaternion[] Rot;
        public readonly float[] Speed;
        public readonly AgentAction[] Action;
        public readonly float[] ActionTime;
        public readonly float[] DiveTime;
        public readonly int[] DiveSide;
        public readonly bool[] OnPitch;
        public Vector3 Ball;
        public float Clock;

        public MatchFrame(int agents)
        {
            Pos = new Vector3[agents];
            Rot = new Quaternion[agents];
            Speed = new float[agents];
            Action = new AgentAction[agents];
            ActionTime = new float[agents];
            DiveTime = new float[agents];
            DiveSide = new int[agents];
            OnPitch = new bool[agents];
        }

        public void Capture(MatchEngine e)
        {
            var agents = e.Agents;
            for (int i = 0; i < agents.Count && i < Pos.Length; i++)
            {
                var a = agents[i];
                Pos[i] = PitchSpace.ToUnity(a.Position);
                var f = a.Facing;
                Rot[i] = Quaternion.LookRotation(new Vector3(f.X, 0f, f.Y).sqrMagnitude > 1e-4f ? new Vector3(f.X, 0f, f.Y) : Vector3.forward, Vector3.up);
                Speed[i] = a.Velocity.Length;
                Action[i] = a.CurrentAction;
                ActionTime[i] = a.ActionTime;
                DiveTime[i] = a.DiveTime;
                OnPitch[i] = a.OnPitch;
                var d = a.DiveDirection;
                float side = d.X * f.Y - d.Y * f.X; // proyección sobre la derecha del jugador
                DiveSide[i] = d.LengthSq < 0.01f ? 0 : (side > 0.3f ? 1 : side < -0.3f ? -1 : 0);
            }
            Ball = PitchSpace.ToUnity(e.Ball.Position);
            Clock = e.ClockMinute;
        }

        public void CopyFrom(MatchFrame o)
        {
            System.Array.Copy(o.Pos, Pos, Pos.Length);
            System.Array.Copy(o.Rot, Rot, Rot.Length);
            System.Array.Copy(o.Speed, Speed, Speed.Length);
            System.Array.Copy(o.Action, Action, Action.Length);
            System.Array.Copy(o.ActionTime, ActionTime, ActionTime.Length);
            System.Array.Copy(o.DiveTime, DiveTime, DiveTime.Length);
            System.Array.Copy(o.DiveSide, DiveSide, DiveSide.Length);
            System.Array.Copy(o.OnPitch, OnPitch, OnPitch.Length);
            Ball = o.Ball;
            Clock = o.Clock;
        }
    }

    /// <summary>
    /// Graba los últimos segundos del partido y los reproduce a cámara lenta (goles y paradas).
    /// Reproduce exactamente lo que ocurrió: lee instantáneas, no vuelve a simular.
    /// </summary>
    public sealed class ReplaySystem
    {
        private readonly MatchFrame[] _buffer;
        private int _head;   // siguiente posición de escritura
        private int _count;
        private float _playIndex = -1f;
        private int _startIndex;
        private int _length;
        private readonly int _rate;

        public ReplaySystem(int agents, int tickRate, float seconds = 8f)
        {
            _rate = tickRate;
            int n = Mathf.Max(10, Mathf.CeilToInt(seconds * tickRate));
            _buffer = new MatchFrame[n];
            for (int i = 0; i < n; i++) _buffer[i] = new MatchFrame(agents);
        }

        public bool Playing => _playIndex >= 0f;

        /// <summary>Velocidad de reproducción (0,5 = cámara lenta).</summary>
        public float PlaybackSpeed { get; set; } = 0.5f;

        public void Record(MatchEngine e)
        {
            _buffer[_head].Capture(e);
            _head = (_head + 1) % _buffer.Length;
            if (_count < _buffer.Length) _count++;
        }

        public void Clear() => _count = 0;

        /// <summary>Empieza a reproducir los últimos <paramref name="seconds"/> segundos grabados.</summary>
        public bool Start(float seconds)
        {
            int frames = Mathf.Min(_count, Mathf.CeilToInt(seconds * _rate));
            if (frames < 10) return false;
            _length = frames;
            _startIndex = (_head - frames + _buffer.Length) % _buffer.Length;
            _playIndex = 0f;
            return true;
        }

        public void Stop() => _playIndex = -1f;

        /// <summary>Avanza la reproducción y devuelve los dos fotogramas a interpolar y el factor.</summary>
        public bool Advance(float dt, out MatchFrame a, out MatchFrame b, out float t)
        {
            a = b = null;
            t = 0f;
            if (!Playing) return false;
            _playIndex += dt * _rate * PlaybackSpeed;
            if (_playIndex >= _length - 1)
            {
                Stop();
                return false;
            }
            int i0 = Mathf.FloorToInt(_playIndex);
            t = _playIndex - i0;
            a = _buffer[(_startIndex + i0) % _buffer.Length];
            b = _buffer[(_startIndex + i0 + 1) % _buffer.Length];
            return true;
        }
    }
}
