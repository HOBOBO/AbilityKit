#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.BehaviorTree;

using UnityEngine.Scripting.APIUpdating;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;
namespace AbilityKit.BehaviorTree.Editor.Debugging.Observation
{
    /// <summary>
    /// 观察控制器：分离注册中心轮询、实例选择、连接状态、冻结/暂停、采样频率与时间线生命周期。
    /// 由窗口在 EditorApplication.update 里调用 <see cref="Poll"/> 驱动；纯 C#，脱离 IMGUI 可测。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationController")]
    public sealed class ObservationController
    {
        private readonly ObservationTimeline _timeline;
        private readonly List<DebugRegistryEntry> _entries = new();
        private long _selectedId;
        private long _nextSequence;
        private double _nextSampleAt;
        private double _sampleIntervalSeconds;

        public ObservationController(
            int historyLimit = ObservationSettings.DefaultTimelineCapacity,
            double sampleIntervalSeconds = ObservationSettings.DefaultSampleIntervalSeconds)
        {
            _timeline = new ObservationTimeline(historyLimit);
            SampleIntervalSeconds = sampleIntervalSeconds;
        }

        /// <summary>自动采样间隔（秒）。由窗口传入的时间基准驱动，控制器自身不依赖真实时钟。</summary>
        public double SampleIntervalSeconds
        {
            get => _sampleIntervalSeconds;
            set
            {
                var normalized = ObservationSettings.ClampSampleIntervalSeconds(value);
                if (Math.Abs(_sampleIntervalSeconds - normalized) < double.Epsilon) return;
                _sampleIntervalSeconds = normalized;
                _nextSampleAt = 0d;
            }
        }

        public int TimelineCapacity
        {
            get => _timeline.SampleLimit;
            set => _timeline.SetSampleLimit(value);
        }

        /// <summary>是否冻结。冻结时不自动采样，仅允许显式 <see cref="Sample"/> 单步。</summary>
        public bool Paused { get; private set; }

        public long SelectedInstanceId => _selectedId;
        public ObservationTimeline Timeline => _timeline;
        public ObservationSnapshot? Latest => _timeline.Latest;
        public IReadOnlyList<DebugRegistryEntry> Entries => _entries;

        public ObservationSessionState State
        {
            get
            {
                if (_selectedId == 0) return ObservationSessionState.NoSample;
                if (!TryGetSelectedView(out _)) return ObservationSessionState.Disconnected;
                if (_timeline.Count == 0) return ObservationSessionState.NoSample;
                return Paused ? ObservationSessionState.Frozen : ObservationSessionState.Live;
            }
        }

        /// <summary>
        /// 轮询注册中心并（在到期时）自动采样。nowSeconds 由宿主提供（如 EditorApplication.timeSinceStartup）。
        /// </summary>
        public void Poll(double nowSeconds) => Poll(nowSeconds, autoSelectFirst: true);

        public void Poll(double nowSeconds, bool autoSelectFirst)
        {
            RefreshEntries();
            if (autoSelectFirst && _selectedId == 0 && _entries.Count > 0) SelectFirst();
            if (Paused || _selectedId == 0) return;
            if (!TryGetSelectedView(out _)) return;
            // Editor time and decimal sampling intervals are represented as binary doubles.
            // A tiny tolerance prevents an exact logical deadline (for example 0.05 + 0.1)
            // from being missed because it is stored as 0.15000000000000002.
            if (nowSeconds + 1e-9d >= _nextSampleAt)
            {
                _nextSampleAt = nowSeconds + SampleIntervalSeconds;
                SampleCore();
            }
        }

        /// <summary>对选中实例做一次显式采样（冻结时即"单步"）。返回新采样，未选中或已断线返回 null。</summary>
        public ObservationSnapshot? Sample()
        {
            RefreshEntries();
            if (_selectedId == 0) return null;
            return SampleCore();
        }

        /// <summary>切换到指定实例并清空历史。返回 false 表示该 id 不在当前登记表中。</summary>
        public bool SelectInstance(long id)
        {
            RefreshEntries();
            if (id == 0)
            {
                ClearSelection();
                return true;
            }
            foreach (var entry in _entries)
            {
                if (entry.Id == id)
                {
                    _selectedId = id;
                    _timeline.Clear();
                    _nextSampleAt = 0d;
                    return true;
                }
            }
            return false;
        }

        /// <summary>清除选中实例与历史，回到 NoSample。</summary>
        public void ClearSelection()
        {
            _selectedId = 0;
            _timeline.Clear();
            _nextSampleAt = 0d;
        }

        public void Pause() => Paused = true;

        /// <summary>恢复自动采样，并让下一次 <see cref="Poll"/> 立即采样。</summary>
        public void Resume()
        {
            Paused = false;
            _nextSampleAt = 0d;
        }

        public void ClearHistory()
        {
            _timeline.Clear();
        }

        public void Reset()
        {
            _entries.Clear();
            _selectedId = 0;
            _nextSequence = 0;
            _nextSampleAt = 0d;
            Paused = false;
            _timeline.Clear();
        }

        private void RefreshEntries()
        {
            DebugRegistry.CopyEntries(_entries);
        }

        private void SelectFirst()
        {
            if (_entries.Count == 0) return;
            _selectedId = _entries[0].Id;
            _timeline.Clear();
            _nextSampleAt = 0d;
        }

        private ObservationSnapshot? SampleCore()
        {
            if (!TryGetSelectedView(out var view)) return null;
            var snapshot = ObservationSnapshot.Capture(_selectedId, _nextSequence++, view);
            _timeline.Append(snapshot);
            return snapshot;
        }

        private bool TryGetSelectedView(out TreeDebugView view)
        {
            view = null!;
            if (_selectedId == 0) return false;
            foreach (var entry in _entries)
            {
                if (entry.Id == _selectedId)
                {
                    view = entry.View;
                    return view != null;
                }
            }
            return false;
        }
    }
}
