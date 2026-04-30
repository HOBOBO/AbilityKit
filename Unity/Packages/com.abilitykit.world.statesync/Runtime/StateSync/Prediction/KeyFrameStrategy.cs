using System;

namespace AbilityKit.Ability.StateSync
{
    public enum KeyFrameLevel
    {
        FullSnapshot = 0,
        MajorDelta = 1,
        MinorDelta = 2,
        Incremental = 3
    }

    public sealed class KeyFrameStrategy
    {
        private readonly int _fullSnapshotInterval;
        private readonly int _majorDeltaInterval;
        private readonly int _minorDeltaInterval;

        public int FullSnapshotInterval => _fullSnapshotInterval;
        public int MajorDeltaInterval => _majorDeltaInterval;
        public int MinorDeltaInterval => _minorDeltaInterval;

        public KeyFrameStrategy(
            int fullSnapshotInterval = 60,
            int majorDeltaInterval = 10,
            int minorDeltaInterval = 3)
        {
            if (fullSnapshotInterval <= 0) throw new ArgumentOutOfRangeException(nameof(fullSnapshotInterval));
            if (majorDeltaInterval <= 0) throw new ArgumentOutOfRangeException(nameof(majorDeltaInterval));
            if (minorDeltaInterval <= 0) throw new ArgumentOutOfRangeException(nameof(minorDeltaInterval));

            _fullSnapshotInterval = fullSnapshotInterval;
            _majorDeltaInterval = majorDeltaInterval;
            _minorDeltaInterval = minorDeltaInterval;
        }

        public static KeyFrameStrategy OverwatchStyle()
        {
            return new KeyFrameStrategy(
                fullSnapshotInterval: 60,
                majorDeltaInterval: 10,
                minorDeltaInterval: 1);
        }

        public static KeyFrameStrategy LowBandwidth()
        {
            return new KeyFrameStrategy(
                fullSnapshotInterval: 300,
                majorDeltaInterval: 30,
                minorDeltaInterval: 5);
        }

        public static KeyFrameStrategy HighQuality()
        {
            return new KeyFrameStrategy(
                fullSnapshotInterval: 30,
                majorDeltaInterval: 5,
                minorDeltaInterval: 1);
        }

        public bool IsKeyFrame(int frame)
        {
            return frame % _fullSnapshotInterval == 0;
        }

        public bool IsMajorDeltaFrame(int frame)
        {
            return !IsKeyFrame(frame) && frame % _majorDeltaInterval == 0;
        }

        public bool IsMinorDeltaFrame(int frame)
        {
            return !IsKeyFrame(frame) && !IsMajorDeltaFrame(frame) && frame % _minorDeltaInterval == 0;
        }

        public KeyFrameLevel GetKeyFrameLevel(int frame)
        {
            if (IsKeyFrame(frame)) return KeyFrameLevel.FullSnapshot;
            if (IsMajorDeltaFrame(frame)) return KeyFrameLevel.MajorDelta;
            if (IsMinorDeltaFrame(frame)) return KeyFrameLevel.MinorDelta;
            return KeyFrameLevel.Incremental;
        }

        public int GetNextKeyFrame(int currentFrame)
        {
            int next = currentFrame + 1;
            while (!IsKeyFrame(next))
            {
                next++;
                if (next - currentFrame > _fullSnapshotInterval)
                    return currentFrame + 1;
            }
            return next;
        }

        public int GetPreviousKeyFrame(int currentFrame)
        {
            int prev = currentFrame - 1;
            while (prev >= 0 && !IsKeyFrame(prev))
            {
                prev--;
            }
            return prev >= 0 ? prev : 0;
        }

        public int EstimateBandwidth(int frame, int entityCount, int averageEntitySize)
        {
            var level = GetKeyFrameLevel(frame);
            switch (level)
            {
                case KeyFrameLevel.FullSnapshot:
                    return entityCount * averageEntitySize * 2;
                case KeyFrameLevel.MajorDelta:
                    return entityCount * averageEntitySize / 2;
                case KeyFrameLevel.MinorDelta:
                    return entityCount * averageEntitySize / 4;
                case KeyFrameLevel.Incremental:
                    return entityCount * averageEntitySize / 8;
                default:
                    return entityCount * averageEntitySize / 4;
            }
        }
    }
}
