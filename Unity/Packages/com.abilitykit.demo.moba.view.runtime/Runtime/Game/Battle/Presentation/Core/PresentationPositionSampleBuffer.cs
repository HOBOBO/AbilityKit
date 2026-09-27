using System;

namespace AbilityKit.Game.Flow
{
    public readonly struct PresentationPosition
    {
        public PresentationPosition(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
    }

    internal readonly struct PresentationPositionSample
    {
        public PresentationPositionSample(double time, PresentationPosition position)
        {
            Time = time;
            Position = position;
        }

        public double Time { get; }
        public PresentationPosition Position { get; }
    }

    internal struct PresentationPositionSampleStorage
    {
        public const int Capacity = 8;

        private PresentationPositionSample _s0;
        private PresentationPositionSample _s1;
        private PresentationPositionSample _s2;
        private PresentationPositionSample _s3;
        private PresentationPositionSample _s4;
        private PresentationPositionSample _s5;
        private PresentationPositionSample _s6;
        private PresentationPositionSample _s7;

        public int Count { get; private set; }

        public void Clear()
        {
            this = default;
        }

        public PresentationPositionSample Get(int index)
        {
            switch (index)
            {
                case 0: return _s0;
                case 1: return _s1;
                case 2: return _s2;
                case 3: return _s3;
                case 4: return _s4;
                case 5: return _s5;
                case 6: return _s6;
                case 7: return _s7;
                default: return default;
            }
        }

        public void Set(int index, in PresentationPositionSample sample)
        {
            switch (index)
            {
                case 0: _s0 = sample; break;
                case 1: _s1 = sample; break;
                case 2: _s2 = sample; break;
                case 3: _s3 = sample; break;
                case 4: _s4 = sample; break;
                case 5: _s5 = sample; break;
                case 6: _s6 = sample; break;
                case 7: _s7 = sample; break;
            }
        }

        public void InsertAt(int index, in PresentationPositionSample sample)
        {
            if (Count >= Capacity) return;
            for (var i = Count; i > index; i--)
            {
                var previous = Get(i - 1);
                Set(i, in previous);
            }
            Set(index, in sample);
            Count++;
        }

        public void RemoveFirstAndInsertAt(int index, in PresentationPositionSample sample)
        {
            if (Count < Capacity || index <= 0) return;
            var adjustedIndex = index - 1;
            for (var i = 0; i < adjustedIndex; i++)
            {
                var next = Get(i + 1);
                Set(i, in next);
            }
            Set(adjustedIndex, in sample);
        }
    }

    public struct PresentationPositionSampleBuffer
    {
        private const double TimeEpsilon = 1e-6;
        private const double MaxExtrapolationLeadSeconds = 1d / 15d;

        private PresentationPositionSampleStorage _samples;

        public void Clear() => _samples.Clear();

        public void Add(double time, in PresentationPosition position)
        {
            var sample = new PresentationPositionSample(time, position);
            for (var i = 0; i < _samples.Count; i++)
            {
                if (Math.Abs(_samples.Get(i).Time - time) > TimeEpsilon) continue;
                _samples.Set(i, in sample);
                return;
            }

            if (_samples.Count == 0)
            {
                _samples.InsertAt(0, in sample);
                return;
            }

            var insertAt = 0;
            while (insertAt < _samples.Count && time >= _samples.Get(insertAt).Time)
            {
                insertAt++;
            }

            if (_samples.Count < PresentationPositionSampleStorage.Capacity)
            {
                _samples.InsertAt(insertAt, in sample);
            }
            else if (insertAt > 0)
            {
                _samples.RemoveFirstAndInsertAt(insertAt, in sample);
            }
        }

        public bool TryEvaluate(double time, out PresentationPosition position)
        {
            if (_samples.Count == 0)
            {
                position = default;
                return false;
            }

            if (_samples.Count == 1)
            {
                position = _samples.Get(0).Position;
                return true;
            }

            var first = _samples.Get(0);
            if (time <= first.Time)
            {
                position = first.Position;
                return true;
            }

            var last = _samples.Get(_samples.Count - 1);
            if (time >= last.Time)
            {
                var previous = _samples.Get(_samples.Count - 2);
                var delta = last.Time - previous.Time;
                var lead = Math.Min(time - last.Time, MaxExtrapolationLeadSeconds);
                var factor = delta > 0d ? (float)(lead / delta) : 0f;
                position = Lerp(previous.Position, last.Position, 1f + factor);
                return true;
            }

            for (var i = 0; i < _samples.Count - 1; i++)
            {
                var a = _samples.Get(i);
                var b = _samples.Get(i + 1);
                if (time < a.Time || time > b.Time) continue;
                var delta = b.Time - a.Time;
                position = delta > 0d
                    ? Lerp(a.Position, b.Position, (float)((time - a.Time) / delta))
                    : b.Position;
                return true;
            }

            position = last.Position;
            return true;
        }

        private static PresentationPosition Lerp(PresentationPosition a, PresentationPosition b, float t)
        {
            return new PresentationPosition(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t,
                a.Z + (b.Z - a.Z) * t);
        }
    }
}
