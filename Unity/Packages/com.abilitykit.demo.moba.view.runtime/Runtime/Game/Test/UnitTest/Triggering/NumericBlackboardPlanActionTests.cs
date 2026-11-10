using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Demo.Moba.Services.Triggering.PlanActions;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using NUnit.Framework;

namespace AbilityKit.Game.UnitTests.Triggering
{
    public sealed class NumericBlackboardPlanActionTests
    {
        [Test]
        public void DefaultRegistry_ContainsNumericBlackboardActions()
        {
            using var registry = PlanActionModuleRegistry.CreateDefault();

            Assert.That(ContainsAction(registry, "set_num_var"), Is.True);
            Assert.That(ContainsAction(registry, "add_num_var"), Is.True);
            Assert.That(ContainsAction(registry, "set_var"), Is.True);
            Assert.That(ContainsAction(registry, "advance_gameplay_counter"), Is.True);
            Assert.That(
                (int)TriggeringConstants.AdvanceGameplayCounterId.Value,
                Is.EqualTo(327267303));
        }

        [Test]
        public void Schemas_ParseBlackboardTargetAndNumericOperand()
        {
            var target = new BlackboardWriteTarget(101, 202, BlackboardKeyType.Double, "owner");
            var args = new Dictionary<string, ActionArgValue>
            {
                ["target"] = ActionArgValue.OfBlackboardTarget(in target, "target"),
                ["value"] = ActionArgValue.OfConst(3.5, "value")
            };
            var ctx = default(ExecCtx<IWorldResolver>);

            var set = SetNumericBlackboardSchema.Instance.ParseArgs(args, ctx);
            var add = AddNumericBlackboardSchema.Instance.ParseArgs(args, ctx);

            Assert.That(set.Target, Is.EqualTo(target));
            Assert.That(set.Value, Is.EqualTo(3.5));
            Assert.That(add.Target, Is.EqualTo(target));
            Assert.That(add.Value, Is.EqualTo(3.5));
        }

        [Test]
        public void SetVariableSchema_ParsesBooleanAndStringOperands()
        {
            var boolTarget = new BlackboardWriteTarget(101, 202, BlackboardKeyType.Bool, "owner");
            var boolArgs = new Dictionary<string, ActionArgValue>
            {
                ["target"] = ActionArgValue.OfBlackboardTarget(in boolTarget, "target"),
                ["value"] = ActionArgValue.OfBool(true, "value")
            };
            var stringTarget = new BlackboardWriteTarget(101, 203, BlackboardKeyType.String, "owner");
            var stringArgs = new Dictionary<string, ActionArgValue>
            {
                ["target"] = ActionArgValue.OfBlackboardTarget(in stringTarget, "target"),
                ["value"] = ActionArgValue.OfString("armed", "value")
            };

            var boolResult = SetBlackboardVariableSchema.Instance.ParseArgs(boolArgs, default);
            var stringResult = SetBlackboardVariableSchema.Instance.ParseArgs(stringArgs, default);

            Assert.That(boolResult.Target, Is.EqualTo(boolTarget));
            Assert.That(boolResult.Value.Kind, Is.EqualTo(ActionArgKind.BooleanValue));
            Assert.That(boolResult.Value.BooleanValue, Is.True);
            Assert.That(stringResult.Target, Is.EqualTo(stringTarget));
            Assert.That(stringResult.Value.Kind, Is.EqualTo(ActionArgKind.StringValue));
            Assert.That(stringResult.Value.StringValue, Is.EqualTo("armed"));
        }

        [Test]
        public void SetGameplayVarSchema_SupportsActorScopedAndGlobalKeys()
        {
            const int baseKey = 73001;
            const int actorFieldId = 1993035528;
            var scopedInput = new Dictionary<string, ActionArgValue>
            {
                ["key_id"] = ActionArgValue.OfConst(baseKey, "key_id"),
                ["value"] = ActionArgValue.OfConst(0, "value"),
                ["scope_payload_field_id"] = ActionArgValue.OfConst(actorFieldId, "scope_payload_field_id")
            };
            var scoped = SetGameplayVarSchema.Instance.ParseArgs(scopedInput, default);

            Assert.That(scoped.ScopePayloadFieldId, Is.EqualTo(actorFieldId));
            Assert.That(scoped.ResolveKey(44), Is.EqualTo(MobaGameplayVariableKeys.ResolveScopedKey(baseKey, 44)));

            var global = SetGameplayVarSchema.Instance.ParseArgs(new Dictionary<string, ActionArgValue>
            {
                ["key_id"] = ActionArgValue.OfConst(baseKey, "key_id"),
                ["value"] = ActionArgValue.OfConst(0, "value")
            }, default);
            Assert.That(global.ScopePayloadFieldId, Is.Zero);
            Assert.That(global.ResolveKey(44), Is.EqualTo(baseKey));
        }

        private static bool ContainsAction(PlanActionModuleRegistry registry, string actionName)
        {
            var descriptors = registry.Descriptors;
            for (var i = 0; i < descriptors.Length; i++)
            {
                if (descriptors[i].ActionName == actionName) return true;
            }
            return false;
        }
    }
}
