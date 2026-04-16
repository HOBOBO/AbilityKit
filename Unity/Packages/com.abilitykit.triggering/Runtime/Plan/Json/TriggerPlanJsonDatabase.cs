using System;
using System.Collections.Generic;
using AbilityKit.Core.Common.Event;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Registry;
using Newtonsoft.Json;

namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    /// <summary>
    /// 触发器计划数据库（从 JSON 加载 TriggerPlan）
    /// 已迁移到 TriggerPlanConverter 进行转换
    /// </summary>
    public sealed class TriggerPlanJsonDatabase
    {
        /// <summary>
        /// Cue 工厂接口
        /// 负责将 JSON 中的 CueKind / CueVfxId / CueSfxId 解析为具体的 ITriggerCue 实例
        /// </summary>
        public interface ICueFactory
        {
            ITriggerCue Create(string cueKind, string cueVfxId, string cueSfxId);
        }

        /// <summary>
        /// 默认 Cue 工厂：始终返回 NullTriggerCue
        /// 业务项目可注册自定义实现
        /// </summary>
        public sealed class DefaultCueFactory : ICueFactory
        {
            public static readonly DefaultCueFactory Instance = new DefaultCueFactory();

            private DefaultCueFactory() { }

            public ITriggerCue Create(string cueKind, string cueVfxId, string cueSfxId)
            {
                return NullTriggerCue.Instance;
            }
        }

        public interface ITextLoader
        {
            bool TryLoad(string id, out string text);
        }

        [Serializable]
        private sealed class TriggerPlanDatabaseDto
        {
            public List<TriggerPlanDto> Triggers;

            public Dictionary<int, string> Strings;
        }

        [Serializable]
        internal sealed class TriggerPlanDto
        {
            public int TriggerId;
            public string EventName;
            public int EventId;
            public bool AllowExternal;
            public int Phase;
            public int Priority;
            public PredicatePlanDto Predicate;
            public List<ActionCallPlanDto> Actions;

            /// <summary>
            /// 表现 Cue 类型名，由 ICueFactory 解析为 ITriggerCue 实例
            /// </summary>
            public string CueKind;

            /// <summary>
            /// Cue VFX 标识（供工厂实现使用）
            /// </summary>
            public string CueVfxId;

            /// <summary>
            /// Cue SFX 标识（供工厂实现使用）
            /// </summary>
            public string CueSfxId;
        }

        [Serializable]
        internal sealed class PredicatePlanDto
        {
            public string Kind;
            public List<BoolExprNodeDto> Nodes;
        }

        [Serializable]
        internal sealed class BoolExprNodeDto
        {
            public string Kind;
            public bool ConstValue;
            public string CompareOp;
            public NumericValueRefDto Left;
            public NumericValueRefDto Right;
        }

        [Serializable]
        internal sealed class ActionCallPlanDto
        {
            public int ActionId;
            public int Arity;
            public NumericValueRefDto Arg0;
            public NumericValueRefDto Arg1;

            /// <summary>
            /// 具名参数字典（key=参数名）
            /// 优先级高于 Arg0/Arg1
            /// </summary>
            public Dictionary<string, NumericValueRefDto> Args;
        }

        [Serializable]
        internal sealed class NumericValueRefDto
        {
            public string Kind;
            public double ConstValue;
            public int BoardId;
            public int KeyId;
            public int FieldId;
            public string DomainId;
            public string Key;
            public string ExprText;
        }

        public readonly struct Record
        {
            public readonly int TriggerId;
            public readonly string EventName;
            public readonly int EventId;
            public readonly TriggerPlan<object> Plan;

            public Record(int triggerId, string eventName, int eventId, in TriggerPlan<object> plan)
            {
                TriggerId = triggerId;
                EventName = eventName;
                EventId = eventId;
                Plan = plan;
            }
        }

        private List<Record> _records = new List<Record>();
        private Dictionary<int, TriggerPlan<object>> _byTriggerId = new Dictionary<int, TriggerPlan<object>>();
        private Dictionary<int, string> _strings = new Dictionary<int, string>();
        private ICueFactory _cueFactory = DefaultCueFactory.Instance;

        public IReadOnlyList<Record> Records => _records;

        public bool TryGetString(int id, out string value)
        {
            value = null;
            if (id == 0) return false;
            return _strings != null && _strings.TryGetValue(id, out value);
        }

        public bool TryGetPlanByTriggerId(int triggerId, out TriggerPlan<object> plan)
        {
            plan = default;
            if (triggerId <= 0) return false;
            return _byTriggerId != null && _byTriggerId.TryGetValue(triggerId, out plan);
        }

        public void Load(ITextLoader loader, string id)
        {
            if (loader == null) throw new ArgumentNullException(nameof(loader));
            if (string.IsNullOrEmpty(id)) throw new ArgumentException(nameof(id));

            if (!loader.TryLoad(id, out var json) || string.IsNullOrEmpty(json))
            {
                throw new InvalidOperationException($"Trigger plan json not found or empty: {id}");
            }

            LoadFromJson(json, id);
        }

        public void LoadFromJson(string json, string sourceName = null)
        {
            if (string.IsNullOrEmpty(json))
            {
                throw new InvalidOperationException($"Trigger plan json is empty: {sourceName ?? "<json>"}");
            }

            TriggerPlanDatabaseDto dto;
            try
            {
                dto = JsonConvert.DeserializeObject<TriggerPlanDatabaseDto>(json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse trigger plan json: {sourceName ?? "<json>"}. {ex.Message}", ex);
            }

            var next = new List<Record>();
            var byTriggerId = new Dictionary<int, TriggerPlan<object>>();
            var strings = dto?.Strings != null ? new Dictionary<int, string>(dto.Strings) : new Dictionary<int, string>();
            if (dto?.Triggers != null)
            {
                for (int i = 0; i < dto.Triggers.Count; i++)
                {
                    var t = dto.Triggers[i];
                    if (t == null) continue;
                    if (t.TriggerId <= 0) continue;

                    var eid = t.EventId;
                    if (eid == 0 && !string.IsNullOrEmpty(t.EventName))
                    {
                        eid = StableStringId.Get("event:" + t.EventName);
                    }

                    var plan = BuildPlan(t);
                    next.Add(new Record(t.TriggerId, t.EventName, eid, in plan));
                    byTriggerId[t.TriggerId] = plan;
                }
            }

            _records = next;
            _byTriggerId = byTriggerId;
            _strings = strings;
        }

        public void RegisterAll<TCtx>(TriggerRunner<TCtx> runner)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));

            for (int i = 0; i < _records.Count; i++)
            {
                var r = _records[i];
                if (r.EventId == 0) continue;
                var key = new EventKey<object>(r.EventId);
                runner.RegisterPlan<object, TCtx>(key, r.Plan);
            }
        }

        private static readonly TriggerPlanConverter _converter = new TriggerPlanConverter();

        private static TriggerPlan<object> BuildPlan(TriggerPlanDto dto)
        {
            return _converter.Convert(dto);
        }
    }
}
