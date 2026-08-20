using System;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    public enum BattleDiagnosticFrameCursorChangeReason
    {
        None = 0,
        FollowLiveAdvanced = 1,
        UserSelectedFrame = 2,
        SelectionNavigation = 3,
        RetainedRangeClamped = 4,
        SessionChanged = 5
    }

    public readonly struct BattleDiagnosticFrameRange : IEquatable<BattleDiagnosticFrameRange>
    {
        public BattleDiagnosticFrameRange(int firstFrame, int lastFrame)
        {
            FirstFrame = firstFrame;
            LastFrame = lastFrame;
        }

        public int FirstFrame { get; }
        public int LastFrame { get; }

        public bool IsValid =>
            BattleDiagnosticFrames.IsValid(FirstFrame) &&
            LastFrame >= FirstFrame;

        public bool Contains(int frame)
        {
            return IsValid && frame >= FirstFrame && frame <= LastFrame;
        }

        public bool Intersects(BattleDiagnosticFrameRange other)
        {
            return IsValid && other.IsValid &&
                   FirstFrame <= other.LastFrame &&
                   LastFrame >= other.FirstFrame;
        }

        public BattleDiagnosticFrameRange Intersect(BattleDiagnosticFrameRange other)
        {
            if (!Intersects(other))
            {
                return new BattleDiagnosticFrameRange(
                    BattleDiagnosticFrames.Invalid,
                    BattleDiagnosticFrames.Invalid);
            }

            return new BattleDiagnosticFrameRange(
                Math.Max(FirstFrame, other.FirstFrame),
                Math.Min(LastFrame, other.LastFrame));
        }

        public int Clamp(int frame)
        {
            if (!IsValid)
            {
                return BattleDiagnosticFrames.Invalid;
            }

            if (frame < FirstFrame)
            {
                return FirstFrame;
            }

            return frame > LastFrame ? LastFrame : frame;
        }

        public bool Equals(BattleDiagnosticFrameRange other)
        {
            return FirstFrame == other.FirstFrame && LastFrame == other.LastFrame;
        }

        public override bool Equals(object obj)
        {
            return obj is BattleDiagnosticFrameRange other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (FirstFrame * 397) ^ LastFrame;
            }
        }

        public static bool operator ==(BattleDiagnosticFrameRange left, BattleDiagnosticFrameRange right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BattleDiagnosticFrameRange left, BattleDiagnosticFrameRange right)
        {
            return !left.Equals(right);
        }
    }

    public enum BattleDiagnosticTimeRangeMode
    {
        Auto = 0,
        Fixed = 1
    }

    public enum BattleDiagnosticTimeRangeChangeReason
    {
        None = 0,
        UserSelected = 1,
        CursorFocused = 2,
        ResetToAuto = 3,
        RetainedRangeClamped = 4,
        SessionChanged = 5,
        Brushed = 6,
        Zoomed = 7,
        Panned = 8,
        HistoryNavigation = 9
    }

    public readonly struct BattleDiagnosticTimeRange : IEquatable<BattleDiagnosticTimeRange>
    {
        private BattleDiagnosticTimeRange(
            BattleDiagnosticTimeRangeMode mode,
            int firstFrame,
            int lastFrame,
            BattleDiagnosticTimeRangeChangeReason changeReason)
        {
            Mode = mode;
            FirstFrame = firstFrame;
            LastFrame = lastFrame;
            ChangeReason = changeReason;
        }

        public BattleDiagnosticTimeRangeMode Mode { get; }
        public int FirstFrame { get; }
        public int LastFrame { get; }
        public BattleDiagnosticTimeRangeChangeReason ChangeReason { get; }

        public bool IsAuto => Mode == BattleDiagnosticTimeRangeMode.Auto;
        public bool IsFixed => Mode == BattleDiagnosticTimeRangeMode.Fixed && Range.IsValid;
        public BattleDiagnosticFrameRange Range => IsAuto
            ? new BattleDiagnosticFrameRange(
                BattleDiagnosticFrames.Invalid,
                BattleDiagnosticFrames.Invalid)
            : new BattleDiagnosticFrameRange(FirstFrame, LastFrame);

        public static BattleDiagnosticTimeRange Auto(
            BattleDiagnosticTimeRangeChangeReason changeReason =
                BattleDiagnosticTimeRangeChangeReason.None)
        {
            return new BattleDiagnosticTimeRange(
                BattleDiagnosticTimeRangeMode.Auto,
                BattleDiagnosticFrames.Invalid,
                BattleDiagnosticFrames.Invalid,
                changeReason);
        }

        public static BattleDiagnosticTimeRange Fixed(
            int firstFrame,
            int lastFrame,
            BattleDiagnosticTimeRangeChangeReason changeReason =
                BattleDiagnosticTimeRangeChangeReason.UserSelected)
        {
            if (!BattleDiagnosticFrames.IsValid(firstFrame))
            {
                throw new ArgumentOutOfRangeException(nameof(firstFrame));
            }

            if (!BattleDiagnosticFrames.IsValid(lastFrame))
            {
                throw new ArgumentOutOfRangeException(nameof(lastFrame));
            }

            return new BattleDiagnosticTimeRange(
                BattleDiagnosticTimeRangeMode.Fixed,
                Math.Min(firstFrame, lastFrame),
                Math.Max(firstFrame, lastFrame),
                changeReason);
        }

        public BattleDiagnosticFrameRange Resolve(BattleDiagnosticFrameRange automaticRange)
        {
            return IsFixed ? Range : automaticRange;
        }

        public BattleDiagnosticTimeRange WithChangeReason(
            BattleDiagnosticTimeRangeChangeReason changeReason)
        {
            return new BattleDiagnosticTimeRange(
                Mode,
                FirstFrame,
                LastFrame,
                changeReason);
        }

        public BattleDiagnosticTimeRange Zoom(int anchorFrame, double scale)
        {
            if (!IsFixed)
            {
                return this;
            }

            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(scale));
            }

            var range = Range;
            var anchor = range.Clamp(anchorFrame);
            var frameCount = (long)range.LastFrame - range.FirstFrame + 1L;
            var scaledFrameCount = Math.Round(frameCount * scale);
            var nextFrameCount = scaledFrameCount >= (double)int.MaxValue + 1d
                ? (long)int.MaxValue + 1L
                : Math.Max(1L, (long)scaledFrameCount);
            var anchorRatio = frameCount <= 1L
                ? 0.5d
                : (anchor - (double)range.FirstFrame) / (frameCount - 1L);
            var first = (long)anchor - (long)Math.Round((nextFrameCount - 1L) * anchorRatio);
            var last = first + nextFrameCount - 1L;
            ConstrainBounds(ref first, ref last);
            return Fixed(
                (int)first,
                (int)last,
                BattleDiagnosticTimeRangeChangeReason.Zoomed);
        }

        public BattleDiagnosticTimeRange Pan(int frameDelta)
        {
            if (!IsFixed || frameDelta == 0)
            {
                return this;
            }

            var first = (long)FirstFrame + frameDelta;
            var last = (long)LastFrame + frameDelta;
            ConstrainBounds(ref first, ref last);
            return Fixed(
                (int)first,
                (int)last,
                BattleDiagnosticTimeRangeChangeReason.Panned);
        }

        public BattleDiagnosticTimeRange ConstrainTo(BattleDiagnosticFrameRange retainedRange)
        {
            if (!IsFixed || !retainedRange.IsValid)
            {
                return this;
            }

            var first = retainedRange.Clamp(FirstFrame);
            var last = retainedRange.Clamp(LastFrame);
            if (first == FirstFrame && last == LastFrame)
            {
                return this;
            }

            return Fixed(
                first,
                last,
                BattleDiagnosticTimeRangeChangeReason.RetainedRangeClamped);
        }

        public bool Equals(BattleDiagnosticTimeRange other)
        {
            return Mode == other.Mode &&
                   FirstFrame == other.FirstFrame &&
                   LastFrame == other.LastFrame &&
                   ChangeReason == other.ChangeReason;
        }

        public override bool Equals(object obj)
        {
            return obj is BattleDiagnosticTimeRange other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)Mode;
                hashCode = (hashCode * 397) ^ FirstFrame;
                hashCode = (hashCode * 397) ^ LastFrame;
                hashCode = (hashCode * 397) ^ (int)ChangeReason;
                return hashCode;
            }
        }

        public static bool operator ==(
            BattleDiagnosticTimeRange left,
            BattleDiagnosticTimeRange right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            BattleDiagnosticTimeRange left,
            BattleDiagnosticTimeRange right)
        {
            return !left.Equals(right);
        }

        private static void ConstrainBounds(ref long first, ref long last)
        {
            if (first < 0L)
            {
                last -= first;
                first = 0L;
            }

            if (last > int.MaxValue)
            {
                var overflow = last - int.MaxValue;
                first = Math.Max(0L, first - overflow);
                last = int.MaxValue;
            }
        }
    }

    public readonly struct BattleDiagnosticFrameCursor : IEquatable<BattleDiagnosticFrameCursor>
    {
        public BattleDiagnosticFrameCursor(
            int frame,
            bool followsLive,
            BattleDiagnosticFrameCursorChangeReason changeReason)
        {
            Frame = frame;
            FollowsLive = followsLive;
            ChangeReason = changeReason;
        }

        public int Frame { get; }
        public bool FollowsLive { get; }
        public BattleDiagnosticFrameCursorChangeReason ChangeReason { get; }

        public bool HasFrame => BattleDiagnosticFrames.IsValid(Frame);

        public static BattleDiagnosticFrameCursor CreateFollowingLive(int latestCompleteFrame)
        {
            return new BattleDiagnosticFrameCursor(
                latestCompleteFrame,
                true,
                BattleDiagnosticFrameCursorChangeReason.SessionChanged);
        }

        public BattleDiagnosticFrameCursor SetFollowLive(bool followLive, int latestCompleteFrame)
        {
            if (!followLive)
            {
                return new BattleDiagnosticFrameCursor(Frame, false, ChangeReason);
            }

            return new BattleDiagnosticFrameCursor(
                latestCompleteFrame,
                true,
                BattleDiagnosticFrameCursorChangeReason.FollowLiveAdvanced);
        }

        public BattleDiagnosticFrameCursor AdvanceLive(int latestCompleteFrame)
        {
            if (!FollowsLive || latestCompleteFrame == Frame)
            {
                return this;
            }

            return new BattleDiagnosticFrameCursor(
                latestCompleteFrame,
                true,
                BattleDiagnosticFrameCursorChangeReason.FollowLiveAdvanced);
        }

        public BattleDiagnosticFrameCursor SelectFrame(int frame)
        {
            if (!BattleDiagnosticFrames.IsValid(frame))
            {
                throw new ArgumentOutOfRangeException(nameof(frame), frame, "Frame must be non-negative.");
            }

            return new BattleDiagnosticFrameCursor(
                frame,
                false,
                BattleDiagnosticFrameCursorChangeReason.UserSelectedFrame);
        }

        public BattleDiagnosticFrameCursor NavigateToSelection(BattleDiagnosticSelection selection)
        {
            if (!selection.IsValid || !BattleDiagnosticFrames.IsValid(selection.Frame))
            {
                return this;
            }

            return new BattleDiagnosticFrameCursor(
                selection.Frame,
                false,
                BattleDiagnosticFrameCursorChangeReason.SelectionNavigation);
        }

        public BattleDiagnosticFrameCursor ConstrainTo(BattleDiagnosticFrameRange retainedRange)
        {
            if (!retainedRange.IsValid)
            {
                return new BattleDiagnosticFrameCursor(
                    BattleDiagnosticFrames.Invalid,
                    false,
                    BattleDiagnosticFrameCursorChangeReason.RetainedRangeClamped);
            }

            var clampedFrame = retainedRange.Clamp(Frame);
            if (clampedFrame == Frame)
            {
                return this;
            }

            return new BattleDiagnosticFrameCursor(
                clampedFrame,
                FollowsLive && clampedFrame == retainedRange.LastFrame,
                BattleDiagnosticFrameCursorChangeReason.RetainedRangeClamped);
        }

        public bool Equals(BattleDiagnosticFrameCursor other)
        {
            return Frame == other.Frame &&
                   FollowsLive == other.FollowsLive &&
                   ChangeReason == other.ChangeReason;
        }

        public override bool Equals(object obj)
        {
            return obj is BattleDiagnosticFrameCursor other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Frame;
                hashCode = (hashCode * 397) ^ FollowsLive.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)ChangeReason;
                return hashCode;
            }
        }

        public static bool operator ==(BattleDiagnosticFrameCursor left, BattleDiagnosticFrameCursor right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BattleDiagnosticFrameCursor left, BattleDiagnosticFrameCursor right)
        {
            return !left.Equals(right);
        }
    }
}
