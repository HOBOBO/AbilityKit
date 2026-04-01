using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Triggering.Runtime.Executable
{
    /// <summary>
    /// Cue 工厂接口
    /// </summary>
    public interface ICueFactory
    {
        ITriggerCue Create(string cueKind, string cueVfxId, string cueSfxId);
    }

    /// <summary>
    /// 文本加载器接口
    /// </summary>
    public interface ITextLoader
    {
        bool TryLoad(string id, out string text);
    }

    /// <summary>
    /// 触发器记录
    /// </summary>
    public readonly struct TriggerRecord
    {
        public readonly int TriggerId;
        public readonly string EventName;
        public readonly int EventId;
        public readonly ISimpleExecutable Executable;
        public readonly ICondition Condition;
        public readonly int Phase;
        public readonly int Priority;
        public readonly int InterruptPriority;
        public readonly ITriggerCue Cue;

        public TriggerRecord(
            int triggerId,
            string eventName,
            int eventId,
            ISimpleExecutable executable,
            ICondition condition,
            int phase,
            int priority,
            int interruptPriority,
            ITriggerCue cue)
        {
            TriggerId = triggerId;
            EventName = eventName;
            EventId = eventId;
            Executable = executable;
            Condition = condition;
            Phase = phase;
            Priority = priority;
            InterruptPriority = interruptPriority;
            Cue = cue;
        }
    }

    /// <summary>
    /// 基于 Executable 的触发器数据库
    /// </summary>
    public sealed class ExecutableTriggerDatabase
    {
        private readonly List<TriggerRecord> _records = new();
        private readonly Dictionary<int, TriggerRecord> _byTriggerId = new();
        private readonly Dictionary<int, string> _strings = new();
        private ICueFactory _cueFactory;
        private ConfigToExecutableConverter _converter;

        public FunctionRegistry Functions { get; private set; }
        public ActionRegistry Actions { get; private set; }
        public IIdNameRegistry IdNames { get; private set; }

        public IReadOnlyList<TriggerRecord> Records => _records;

        public ICueFactory CueFactory
        {
            get => _cueFactory ?? NullCueFactory.Instance;
            set => _cueFactory = value;
        }

        /// <summary>
        /// 初始化数据库
        /// </summary>
        public void Initialize(FunctionRegistry functions, ActionRegistry actions, IIdNameRegistry idNames = null)
        {
            Functions = functions ?? throw new ArgumentNullException(nameof(functions));
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
            IdNames = idNames;
            _converter = new ConfigToExecutableConverter(functions, actions, idNames);
        }

        /// <summary>
        /// 获取字符串
        /// </summary>
        public bool TryGetString(int id, out string value)
            => _strings.TryGetValue(id, out value);

        /// <summary>
        /// 获取触发器记录
        /// </summary>
        public bool TryGetRecordByTriggerId(int triggerId, out TriggerRecord record)
            => _byTriggerId.TryGetValue(triggerId, out record);

        /// <summary>
        /// 添加触发器记录
        /// </summary>
        public void AddRecord(TriggerRecord record)
        {
            _records.Add(record);
            _byTriggerId[record.TriggerId] = record;
        }

        /// <summary>
        /// 加载 JSON
        /// </summary>
        public void Load(ITextLoader loader, string id)
        {
            if (loader == null) throw new ArgumentNullException(nameof(loader));
            if (string.IsNullOrEmpty(id)) throw new ArgumentException(nameof(id));

            if (!loader.TryLoad(id, out var json) || string.IsNullOrEmpty(json))
                throw new InvalidOperationException($"Executable trigger json not found or empty: {id}");

            LoadFromJson(json, id);
        }

        /// <summary>
        /// 从 JSON 加载
        /// </summary>
        public void LoadFromJson(string json, string sourceName = null)
        {
            if (string.IsNullOrEmpty(json))
                throw new InvalidOperationException($"Executable trigger json is empty: {sourceName ?? "<json>"}");

            TriggerDatabaseDto dto;
            try
            {
                dto = Newtonsoft.Json.JsonConvert.DeserializeObject<TriggerDatabaseDto>(json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse executable trigger json: {sourceName ?? "<json>"}. {ex.Message}", ex);
            }

            LoadDto(dto, sourceName);
        }

        /// <summary>
        /// 加载 DTO
        /// </summary>
        public void LoadDto(TriggerDatabaseDto dto, string sourceName = null)
        {
            _records.Clear();
            _byTriggerId.Clear();

            if (dto?.Strings != null)
            {
                _strings.Clear();
                foreach (var kvp in dto.Strings)
                    _strings[kvp.Key] = kvp.Value;
            }

            if (dto?.Triggers != null)
            {
                foreach (var t in dto.Triggers)
                {
                    if (t == null || t.TriggerId <= 0) continue;

                    var record = BuildRecord(t);
                    _records.Add(record);
                    _byTriggerId[t.TriggerId] = record;
                }
            }
        }

        private TriggerRecord BuildRecord(TriggerPlanDto dto)
        {
            ICondition condition = null;
            if (dto.Predicate != null && _converter != null)
                condition = _converter.ConvertCondition(dto.Predicate);

            ISimpleExecutable executable = null;
            if (dto.Executables != null && dto.Executables.Count > 0 && _converter != null)
                executable = _converter.ConvertToSequence(dto.Executables);

            var cue = CueFactory.Create(dto.CueKind, dto.CueVfxId, dto.CueSfxId);

            var eventId = dto.EventId;
            if (eventId == 0 && !string.IsNullOrEmpty(dto.EventName))
                eventId = StableStringId.Get("event:" + dto.EventName);

            return new TriggerRecord(
                dto.TriggerId,
                dto.EventName,
                eventId,
                executable,
                condition,
                dto.Phase,
                dto.Priority,
                dto.InterruptPriority,
                cue ?? NullTriggerCue.Instance);
        }
    }

    /// <summary>
    /// 默认 Cue 工厂
    /// </summary>
    public sealed class NullCueFactory : ICueFactory
    {
        public static readonly NullCueFactory Instance = new();

        private NullCueFactory() { }

        public ITriggerCue Create(string cueKind, string cueVfxId, string cueSfxId)
            => NullTriggerCue.Instance;
    }
}
