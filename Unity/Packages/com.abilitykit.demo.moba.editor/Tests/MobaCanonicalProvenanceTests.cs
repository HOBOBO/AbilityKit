using System;
using AbilityKit.Demo.Moba.Services;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaCanonicalProvenanceTests
    {
        [Test]
        public void ResolveContextSource_EnrichesMissingCanonicalFields()
        {
            var handle = new MobaSkillCastRuntimeHandle(41L, 2, 1001L);
            var payload = new CompositeProvider(
                CreateSource(
                    sourceActorId: 11,
                    sourceContextId: 1001L,
                    configId: 701),
                CreateSnapshot(
                    sourceActorId: 11,
                    targetActorId: 12,
                    sourceContextId: 1001L,
                    rootContextId: 1002L,
                    ownerContextId: 1003L,
                    configId: 702,
                    skillRuntimeHandle: handle));

            Assert.That(payload.TryResolveContextSource(out var resolved), Is.True);
            Assert.That(resolved.ResolveKind, Is.EqualTo(MobaContextSourceResolveKind.DirectProvider));
            Assert.That(resolved.SourceActorId, Is.EqualTo(11));
            Assert.That(resolved.TargetActorId, Is.EqualTo(12));
            Assert.That(resolved.SourceContextId, Is.EqualTo(1001L));
            Assert.That(resolved.ParentContextId, Is.EqualTo(1001L));
            Assert.That(resolved.RootContextId, Is.EqualTo(1002L));
            Assert.That(resolved.OwnerContextId, Is.EqualTo(1003L));
            Assert.That(resolved.SkillRuntimeHandle, Is.EqualTo(handle));
            Assert.That(resolved.ConfigId, Is.EqualTo(701), "Origin and execution configuration identities must remain distinct.");
        }

        [TestCase("sourceActorId")]
        [TestCase("targetActorId")]
        [TestCase("sourceContextId")]
        [TestCase("parentContextId")]
        [TestCase("rootContextId")]
        [TestCase("ownerContextId")]
        [TestCase("skillRuntimeHandle")]
        public void ResolveContextSource_RejectsCanonicalIdentityConflict(string field)
        {
            var firstHandle = new MobaSkillCastRuntimeHandle(41L, 2, 1001L);
            var secondHandle = field == "skillRuntimeHandle"
                ? new MobaSkillCastRuntimeHandle(42L, 2, 1001L)
                : firstHandle;
            var sourceContextId = field == "sourceContextId" || field == "parentContextId" ? 2001L : 1001L;
            var payload = new CompositeProvider(
                CreateSource(
                    sourceActorId: 11,
                    targetActorId: 12,
                    sourceContextId: field == "parentContextId" ? 0L : 1001L,
                    parentContextId: 1001L,
                    rootContextId: 1002L,
                    ownerContextId: 1003L,
                    skillRuntimeHandle: firstHandle),
                CreateSnapshot(
                    sourceActorId: field == "sourceActorId" ? 21 : 11,
                    targetActorId: field == "targetActorId" ? 22 : 12,
                    sourceContextId: sourceContextId,
                    rootContextId: field == "rootContextId" ? 2002L : 1002L,
                    ownerContextId: field == "ownerContextId" ? 2003L : 1003L,
                    skillRuntimeHandle: secondHandle));

            var error = Assert.Throws<InvalidOperationException>(
                () => payload.TryResolveContextSource(out _));

            Assert.That(error.Message, Does.Contain($"field={field}"));
        }

        [TestCase("contextKind")]
        [TestCase("triggerId")]
        [TestCase("frame")]
        public void ResolveContextSource_RejectsExecutionMetadataConflict(string field)
        {
            var payload = new CompositeProvider(
                CreateSource(
                    contextKind: EffectContextKind.Skill,
                    sourceActorId: 11,
                    sourceContextId: 1001L,
                    triggerId: 701,
                    frame: 10),
                CreateSnapshot(
                    kind: field == "contextKind" ? EffectContextKind.Buff : EffectContextKind.Skill,
                    sourceActorId: 11,
                    sourceContextId: 1001L,
                    triggerId: field == "triggerId" ? 702 : 701,
                    frame: field == "frame" ? 11 : 10));

            var error = Assert.Throws<InvalidOperationException>(
                () => payload.TryResolveContextSource(out _));

            Assert.That(error.Message, Does.Contain($"field={field}"));
        }

        [Test]
        public void CanonicalProvenance_TracksCompletenessPerField()
        {
            var source = CreateSource(
                sourceActorId: 11,
                sourceContextId: 1001L,
                rootContextId: 1002L);

            var provenance = MobaCanonicalProvenance.FromContextSource(in source);

            Assert.That(provenance.SourceActorState, Is.EqualTo(MobaProvenanceFieldState.Explicit));
            Assert.That(provenance.TargetActorState, Is.EqualTo(MobaProvenanceFieldState.Missing));
            Assert.That(provenance.SourceContextState, Is.EqualTo(MobaProvenanceFieldState.Explicit));
            Assert.That(provenance.ParentContextState, Is.EqualTo(MobaProvenanceFieldState.Missing));
            Assert.That(provenance.RootContextState, Is.EqualTo(MobaProvenanceFieldState.Explicit));
            Assert.That(provenance.OwnerContextState, Is.EqualTo(MobaProvenanceFieldState.Missing));
            Assert.That(provenance.SkillRuntimeState, Is.EqualTo(MobaProvenanceFieldState.Missing));
        }

        [Test]
        public void WithEffectExecutionNode_RootPromotesCurrentNodeToRoot()
        {
            var context = CreateExecutionContext(parentContextId: 0L, rootContextId: 0L, ownerContextId: 0L);

            var advanced = context.WithEffectExecutionNode(2001L, 702, true);

            Assert.That(advanced.ParentContextId, Is.EqualTo(2001L));
            Assert.That(advanced.ExecutionSnapshot.SourceContextId, Is.EqualTo(2001L));
            Assert.That(advanced.RootContextId, Is.EqualTo(2001L));
            Assert.That(advanced.OwnerContextId, Is.EqualTo(2001L));
            Assert.That(advanced.ConfigId, Is.EqualTo(702));
        }

        [Test]
        public void WithEffectExecutionNode_ChildAdvancesParentAndPreservesRootOwnership()
        {
            var handle = new MobaSkillCastRuntimeHandle(41L, 2, 1001L);
            var context = CreateExecutionContext(
                parentContextId: 1002L,
                rootContextId: 1001L,
                ownerContextId: 1003L,
                skillRuntimeHandle: handle);

            var advanced = context.WithEffectExecutionNode(2001L, 702, false);

            Assert.That(advanced.ParentContextId, Is.EqualTo(2001L));
            Assert.That(advanced.ExecutionSnapshot.SourceContextId, Is.EqualTo(2001L));
            Assert.That(advanced.RootContextId, Is.EqualTo(1001L));
            Assert.That(advanced.OwnerContextId, Is.EqualTo(1003L));
            Assert.That(advanced.SkillRuntimeHandle, Is.EqualTo(handle));
            Assert.That(advanced.ConfigId, Is.EqualTo(702));
        }

        private static MobaCombatExecutionContext CreateExecutionContext(
            long parentContextId,
            long rootContextId,
            long ownerContextId,
            MobaSkillCastRuntimeHandle skillRuntimeHandle = default)
        {
            var lineage = new MobaEffectLineageInput(
                EffectContextKind.Skill,
                MobaTraceKind.SkillEffect,
                11,
                12,
                parentContextId,
                rootContextId,
                ownerContextId,
                701);
            var snapshot = new MobaTriggerExecutionSnapshot(
                EffectContextKind.Skill,
                11,
                12,
                parentContextId,
                rootContextId,
                ownerContextId,
                801,
                701,
                10,
                skillRuntimeHandle);
            return new MobaCombatExecutionContext(
                new object(),
                lineage,
                default,
                snapshot,
                skillRuntimeHandle,
                10);
        }

        private static MobaContextSourceView CreateSource(
            EffectContextKind contextKind = EffectContextKind.Skill,
            int sourceActorId = 0,
            int targetActorId = 0,
            long sourceContextId = 0L,
            long parentContextId = 0L,
            long rootContextId = 0L,
            long ownerContextId = 0L,
            int configId = 0,
            int triggerId = 0,
            int frame = 0,
            MobaSkillCastRuntimeHandle skillRuntimeHandle = default)
        {
            return new MobaContextSourceView(
                MobaContextSourceResolveKind.DirectProvider,
                MobaContextSourceBoundary.Snapshot,
                contextKind,
                MobaTraceKind.SkillEffect,
                sourceActorId,
                targetActorId,
                sourceContextId,
                parentContextId,
                rootContextId,
                ownerContextId,
                configId,
                triggerId,
                frame,
                null,
                0,
                false,
                skillRuntimeHandle);
        }

        private static MobaTriggerExecutionSnapshot CreateSnapshot(
            EffectContextKind kind = EffectContextKind.Skill,
            int sourceActorId = 0,
            int targetActorId = 0,
            long sourceContextId = 0L,
            long rootContextId = 0L,
            long ownerContextId = 0L,
            int triggerId = 0,
            int configId = 0,
            int frame = 0,
            MobaSkillCastRuntimeHandle skillRuntimeHandle = default)
        {
            return new MobaTriggerExecutionSnapshot(
                kind,
                sourceActorId,
                targetActorId,
                sourceContextId,
                rootContextId,
                ownerContextId,
                triggerId,
                configId,
                frame,
                skillRuntimeHandle);
        }

        private sealed class CompositeProvider : IMobaContextSourceProvider, IMobaTriggerExecutionSnapshotProvider
        {
            private readonly MobaContextSourceView _source;
            private readonly MobaTriggerExecutionSnapshot _snapshot;

            public CompositeProvider(
                MobaContextSourceView source,
                MobaTriggerExecutionSnapshot snapshot)
            {
                _source = source;
                _snapshot = snapshot;
            }

            public bool TryGetContextSource(out MobaContextSourceView source)
            {
                source = _source;
                return source.IsValid;
            }

            public bool TryGetExecutionSnapshot(out MobaTriggerExecutionSnapshot snapshot)
            {
                snapshot = _snapshot;
                return snapshot.IsValid;
            }
        }
    }
}
