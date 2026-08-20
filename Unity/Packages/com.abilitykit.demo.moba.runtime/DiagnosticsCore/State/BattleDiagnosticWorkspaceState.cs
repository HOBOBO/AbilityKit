using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    public sealed class BattleDiagnosticWorkspaceState
    {
        private readonly BattleDiagnosticNavigationHistory _navigation;
        private readonly List<BattleDiagnosticTimeRange> _timeRangeHistory;
        private readonly int _timeRangeHistoryCapacity;
        private int _timeRangeHistoryIndex;

        public BattleDiagnosticWorkspaceState(int navigationCapacity = BattleDiagnosticNavigationHistory.DefaultCapacity)
        {
            _navigation = new BattleDiagnosticNavigationHistory(navigationCapacity);
            _timeRangeHistoryCapacity = navigationCapacity;
            _timeRangeHistory = new List<BattleDiagnosticTimeRange>(navigationCapacity);
            FrameCursor = new BattleDiagnosticFrameCursor(
                BattleDiagnosticFrames.Invalid,
                true,
                BattleDiagnosticFrameCursorChangeReason.None);
            TimeRange = BattleDiagnosticTimeRange.Auto();
            ResetTimeRangeHistory(TimeRange);
            Filter = BattleDiagnosticFilter.Default;
        }

        public event Action Changed;

        public BattleDiagnosticSessionScope Scope { get; private set; }
        public BattleDiagnosticSelection Selection { get; private set; }
        public BattleDiagnosticFrameCursor FrameCursor { get; private set; }
        public BattleDiagnosticTimeRange TimeRange { get; private set; }
        public BattleDiagnosticFilter Filter { get; private set; }
        public BattleDiagnosticNavigationHistory Navigation => _navigation;
        public int TimeRangeHistoryCount => _timeRangeHistory.Count;
        public bool CanGoBackTimeRange => _timeRangeHistoryIndex > 0;
        public bool CanGoForwardTimeRange =>
            _timeRangeHistoryIndex >= 0 &&
            _timeRangeHistoryIndex < _timeRangeHistory.Count - 1;

        public void AttachSession(BattleDiagnosticSessionScope scope, int latestCompleteFrame)
        {
            if (!scope.IsValid)
            {
                throw new ArgumentException("A valid session scope is required.", nameof(scope));
            }

            var scopeChanged = scope != Scope;
            Scope = scope;
            FrameCursor = BattleDiagnosticFrameCursor.CreateFollowingLive(latestCompleteFrame);

            if (scopeChanged)
            {
                Selection = default;
                TimeRange = BattleDiagnosticTimeRange.Auto(
                    BattleDiagnosticTimeRangeChangeReason.SessionChanged);
                _navigation.Reset(scope);
                ResetTimeRangeHistory(TimeRange);
            }

            RaiseChanged();
        }

        public void DetachSession()
        {
            Scope = default;
            Selection = default;
            FrameCursor = new BattleDiagnosticFrameCursor(
                BattleDiagnosticFrames.Invalid,
                false,
                BattleDiagnosticFrameCursorChangeReason.SessionChanged);
            TimeRange = BattleDiagnosticTimeRange.Auto(
                BattleDiagnosticTimeRangeChangeReason.SessionChanged);
            _navigation.Reset(default);
            ResetTimeRangeHistory(TimeRange);
            RaiseChanged();
        }

        public bool Select(BattleDiagnosticSelection selection, bool navigateToSelectionFrame = true)
        {
            if (!selection.BelongsTo(Scope))
            {
                return false;
            }

            var selectionChanged = selection != Selection;
            if (!selectionChanged)
            {
                return false;
            }

            Selection = selection;
            _navigation.NavigateTo(selection);
            if (navigateToSelectionFrame)
            {
                FrameCursor = FrameCursor.NavigateToSelection(selection);
            }

            RaiseChanged();
            return true;
        }

        public bool GoBack()
        {
            if (!_navigation.TryGoBack(out var selection))
            {
                return false;
            }

            ApplyNavigationSelection(selection);
            return true;
        }

        public bool GoForward()
        {
            if (!_navigation.TryGoForward(out var selection))
            {
                return false;
            }

            ApplyNavigationSelection(selection);
            return true;
        }

        public void SetFrame(int frame)
        {
            var next = FrameCursor.SelectFrame(frame);
            if (next == FrameCursor)
            {
                return;
            }

            FrameCursor = next;
            RaiseChanged();
        }

        public void SetFollowLive(bool followLive, int latestCompleteFrame)
        {
            var next = FrameCursor.SetFollowLive(followLive, latestCompleteFrame);
            if (next == FrameCursor)
            {
                return;
            }

            FrameCursor = next;
            RaiseChanged();
        }

        public void AdvanceLive(int latestCompleteFrame)
        {
            var next = FrameCursor.AdvanceLive(latestCompleteFrame);
            if (next == FrameCursor)
            {
                return;
            }

            FrameCursor = next;
            RaiseChanged();
        }

        public void ConstrainToRetainedRange(BattleDiagnosticFrameRange retainedRange)
        {
            var nextCursor = FrameCursor.ConstrainTo(retainedRange);
            var nextTimeRange = TimeRange.ConstrainTo(retainedRange);
            if (nextCursor == FrameCursor && nextTimeRange == TimeRange)
            {
                return;
            }

            var timeRangeChanged = nextTimeRange != TimeRange;
            FrameCursor = nextCursor;
            TimeRange = nextTimeRange;
            if (timeRangeChanged)
            {
                ResetTimeRangeHistory(nextTimeRange);
            }
            RaiseChanged();
        }

        public bool SetTimeRange(
            int firstFrame,
            int lastFrame,
            BattleDiagnosticTimeRangeChangeReason changeReason =
                BattleDiagnosticTimeRangeChangeReason.UserSelected)
        {
            return ApplyTimeRange(BattleDiagnosticTimeRange.Fixed(
                firstFrame,
                lastFrame,
                changeReason));
        }

        public bool ZoomTimeRange(int anchorFrame, double scale)
        {
            return TimeRange.IsFixed && ApplyTimeRange(TimeRange.Zoom(anchorFrame, scale));
        }

        public bool PanTimeRange(int frameDelta)
        {
            return TimeRange.IsFixed && ApplyTimeRange(TimeRange.Pan(frameDelta));
        }

        public bool GoBackTimeRange()
        {
            if (!CanGoBackTimeRange)
            {
                return false;
            }

            _timeRangeHistoryIndex--;
            TimeRange = _timeRangeHistory[_timeRangeHistoryIndex].WithChangeReason(
                BattleDiagnosticTimeRangeChangeReason.HistoryNavigation);
            RaiseChanged();
            return true;
        }

        public bool GoForwardTimeRange()
        {
            if (!CanGoForwardTimeRange)
            {
                return false;
            }

            _timeRangeHistoryIndex++;
            TimeRange = _timeRangeHistory[_timeRangeHistoryIndex].WithChangeReason(
                BattleDiagnosticTimeRangeChangeReason.HistoryNavigation);
            RaiseChanged();
            return true;
        }

        public bool FocusTimeRange(int centerFrame, int radiusFrames)
        {
            if (!BattleDiagnosticFrames.IsValid(centerFrame))
            {
                return false;
            }

            if (radiusFrames < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(radiusFrames));
            }

            var firstFrame = Math.Max(0, centerFrame - radiusFrames);
            var lastFrame = centerFrame > int.MaxValue - radiusFrames
                ? int.MaxValue
                : centerFrame + radiusFrames;
            var next = BattleDiagnosticTimeRange.Fixed(
                firstFrame,
                lastFrame,
                BattleDiagnosticTimeRangeChangeReason.CursorFocused);
            return ApplyTimeRange(next);
        }

        public bool ClearTimeRange()
        {
            if (TimeRange.IsAuto)
            {
                return false;
            }

            return ApplyTimeRange(BattleDiagnosticTimeRange.Auto(
                BattleDiagnosticTimeRangeChangeReason.ResetToAuto));
        }

        public void SetFilter(BattleDiagnosticFilter filter)
        {
            if (filter.Equals(Filter))
            {
                return;
            }

            Filter = filter;
            RaiseChanged();
        }

        private void ApplyNavigationSelection(BattleDiagnosticSelection selection)
        {
            Selection = selection;
            FrameCursor = FrameCursor.NavigateToSelection(selection);
            RaiseChanged();
        }

        private bool ApplyTimeRange(BattleDiagnosticTimeRange next)
        {
            if (HasSameRange(TimeRange, next))
            {
                return false;
            }

            TimeRange = next;
            RemoveForwardTimeRanges();
            _timeRangeHistory.Add(next);
            _timeRangeHistoryIndex = _timeRangeHistory.Count - 1;
            TrimTimeRangeHistory();
            RaiseChanged();
            return true;
        }

        private void ResetTimeRangeHistory(BattleDiagnosticTimeRange current)
        {
            _timeRangeHistory.Clear();
            _timeRangeHistory.Add(current);
            _timeRangeHistoryIndex = 0;
        }

        private void RemoveForwardTimeRanges()
        {
            var firstForwardIndex = _timeRangeHistoryIndex + 1;
            if (firstForwardIndex < _timeRangeHistory.Count)
            {
                _timeRangeHistory.RemoveRange(
                    firstForwardIndex,
                    _timeRangeHistory.Count - firstForwardIndex);
            }
        }

        private void TrimTimeRangeHistory()
        {
            var overflow = _timeRangeHistory.Count - _timeRangeHistoryCapacity;
            if (overflow <= 0)
            {
                return;
            }

            _timeRangeHistory.RemoveRange(0, overflow);
            _timeRangeHistoryIndex -= overflow;
        }

        private static bool HasSameRange(
            BattleDiagnosticTimeRange left,
            BattleDiagnosticTimeRange right)
        {
            return left.Mode == right.Mode &&
                   left.FirstFrame == right.FirstFrame &&
                   left.LastFrame == right.LastFrame;
        }

        private void RaiseChanged()
        {
            Changed?.Invoke();
        }
    }
}
