using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services.Triggering.PlanActions;
using AbilityKit.Triggering.Runtime;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaPlanActionInputResolverTests
    {
        [Test]
        public void TryResolve_WithoutSessionOrPayloadContext_ReturnsFalse()
        {
            var ctx = CreateContextWithoutCombatExecutionContext();

            var resolved = MobaPlanActionInputResolver.TryResolve(
                triggerArgs: null,
                ctx,
                out var input);

            Assert.That(resolved, Is.False);
            Assert.That(input, Is.EqualTo(default(MobaPlanActionInput)));
        }

        [Test]
        public void TryResolveEffect_WithoutSessionOrPayloadContext_ReturnsFalse()
        {
            var ctx = CreateContextWithoutCombatExecutionContext();

            var resolved = MobaPlanActionInputResolver.TryResolveEffect(
                triggerArgs: null,
                ctx,
                out var input);

            Assert.That(resolved, Is.False);
            Assert.That(input, Is.EqualTo(default(MobaEffectActionInput)));
        }

        private static ExecCtx<IWorldResolver> CreateContextWithoutCombatExecutionContext()
        {
            return new ExecCtx<IWorldResolver>(
                context: null,
                eventBus: null,
                functions: null,
                actions: null,
                blackboards: null,
                payloads: null,
                idNames: null,
                numericDomains: null,
                numericFunctions: null,
                policy: default,
                control: null);
        }
    }
}
