using System;
using System.Collections.Generic;
using AbilityKit.Core.Eventing;
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
        }

        [Serializable]
        private sealed class TriggerPlanDto
        {
            public int TriggerId;
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
            public IntValueRefDto Left;
            public IntValueRefDto Right;
        }

        [Serializable]
        private sealed class ActionCallPlanDto
        {
            public int ActionId;
            public int Arity;
            public IntValueRefDto Arg0;
            public IntValueRefDto Arg1;
        }

        [Serializable]
        private sealed class IntValueRefDto
        {
            public string Kind;
            public int ConstValue;
            public int BoardId;
            public int KeyId;
            public int FieldId;
        }

        public readonly struct Record
        {
            public readonly int TriggerId;
            public readonly int EventId;
            public readonly TriggerPlan<object> Plan;

            public Record(int triggerId, int eventId, in TriggerPlan<object> plan)
            {
                TriggerId = triggerId;
                EventId = eventId;
                Plan = plan;
            }
        }

        private List<Record> _records = new List<Record>();

        public IReadOnlyList<Record> Records => _records;

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
            if (dto?.Triggers != null)
            {
                for (int i = 0; i < dto.Triggers.Count; i++)
                {
                    var t = dto.Triggers[i];
                    if (t == null) continue;
                    if (t.TriggerId <= 0) continue;
                    if (t.EventId == 0) continue;

                    var plan = BuildPlan(t);
                    next.Add(new Record(t.TriggerId, t.EventId, in plan));
                }
            }

            _records = next;
        }

        public void RegisterAll<TCtx>(TriggerRunner<TCtx> runner)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));

            for (int i = 0; i < _records.Count; i++)
            {
                var r = _records[i];
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
                        arr[i] = new ActionCallPlan(id, BuildIntValueRef(d.Arg0));
                        break;
                    case 2:
                        arr[i] = new ActionCallPlan(id, BuildIntValueRef(d.Arg0), BuildIntValueRef(d.Arg1));
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
                    case EBoolExprNodeKind.CompareInt:
                    {
                        if (!Enum.TryParse<ECompareOp>(d.CompareOp, out var op))
                        {
                            throw new InvalidOperationException($"Unknown compare op: {d.CompareOp}");
                        }

                        arr[i] = BoolExprNode.Compare(op, BuildIntValueRef(d.Left), BuildIntValueRef(d.Right));
                        break;
                    }
                    default:
                        throw new InvalidOperationException($"Unsupported expr node kind: {kind}");
                }
            }

            return arr;
        }

        private static IntValueRef BuildIntValueRef(IntValueRefDto dto)
        {
            if (dto == null) return default;

            if (!Enum.TryParse<EIntValueRefKind>(dto.Kind, out var kind))
            {
                throw new InvalidOperationException($"Unknown IntValueRef kind: {dto.Kind}");
            }

            switch (kind)
            {
                case EIntValueRefKind.Const:
                    return IntValueRef.Const(dto.ConstValue);
                case EIntValueRefKind.Blackboard:
                    return IntValueRef.Blackboard(dto.BoardId, dto.KeyId);
                case EIntValueRefKind.PayloadField:
                    return IntValueRef.PayloadField(dto.FieldId);
                default:
                    throw new InvalidOperationException($"Unsupported IntValueRef kind: {kind}");
            }
        }
    }
}
