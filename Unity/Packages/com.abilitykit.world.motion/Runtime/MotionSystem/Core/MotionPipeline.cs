using System;
using System.Collections.Generic;
using AbilityKit.Core.Common.MotionSystem.Collision;
using AbilityKit.Core.Common.MotionSystem.Events;
using AbilityKit.Core.Math;

namespace AbilityKit.Core.Common.MotionSystem.Core
{
    public sealed class MotionPipeline
    {
        private readonly List<IMotionSource> _sources = new List<IMotionSource>(4);

        private readonly Dictionary<int, int> _bestIndexByGroup = new Dictionary<int, int>(8);
        private readonly List<int> _suppressedGroups = new List<int>(8);

        public IMotionSolver Solver { get; set; } = NoMotionSolver.Instance;
        public IMotionEventSink Events { get; set; }

        public MotionPipelinePolicy Policy { get; set; }

        public void AddSource(IMotionSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _sources.Add(source);
            _sources.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public bool RemoveSource(IMotionSource source)
        {
            if (source == null) return false;
            return _sources.Remove(source);
        }

        public void ClearSources()
        {
            _sources.Clear();
        }

        public MotionSolveResult Tick(int id, ref MotionState state, float dt, ref MotionOutput output)
        {
            output.Clear();

            var desired = Vec3.Zero;

            _bestIndexByGroup.Clear();
            _suppressedGroups.Clear();

            // Pass 1: cleanup and find best source index per group (by priority) for exclusive/override groups.
            for (int i = _sources.Count - 1; i >= 0; i--)
            {
                var s = _sources[i];
                if (s == null)
                {
                    _sources.RemoveAt(i);
                    continue;
                }

                if (!s.IsActive)
                {
                    _sources.RemoveAt(i);
                    continue;
                }

                if (s.Stacking == MotionStacking.Additive) continue;

                var gid = s.GroupId;
                if (_bestIndexByGroup.TryGetValue(gid, out var bestIdx))
                {
                    if (_sources[bestIdx].Priority < s.Priority)
                    {
                        _bestIndexByGroup[gid] = i;
                    }
                }
                else
                {
                    _bestIndexByGroup.Add(gid, i);
                }
            }

            // Pass 1.5: apply cross-group suppression based on policy.
            if (Policy != null)
            {
                foreach (var kv in _bestIndexByGroup)
                {
                    var bestIdx = kv.Value;
                    if (bestIdx < 0 || bestIdx >= _sources.Count) continue;

                    var s = _sources[bestIdx];
                    if (s == null) continue;
                    if (!s.IsActive) continue;

                    if (s.Stacking != MotionStacking.OverrideLowerPriority) continue;

                    if (Policy.TryGetSuppressedGroups(s.GroupId, out var suppressed) && suppressed != null)
                    {
                        for (int j = 0; j < suppressed.Length; j++)
                        {
                            AddSuppressed(suppressed[j]);
                        }
                    }
                }
            }

            // Pass 2: tick only effective sources.
            for (int i = _sources.Count - 1; i >= 0; i--)
            {
                var s = _sources[i];
                if (s == null) continue;
                if (!s.IsActive) continue;

                if (IsSuppressed(s.GroupId)) continue;

                if (s.Stacking != MotionStacking.Additive)
                {
                    if (_bestIndexByGroup.TryGetValue(s.GroupId, out var bestIdx) && bestIdx != i)
                    {
                        continue;
                    }
                }

                s.Tick(id, ref state, dt, ref desired);

                // If a source finishes in this tick, notify.
                if (!s.IsActive)
                {
                    if (Events != null)
                    {
                        var evt = MotionFinishEvent.Arrive;
                        if (s is IMotionFinishEventSource finish)
                        {
                            evt = finish.FinishEvent;
                        }

                        switch (evt)
                        {
                            case MotionFinishEvent.Expired:
                                Events.OnExpired(id, in state);
                                break;
                            case MotionFinishEvent.Arrive:
                                Events.OnArrive(id, in state);
                                break;
                        }
                    }
                }
            }

            output.DesiredDelta = desired;

            var solver = Solver ?? NoMotionSolver.Instance;
            var result = solver.Solve(id, state, output, dt);

            output.AppliedDelta = result.AppliedDelta;

            state.Position = new Vec3(state.Position.X + result.AppliedDelta.X, state.Position.Y + result.AppliedDelta.Y, state.Position.Z + result.AppliedDelta.Z);
            state.Time += dt;

            if (result.Hit.Hit)
            {
                Events?.OnHit(id, in state, in result.Hit);
            }

            return result;
        }

        private void AddSuppressed(int groupId)
        {
            for (int i = 0; i < _suppressedGroups.Count; i++)
            {
                if (_suppressedGroups[i] == groupId) return;
            }

            _suppressedGroups.Add(groupId);
        }

        private bool IsSuppressed(int groupId)
        {
            for (int i = 0; i < _suppressedGroups.Count; i++)
            {
                if (_suppressedGroups[i] == groupId) return true;
            }

            return false;
        }
    }
}
