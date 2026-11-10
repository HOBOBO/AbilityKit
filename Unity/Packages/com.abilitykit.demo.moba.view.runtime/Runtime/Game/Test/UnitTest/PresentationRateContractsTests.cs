using NUnit.Framework;
using AbilityKit.Game.Flow;

namespace AbilityKit.Game.UnitTests
{
    public sealed class PresentationRateContractsTests
    {
        [Test]
        public void Resolver_ComposesMultiplySources()
        {
            var resolver = new PresentationRateResolver();
            resolver.Set(new PresentationRateSource("global", PresentationRateChannel.Vfx, 2f));
            resolver.Set(new PresentationRateSource("skill", PresentationRateChannel.Vfx, 0.5f));

            Assert.AreEqual(1f, resolver.ResolveRate(PresentationRateChannel.Vfx));
            Assert.AreEqual(0.25f, resolver.Resolve(0.25f, PresentationRateChannel.Vfx).DeltaSeconds);
        }

        [Test]
        public void Resolver_UsesHighestPriorityOverrideAndPause()
        {
            var resolver = new PresentationRateResolver();
            resolver.Set(new PresentationRateSource("slow", PresentationRateChannel.Action, 0.5f));
            resolver.Set(new PresentationRateSource("override-low", PresentationRateChannel.Action, 4f,
                PresentationRateMode.Override, 1));
            resolver.Set(new PresentationRateSource("override-high", PresentationRateChannel.Action, 2f,
                PresentationRateMode.Override, 2));

            Assert.AreEqual(1f, resolver.ResolveRate(PresentationRateChannel.Action));

            resolver.Set(new PresentationRateSource("pause", PresentationRateChannel.Action, 1f,
                PresentationRateMode.Pause, 3));
            Assert.AreEqual(0f, resolver.ResolveRate(PresentationRateChannel.Action));
        }

        [Test]
        public void Resolver_DefaultsToOneAndSanitizesDelta()
        {
            var resolver = new PresentationRateResolver();
            Assert.AreEqual(1f, resolver.ResolveRate(PresentationRateChannel.SkillTimeline));
            Assert.AreEqual(0f, resolver.Resolve(float.NaN, PresentationRateChannel.SkillTimeline).DeltaSeconds);
            Assert.AreEqual(0f, resolver.Resolve(-1f, PresentationRateChannel.SkillTimeline).DeltaSeconds);
        }

        [Test]
        public void Resolver_ExplainsControlAndCopiesStableSources()
        {
            var resolver = new PresentationRateResolver();
            resolver.Set(new PresentationRateSource("z-multiply", PresentationRateChannel.Vfx, 2f));
            resolver.Set(new PresentationRateSource("a-override", PresentationRateChannel.Vfx, 0.5f,
                PresentationRateMode.Override, 3));
            resolver.Set(new PresentationRateSource("pause-low", PresentationRateChannel.Vfx, 1f,
                PresentationRateMode.Pause, 1));
            var explanation = resolver.Explain(PresentationRateChannel.Vfx);
            Assert.AreEqual(1f, explanation.EffectiveRate);
            Assert.AreEqual(2f, explanation.Multiplier);
            Assert.AreEqual("a-override", explanation.ControlSourceId);
            var sources = new System.Collections.Generic.List<PresentationRateSource>();
            resolver.CopySources(sources, PresentationRateChannel.Vfx);
            Assert.AreEqual("a-override", sources[0].SourceId);
            Assert.AreEqual("z-multiply", sources[2].SourceId);
        }

        [Test]
        public void Lifecycle_ConsumesEffectivePlaybackDelta()
        {
            var lifecycle = new PresentationPlaybackLifecycle(
                new PresentationPlaybackSpec(playCount: 1), 1f);

            Assert.IsFalse(lifecycle.Tick(0.25f, 2f));
            Assert.AreEqual(0.5f, lifecycle.Snapshot.ElapsedSeconds);
            Assert.IsTrue(lifecycle.Tick(0.25f, 2f));
            Assert.AreEqual(PresentationPlaybackPhase.Completed, lifecycle.Snapshot.Phase);
        }
    }
}
