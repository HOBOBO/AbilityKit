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
        private readonly Dictionary<int, TRecord> _recordsById = new Dictionary<int, TRecord>();
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

        internal bool TryGetRecord(int id, out TRecord record) => _recordsById.TryGetValue(id, out record);

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

            _records.Add(record);
            _recordsById[id] = record;
            if (root != null)
                _rootsById[id] = root;
        }

        internal void AddRecord(in TRecord record, bool replaceExisting)
        {
            var (id, root) = _describe(record);
            if (id <= 0) return;

            var index = 0;
            for (; index < _records.Count; index++)
            {
                if (_describe(_records[index]).Id == id) break;
            }

            if (index == _records.Count)
                _records.Add(record);
            else if (replaceExisting)
                _records[index] = record;
            else
                return;

            _recordsById[id] = record;
            if (root != null)
                _rootsById[id] = root;
            else
                _rootsById.Remove(id);
        }

        internal void MergeFrom(TriggerPlanRecordStore<TRecord>? source, bool replaceExisting)
        {
            if (source == null) return;

            foreach (var entry in source._strings)
                AddString(entry.Key, entry.Value, replaceExisting);
            for (var index = 0; index < source._records.Count; index++)
                AddRecord(source._records[index], replaceExisting);
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
