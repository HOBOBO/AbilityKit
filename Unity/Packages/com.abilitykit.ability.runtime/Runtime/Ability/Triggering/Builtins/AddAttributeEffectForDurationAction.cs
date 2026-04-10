using System;
using AbilityKit.Core.Common.AttributeSystem;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Modifiers;
using AbilityKit.ECS;

namespace AbilityKit.Ability.Triggering.Runtime.Builtins
{
    public sealed class AddAttributeEffectForDurationAction : ITriggerActionV2
    {
        private readonly string _attrName;
        private readonly ModifierOp _op;
        private readonly float _value;
        private readonly int _sourceId;
        private readonly float _duration;

        public AddAttributeEffectForDurationAction(string attrName, ModifierOp op, float value, int sourceId, float duration)
        {
            _attrName = attrName ?? throw new ArgumentNullException(nameof(attrName));
            _op = op;
            _value = value;
            _sourceId = sourceId;
            _duration = duration;
        }

        public static AddAttributeEffectForDurationAction FromDef(ActionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var args = def.Args;
            if (args == null) throw new InvalidOperationException("Action args is null");

            if (!args.TryGetValue("attr", out var attrObj) || !(attrObj is string attr) || string.IsNullOrEmpty(attr))
            {
                throw new InvalidOperationException("AddAttributeEffectForDurationAction requires args['attr'] as non-empty string");
            }

            var op = ModifierOp.Add;
            if (args.TryGetValue("op", out var opObj) && opObj is string opStr && !string.IsNullOrEmpty(opStr))
            {
                if (string.Equals(opStr, "add", StringComparison.OrdinalIgnoreCase)) op = ModifierOp.Add;
                else if (string.Equals(opStr, "mul", StringComparison.OrdinalIgnoreCase)) op = ModifierOp.Mul;
                else if (string.Equals(opStr, "percent_add", StringComparison.OrdinalIgnoreCase)) op = ModifierOp.PercentAdd;
                else if (string.Equals(opStr, "override", StringComparison.OrdinalIgnoreCase)) op = ModifierOp.Override;
            }

            if (!args.TryGetValue("value", out var vObj)) throw new InvalidOperationException("AddAttributeEffectForDurationAction requires args['value']");
            var value = vObj is float f ? f : vObj is int i ? i : Convert.ToSingle(vObj);

            var sourceId = 0;
            if (args.TryGetValue("sourceId", out var sidObj))
            {
                sourceId = sidObj is int si ? si : Convert.ToInt32(sidObj);
            }

            var duration = 0f;
            if (args.TryGetValue("duration", out var dObj))
            {
                duration = dObj is float df ? df : dObj is int di ? di : Convert.ToSingle(dObj);
            }

            return new AddAttributeEffectForDurationAction(attr, op, value, sourceId, duration);
        }

        public void Execute(TriggerContext context)
        {
            Start(context);
        }

        public IRunningAction Start(TriggerContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var unit = context.Target as IUnitFacade;
            if (unit == null) return null;

            if (!Attributes.TryAttr(_attrName, out var attrId) || !attrId.IsValid)
            {
                return null;
            }

            // 使用新的 API 创建效果
            var effect = _op switch
            {
                ModifierOp.Add => AttributeEffect.Add(attrId, _value, _sourceId),
                ModifierOp.Mul => AttributeEffect.Mul(attrId, _value, _sourceId),
                ModifierOp.PercentAdd => AttributeEffect.PercentAdd(attrId, _value, _sourceId),
                ModifierOp.Override => AttributeEffect.Override(attrId, _value, _sourceId),
                _ => AttributeEffect.Add(attrId, _value, _sourceId)
            };

            var sourceId = unit.Attributes.ApplyEffect(effect);
            if (sourceId == 0) return null;

            return new AttributeEffectDurationRunningAction(sourceId, _duration);
        }
    }
}
