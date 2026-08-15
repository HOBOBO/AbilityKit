using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow;
using AbilityKit.World.ECS;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattlePresentationContextTests
    {
        [Test]
        public void BindVfx_TwoContextsDoNotShareMutablePresentationState()
        {
            var first = new BattlePresentationContext();
            var second = new BattlePresentationContext();
            var firstManager = new BattleVfxManager(null);
            var secondManager = new BattleVfxManager(null);
            var firstWorld = new EntityWorld();
            var secondWorld = new EntityWorld();
            var firstNode = firstWorld.Create("first-vfx");
            var secondNode = secondWorld.Create("second-vfx");

            first.BindVfx(firstManager, firstNode);
            second.BindVfx(secondManager, secondNode);

            Assert.That(first.ViewVfxManager, Is.SameAs(firstManager));
            Assert.That(second.ViewVfxManager, Is.SameAs(secondManager));
            Assert.That(first.ViewVfxManager, Is.Not.SameAs(second.ViewVfxManager));
            Assert.That(first.ViewVfxNode, Is.EqualTo(firstNode));
            Assert.That(second.ViewVfxNode, Is.EqualTo(secondNode));
        }

        [Test]
        public void ClearVfx_StaleGenerationDoesNotClearReplacementBinding()
        {
            var owner = new BattlePresentationContext();
            var world = new EntityWorld();
            var originalManager = new BattleVfxManager(null);
            var replacementManager = new BattleVfxManager(null);
            var originalGeneration = owner.BindVfx(
                originalManager,
                world.Create("original-vfx"));
            var replacementNode = world.Create("replacement-vfx");
            var replacementGeneration = owner.BindVfx(
                replacementManager,
                replacementNode);

            Assert.That(owner.ClearVfx(originalGeneration), Is.False);
            Assert.That(owner.ViewVfxManager, Is.SameAs(replacementManager));
            Assert.That(owner.ViewVfxNode, Is.EqualTo(replacementNode));
            Assert.That(owner.ClearVfx(replacementGeneration), Is.True);
            Assert.That(owner.ViewVfxManager, Is.Null);
            Assert.That(owner.ViewVfxNode.IsValid, Is.False);
        }

        [Test]
        public void EndRemoteInterpolation_StaleGenerationDoesNotDisableReplacement()
        {
            var owner = new BattlePresentationContext();
            var originalGeneration = owner.BeginRemoteInterpolation();
            var replacementGeneration = owner.BeginRemoteInterpolation();

            Assert.That(owner.EndRemoteInterpolation(originalGeneration), Is.False);
            Assert.That(owner.EnableRemoteInterpolation, Is.True);
            Assert.That(owner.EndRemoteInterpolation(replacementGeneration), Is.True);
            Assert.That(owner.EnableRemoteInterpolation, Is.False);
        }

        [Test]
        public void Reset_ClearsPresentationStateAndInvalidatesExistingGenerations()
        {
            var owner = new BattlePresentationContext();
            var world = new EntityWorld();
            var vfxGeneration = owner.BindVfx(
                new BattleVfxManager(null),
                world.Create("reset-vfx"));
            var interpolationGeneration = owner.BeginRemoteInterpolation();

            owner.Reset();

            Assert.That(owner.EnableRemoteInterpolation, Is.False);
            Assert.That(owner.ViewVfxManager, Is.Null);
            Assert.That(owner.ViewVfxNode.IsValid, Is.False);
            Assert.That(owner.ClearVfx(vfxGeneration), Is.False);
            Assert.That(
                owner.EndRemoteInterpolation(interpolationGeneration),
                Is.False);
        }
    }
}
