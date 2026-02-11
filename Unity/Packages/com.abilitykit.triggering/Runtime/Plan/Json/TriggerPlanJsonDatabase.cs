using System;
using System.Collections.Generic;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Registry;
using Newtonsoft.Json;

namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    public sealed class TriggerPlanJsonDatabase
    {
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
        private sealed class TriggerPlanDto
        {
            public int TriggerId;
            public string EventName;
            public int EventId;
            public bool AllowExternal;
            public int Phase;
            public int Priority;
            public PredicatePlanDto Predicate;
            public List<ActionCallPlanDto> Actions;
        }

        [Serializable]
        private sealed class PredicatePlanDto
        {
            public string Kind;
            public List<BoolExprNodeDto> Nodes;
        }

        [Serializable]
        private sealed class BoolExprNodeDto
        {
            public string Kind;
            public bool ConstValue;
            public string CompareOp;
            public NumericValueRefDto Left;
            public NumericValueRefDto Right;
        }

        [Serializable]
        private sealed class ActionCallPlanDto
        {
            public int ActionId;
            public int Arity;
            public NumericValueRefDto Arg0;
            public NumericValueRefDto Arg1;
        }

        [Serializable]
        private sealed class NumericValueRefDto
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

        private static TriggerPlan<object> BuildPlan(TriggerPlanDto dto)
        {
            var actions = BuildActions(dto.Actions);

            var pred = dto.Predicate;
            if (pred == null || string.Equals(pred.Kind, "none", StringComparison.OrdinalIgnoreCase))
            {
                return new TriggerPlan<object>(dto.Phase, dto.Priority, actions);
            }

            if (string.Equals(pred.Kind, "expr", StringComparison.OrdinalIgnoreCase))
            {
                var expr = new PredicateExprPlan(BuildExprNodes(pred.Nodes));
                return new TriggerPlan<object>(dto.Phase, dto.Priority, expr, actions);
            }

            throw new NotSupportedException($"Predicate kind not supported by loader: {pred.Kind}");
        }

        private static ActionCallPlan[] BuildActions(List<ActionCallPlanDto> dtos)
        {
            if (dtos == null || dtos.Count == 0) return Array.Empty<ActionCallPlan>();

            var arr = new ActionCallPlan[dtos.Count];
            for (int i = 0; i < dtos.Count; i++)
            {
                var d = dtos[i];
                if (d == null)
                {
                    arr[i] = default;
                    continue;
                }

                var id = new ActionId(d.ActionId);
                switch (d.Arity)
                {
                    case 0:
                        arr[i] = new ActionCallPlan(id);
                        break;
                    case 1:
                        arr[i] = new ActionCallPlan(id, BuildNumericValueRef(d.Arg0));
                        break;
                    case 2:
                        arr[i] = new ActionCallPlan(id, BuildNumericValueRef(d.Arg0), BuildNumericValueRef(d.Arg1));
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported action arity: {d.Arity} actionId={d.ActionId}");
                }
            }

            return arr;
        }

        private static BoolExprNode[] BuildExprNodes(List<BoolExprNodeDto> dtos)
        {
            if (dtos == null || dtos.Count == 0) return Array.Empty<BoolExprNode>();

            var arr = new BoolExprNode[dtos.Count];
            for (int i = 0; i < dtos.Count; i++)
            {
                var d = dtos[i];
                if (d == null)
                {
                    arr[i] = BoolExprNode.Const(true);
                    continue;
                }

                if (!Enum.TryParse<EBoolExprNodeKind>(d.Kind, out var kind))
                {
                    throw new InvalidOperationException($"Unknown expr node kind: {d.Kind}");
                }

                switch (kind)
                {
                    case EBoolExprNodeKind.Const:
                        arr[i] = BoolExprNode.Const(d.ConstValue);
                        break;
                    case EBoolExprNodeKind.Not:
                        arr[i] = BoolExprNode.Not();
                        break;
                    case EBoolExprNodeKind.And:
                        arr[i] = BoolExprNode.And();
                        break;
                    case EBoolExprNodeKind.Or:
                        arr[i] = BoolExprNode.Or();
                        break;
                    case EBoolExprNodeKind.CompareNumeric:
                    {
                        if (!Enum.TryParse<ECompareOp>(d.CompareOp, out var op))
                        {
                            throw new InvalidOperationException($"Unknown compare op: {d.CompareOp}");
                        }

                        arr[i] = BoolExprNode.Compare(op, BuildNumericValueRef(d.Left), BuildNumericValueRef(d.Right));
                        break;
                    }
                    default:
                        throw new InvalidOperationException($"Unsupported expr node kind: {kind}");
                }
            }

            return arr;
        }

        private static NumericValueRef BuildNumericValueRef(NumericValueRefDto dto)
        {
            if (dto == null) return default;

            if (!Enum.TryParse<ENumericValueRefKind>(dto.Kind, out var kind))
            {
                throw new InvalidOperationException($"Unknown NumericValueRef kind: {dto.Kind}");
            }

            switch (kind)
            {
                case ENumericValueRefKind.Const:
                    return NumericValueRef.Const(dto.ConstValue);
                case ENumericValueRefKind.Blackboard:
                    return NumericValueRef.Blackboard(dto.BoardId, dto.KeyId);
                case ENumericValueRefKind.PayloadField:
                    return NumericValueRef.PayloadField(dto.FieldId);
                case ENumericValueRefKind.Var:
                    return NumericValueRef.Var(dto.DomainId, dto.Key);
                case ENumericValueRefKind.Expr:
                    throw new NotSupportedException("UGC numeric value source kind 'Expr' is not allowed in JSON");
                default:
                    throw new InvalidOperationException($"Unsupported NumericValueRef kind: {kind}");
            }
        }
    }
}
