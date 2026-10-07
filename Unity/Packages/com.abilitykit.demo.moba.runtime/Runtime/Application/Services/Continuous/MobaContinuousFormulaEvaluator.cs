using System;
using AbilityKit.Attributes.Core;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;

namespace AbilityKit.Demo.Moba.Services
{
    internal static class MobaContinuousFormulaEvaluator
    {
        private const double Precision = 10000d;

        public static bool TryEvaluate(MobaActorLookupService actors,
            IMobaContinuousProjectionConfig projection, IMobaContinuousFormulaSpec formula,
            int stack, int op, out float value)
        {
            value = 0f;
            if (actors == null || projection == null || formula == null || !formula.HasFormula ||
                !TryGetContext(actors, projection.OwnerActorId, out var owner)) return false;

            var sum = Quantize(formula.FormulaConstant + formula.FormulaLevelCoefficient * owner.Level);
            if (double.IsNaN(sum) || double.IsInfinity(sum)) return false;
            var terms = formula.FormulaTerms;
            if (terms == null || terms.Count > 8) return false;
            for (var i = 0; i < terms.Count; i++)
            {
                var term = terms[i];
                var actorId = MobaModifierAttributeSourceResolver.ResolveActorId(projection, term.ActorRole);
                var attributeId = MobaAttributeIds.Get((BattleAttributeType)term.AttributeTypeId);
                if (actorId <= 0 || !attributeId.IsValid ||
                    !TryGetContext(actors, actorId, out var context) ||
                    !context.GetGroupFor(attributeId).TryGet(attributeId, out var attribute)) return false;
                sum = Quantize(sum + Quantize(attribute.BaseValue * term.Coefficient));
                if (double.IsNaN(sum) || double.IsInfinity(sum)) return false;
            }
            if (stack > 1)
                sum = Quantize(MobaContinuousModifierMath.ApplyStackValue((float)sum, stack, op));
            if (sum < float.MinValue || sum > float.MaxValue) return false;
            value = (float)sum;
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool TryGetContext(MobaActorLookupService actors, int actorId,
            out AttributeContext context)
        {
            context = null;
            if (actorId <= 0 || !actors.TryGetActorEntity(actorId, out var entity) ||
                entity == null || !entity.hasAttributeGroup) return false;
            context = entity.attributeGroup.Ctx;
            return context != null;
        }

        private static double Quantize(double value) => Math.Round(value * Precision,
            MidpointRounding.ToEven) / Precision;
    }
}
