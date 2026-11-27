#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Blackboard;

namespace AbilityKit.Triggering.Runtime.Plan
{
    /// <summary>
    /// 保存有序的运行时计划记录及其查询索引，不负责识别或解析输入格式。
    /// </summary>
    internal sealed class TriggerPlanRecordStore<TRecord> where TRecord : struct
    {
        private readonly Func<TRecord, (int Id, ITriggerPlanExecutable? Root)> _describe;
        private readonly List<TRecord> _records = new List<TRecord>();
        private readonly Dictionary<int, int> _recordIndexes = new Dictionary<int, int>();
        private readonly Dictionary<int, ITriggerPlanExecutable> _rootsById = new Dictionary<int, ITriggerPlanExecutable>();
        private readonly Dictionary<int, string?> _strings = new Dictionary<int, string?>();
        private readonly List<BlackboardInitializationPlan> _blackboards;

        internal TriggerPlanRecordStore(
            Func<TRecord, (int Id, ITriggerPlanExecutable? Root)> describe,
            IDictionary<int, string>? strings = null,
            IEnumerable<BlackboardInitializationPlan>? blackboards = null)
        {
            _describe = describe;
            if (strings != null)
            {
                foreach (var entry in strings)
                    _strings.Add(entry.Key, entry.Value);
            }
            _blackboards = blackboards == null
                ? new List<BlackboardInitializationPlan>()
                : new List<BlackboardInitializationPlan>(blackboards);
        }

        internal IReadOnlyList<TRecord> Records => _records;
        internal IReadOnlyList<BlackboardInitializationPlan> Blackboards => _blackboards;

        internal bool TryGetRecord(int id, out TRecord record)
        {
            if (_recordIndexes.TryGetValue(id, out var index))
            {
                record = _records[index];
                return true;
            }

            record = default;
            return false;
        }

        internal bool TryGetRoot(int id, out ITriggerPlanExecutable? root) => _rootsById.TryGetValue(id, out root);

        internal bool TryGetString(int id, out string? value)
        {
            value = null;
            return id != 0 && _strings.TryGetValue(id, out value);
        }

        internal void AddString(int id, string? value, bool replaceExisting)
        {
            if (id != 0 && (replaceExisting || !_strings.ContainsKey(id)))
                _strings[id] = value;
        }

        /// <summary>
        /// 按原顺序保留全部记录，按 ID 查询最后一条记录以及最后一个非空执行根。
        /// 此策略用于保持现有加载行为，与按 ID 覆盖整条记录的合并行为不同。
        /// </summary>
        internal void Append(in TRecord record)
        {
            var (id, root) = _describe(record);
            if (id <= 0) return;

            _recordIndexes[id] = _records.Count;
            _records.Add(record);
            if (root != null)
                _rootsById[id] = root;
        }

        internal void AddRecord(in TRecord record, bool replaceExisting)
        {
            var (id, root) = _describe(record);
            if (id <= 0 || (!replaceExisting && _recordIndexes.ContainsKey(id))) return;

            NormalizeRecords();
            WriteEffectiveRecord(in record, id, root, replaceExisting);
        }

        private void WriteEffectiveRecord(in TRecord record, int id, ITriggerPlanExecutable? root, bool replaceExisting)
        {
            if (_recordIndexes.TryGetValue(id, out var index))
            {
                if (!replaceExisting) return;
                _records[index] = record;
            }
            else
            {
                _recordIndexes.Add(id, _records.Count);
                _records.Add(record);
            }

            if (root != null)
                _rootsById[id] = root;
            else
                _rootsById.Remove(id);
        }

        /// <summary>
        /// 按 ID 首次出现的顺序枚举，每个 ID 只取最终生效的完整记录。
        /// </summary>
        private IEnumerable<TRecord> EnumerateEffectiveRecords()
        {
            if (_records.Count == _recordIndexes.Count)
            {
                foreach (var record in _records)
                    yield return record;
                yield break;
            }

            var seen = new HashSet<int>();
            foreach (var record in _records)
            {
                var id = _describe(record).Id;
                if (seen.Add(id))
                    yield return _records[_recordIndexes[id]];
            }
        }

        private void NormalizeRecords()
        {
            if (_records.Count == _recordIndexes.Count) return;

            var effectiveRecords = new List<TRecord>(EnumerateEffectiveRecords());
            _records.Clear();
            _recordIndexes.Clear();
            _rootsById.Clear();
            foreach (var record in effectiveRecords)
                Append(in record);
        }

        internal void MergeFrom(TriggerPlanRecordStore<TRecord> source, bool replaceExisting)
        {
            NormalizeRecords();
            foreach (var entry in source._strings)
                AddString(entry.Key, entry.Value, replaceExisting);
            foreach (var record in source.EnumerateEffectiveRecords())
            {
                var (id, root) = _describe(record);
                WriteEffectiveRecord(in record, id, root, replaceExisting);
            }
            for (var index = 0; index < source._blackboards.Count; index++)
                AddBlackboardInitialization(source._blackboards[index], replaceExisting);
        }

        private void AddBlackboardInitialization(BlackboardInitializationPlan plan, bool replaceExisting)
        {
            if (plan == null || plan.BoardId == 0) return;

            for (var index = 0; index < _blackboards.Count; index++)
            {
                if (_blackboards[index] == null || _blackboards[index].BoardId != plan.BoardId) continue;
                if (replaceExisting) _blackboards[index] = plan;
                return;
            }

            _blackboards.Add(plan);
        }
    }
}
