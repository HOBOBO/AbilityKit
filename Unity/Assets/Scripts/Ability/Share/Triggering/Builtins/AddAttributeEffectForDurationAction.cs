using System;
using AbilityKit.Ability.Share.Common.AttributeSystem;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Triggering.Definitions;

namespace AbilityKit.Ability.Triggering.Runtime.Builtins
{
    public sealed class AddAttributeEffectForDurationAction : ITriggerActionV2
    {
        private readonly string _attrName;
        private readonly AttributeModifierOp _op;
        private readonly float _value;
        private readonly int _sourceId;
        private readonly float _duration;

        public AddAttributeEffectForDurationAction(string attrName, AttributeModifierOp op, float value, int sourceId, float duration)
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

            var op = AttributeModifierOp.Add;
            if (args.TryGetValue("op", out var opObj) && opObj is string opStr && !string.IsNullOrEmpty(opStr))
            {
                if (string.Equals(opStr, "add", StringComparison.OrdinalIgnoreCase)) op = AttributeModifierOp.Add;
                else if (string.Equals(opStr, "mul", StringComparison.OrdinalIgnoreCase)) op = AttributeModifierOp.Mul;
                else if (string.Equals(opStr, "final_add", StringComparison.OrdinalIgnoreCase)) op = AttributeModifierOp.FinalAdd;
                else if (string.Equals(opStr, "override", StringComparison.OrdinalIgnoreCase)) op = AttributeModifierOp.Override;
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

            var effect = new AttributeEffect(
                new AttributeEffect.Entry(
                    attrId,
                    new AttributeModifier(_op, _value, _sourceId)
                )
            );

            var handle = unit.Attributes.ApplyEffect(effect);
            if (handle == null) return null;

            return new AttributeEffectDurationRunningAction(handle, _duration);
        }
    }
}
