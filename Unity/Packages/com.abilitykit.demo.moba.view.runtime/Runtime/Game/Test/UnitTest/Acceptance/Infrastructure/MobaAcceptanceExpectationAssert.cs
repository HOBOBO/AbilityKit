using System;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Attributes;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public static class MobaAcceptanceExpectationAssert
    {
        public static void AssertMatches(MobaAcceptanceExpectation expectation, MobaAcceptanceTraceRecord[] records,
            MobaSkillConfigTestHarness harness = null, MobaAcceptanceTraceEvidence evidence = null)
        {
            if (expectation == null) throw new ArgumentNullException(nameof(expectation));
            if (records == null) throw new ArgumentNullException(nameof(records));

            var effectRootId = FindFirstTraceRootId(records, "EffectExecution", expectation.config != null ? expectation.config.effectId : 0);
            if (expectation.config != null && expectation.config.effectId > 0)
            {
                Assert.IsTrue(effectRootId > 0, BuildMissingEffectRootMessage(expectation, harness));
            }

            AssertContainsAll(records, expectation.mustContain, effectRootId);
            AssertContainsNone(records, expectation.mustNotContain);
            if (expectation.mustNotContain != null && expectation.mustNotContain.Length > 0)
                Assert.IsTrue(MobaAcceptanceTraceEvidence.CanProveAbsence(evidence, records.Length),
                    $"mustNotContain requires complete trace evidence: {evidence?.reason ?? "capture not verified"}.");
            AssertConfigActions(records, expectation, effectRootId);
            AssertRelationships(records, expectation.relationships);
        }

        public static void AssertStateMatches(MobaAcceptanceExpectation expectation, MobaAcceptanceTraceRecord[] records, MobaSkillConfigTestHarness harness)
        {
            if (expectation == null) throw new ArgumentNullException(nameof(expectation));
            if (harness == null) throw new ArgumentNullException(nameof(harness));

            AssertStateExpectations(expectation, harness, expectation.stateExpectations);
            AssertStateExpectations(expectation, harness, expectation.scenario != null ? expectation.scenario.stateExpectations : null);
            AssertContextExpectations(expectation, harness, records, expectation.contextExpectations);
            AssertContextExpectations(expectation, harness, records, expectation.scenario != null ? expectation.scenario.contextExpectations : null);
        }

        public static bool TryGetEffectRootId(MobaAcceptanceTraceRecord[] records, int effectId, out long effectRootId)
        {
            effectRootId = FindFirstTraceRootId(records, "EffectExecution", effectId);
            return effectRootId > 0;
        }

        private static void AssertContainsAll(MobaAcceptanceTraceRecord[] records, MobaAcceptanceTraceExpectation[] expectations, long effectRootId)
        {
            if (expectations == null) return;
 
            for (var i = 0; i < expectations.Length; i++)
            {
                var expectation = expectations[i];
                var count = Count(records, expectation.kind, expectation.configId, expectation.underEffectId > 0 ? effectRootId : 0);
                var minCount = expectation.minCount > 0 ? expectation.minCount : 1;
                Assert.GreaterOrEqual(
                    count,
                    minCount,
                    BuildMissingTraceNodeMessage(records, expectation, effectRootId, minCount, count));
 
                if (expectation.maxCount > 0)
                {
                    Assert.LessOrEqual(
                        count,
                        expectation.maxCount,
                        $"Required trace node exceeds max count: kind={expectation.kind}, configId={expectation.configId}, underEffectId={expectation.underEffectId}, maxCount={expectation.maxCount}, actualCount={count}.");
                }
            }
        }
        private static string BuildMissingTraceNodeMessage(MobaAcceptanceTraceRecord[] records, MobaAcceptanceTraceExpectation expectation, long effectRootId, int minCount, int actualCount)
        {
            return $"Missing required trace node: kind={expectation.kind}, configId={expectation.configId}, underEffectId={expectation.underEffectId}, minCount={minCount}, actualCount={actualCount}. {BuildTraceDebugSnapshot(records, expectation, effectRootId)}";
        }

        private static string BuildTraceDebugSnapshot(MobaAcceptanceTraceRecord[] records, MobaAcceptanceTraceExpectation expectation, long effectRootId)
        {
            if (records == null || records.Length == 0)
            {
                return "TraceDebug: no records captured.";
            }

            var expectedRootId = expectation != null && expectation.underEffectId > 0 ? effectRootId : 0;
            var matchingKindCount = 0;
            var matchingConfigCount = 0;
            var matchingRootCount = 0;
            var sample = new System.Text.StringBuilder();
            var sampleCount = 0;
            var rootChildren = new System.Text.StringBuilder();
            var rootChildCount = 0;

            for (var i = 0; i < records.Length; i++)
            {
                var record = records[i];
                if (record == null) continue;

                if (string.Equals(record.kind, expectation.kind, StringComparison.OrdinalIgnoreCase))
                {
                    matchingKindCount++;
                    if (record.configId == expectation.configId)
                    {
                        matchingConfigCount++;
                        if (expectedRootId <= 0 || record.rootId == expectedRootId)
                        {
                            matchingRootCount++;
                        }

                        if (sampleCount < 6)
                        {
                            if (sampleCount > 0) sample.Append(" | ");
                            sample.Append(FormatTraceRecord(record));
                            sampleCount++;
                        }
                    }
                }

                if (expectedRootId > 0 && record.rootId == expectedRootId && rootChildCount < 12)
                {
                    if (rootChildCount > 0) rootChildren.Append(" | ");
                    rootChildren.Append(record.kind);
                    rootChildren.Append('(');
                    rootChildren.Append(record.configId);
                    rootChildren.Append(")#");
                    rootChildren.Append(record.nodeId);
                    if (record.parentId != 0)
                    {
                        rootChildren.Append("<-p");
                        rootChildren.Append(record.parentId);
                    }

                    rootChildCount++;
                }
            }

            var sampleText = sampleCount > 0 ? sample.ToString() : "<none>";
            var rootText = rootChildCount > 0 ? rootChildren.ToString() : "<none>";
            return $"TraceDebug: expectedRootId={expectedRootId}, totalRecords={records.Length}, matchingKindCount={matchingKindCount}, matchingConfigCount={matchingConfigCount}, matchingRootCount={matchingRootCount}, matchingSamples={sampleText}, rootRecords={rootText}";
        }

        private static string FormatTraceRecord(MobaAcceptanceTraceRecord record)
        {
            if (record == null)
            {
                return "<null>";
            }

            return $"{record.kind}({record.configId})#node={record.nodeId},root={record.rootId},parent={record.parentId},src={record.sourceActorId},dst={record.targetActorId}";
        }

        private static void AssertContainsNone(MobaAcceptanceTraceRecord[] records, MobaAcceptanceTraceExpectation[] expectations)
        {
            if (expectations == null) return;

            var missingScope = 0;
            for (var i = 0; i < expectations.Length; i++)
            {
                var expectation = expectations[i];
                var count = MobaAcceptanceForbiddenTrace.Count(records, expectation, out var scopeFound);
                if (!scopeFound) missingScope = expectation.underEffectId;
                Assert.AreEqual(
                    0,
                    count,
                    $"Unexpected trace node present: kind={expectation.kind}, configId={expectation.configId}, underEffectId={expectation.underEffectId}, actualCount={count}.");
            }
            Assert.AreEqual(0, missingScope, $"mustNotContain requires an observed effect scope: underEffectId={missingScope}.");
        }

        private static void AssertConfigActions(MobaAcceptanceTraceRecord[] records, MobaAcceptanceExpectation expectation, long effectRootId)
        {
            if (expectation.config == null || expectation.config.expectedActions == null) return;

            for (var i = 0; i < expectation.config.expectedActions.Length; i++)
            {
                var action = expectation.config.expectedActions[i];
                Assert.IsTrue(
                    Contains(records, "EffectAction", action.actionId, effectRootId),
                    $"Missing expected effect action {action.type}({action.actionId}) under effect root {effectRootId}.");
            }

            if (expectation.config.expectedProjectile != null && expectation.config.expectedProjectile.projectileId > 0)
            {
                Assert.IsTrue(
                    Contains(records, "ProjectileLaunch", expectation.config.expectedProjectile.projectileId, effectRootId),
                    $"Missing expected projectile launch {expectation.config.expectedProjectile.projectileId} under effect root {effectRootId}.");
            }
        }

        private static void AssertRelationships(MobaAcceptanceTraceRecord[] records, MobaAcceptanceRelationshipExpectation[] relationships)
        {
            if (relationships == null) return;

            for (var i = 0; i < relationships.Length; i++)
            {
                var relationship = relationships[i];
                Assert.IsTrue(
                    HasRelationship(records, relationship, out var message),
                    message);
            }
        }

        private static void AssertStateExpectations(MobaAcceptanceExpectation expectation, MobaSkillConfigTestHarness harness, MobaAcceptanceStateExpectation[] expectations)
        {
            if (expectations == null) return;

            var lookup = harness.World.Services.Resolve<AbilityKit.Demo.Moba.Services.MobaActorLookupService>();
            for (var i = 0; i < expectations.Length; i++)
            {
                var state = expectations[i];
                if (state == null) continue;

                var actorId = ResolveActorId(harness, state.alias, state.actorId);
                AssertStateExpectation(expectation, harness, lookup, actorId, state);
            }
        }

        private static void AssertContextExpectations(MobaAcceptanceExpectation expectation, MobaSkillConfigTestHarness harness, MobaAcceptanceTraceRecord[] records, MobaAcceptanceContextExpectation[] expectations)
        {
            if (expectations == null) return;

            for (var i = 0; i < expectations.Length; i++)
            {
                var context = expectations[i];
                if (context == null) continue;

                var actorId = ResolveActorId(harness, context.alias, context.actorId);
                AssertContextExpectation(expectation, records, actorId, context);
            }
        }

        private static void AssertStateExpectation(MobaAcceptanceExpectation expectation, MobaSkillConfigTestHarness harness, AbilityKit.Demo.Moba.Services.MobaActorLookupService lookup, int actorId, MobaAcceptanceStateExpectation state)
        {
            var property = NormalizePropertyName(state.property);
            var comparator = NormalizeComparator(state.comparator);
            Assert.IsTrue(actorId > 0, BuildExpectationMessage("state", state.note, state.alias, state.actorId, $"resolved actor id missing for property {property}."));

            global::ActorEntity entity = null;
            if (lookup != null)
            {
                lookup.TryGetActorEntity(actorId, out entity);
            }

            Assert.IsNotNull(entity, BuildExpectationMessage("state", state.note, state.alias, state.actorId, $"actor not found: {actorId}."));

            switch (property)
            {
                case "exists":
                case "present":
                case "bound":
                    AssertComparison(true, true, comparator, BuildExpectationMessage("state", state.note, state.alias, state.actorId, "actor should exist."));
                    return;
                case "actorid":
                case "id":
                    AssertComparison(actorId, state.expectedInt != 0 ? state.expectedInt : ParseExpectedInt(state.expectedValue, actorId), comparator, BuildExpectationMessage("state", state.note, state.alias, state.actorId, "actor id mismatch."));
                    return;
                case "hp":
                    AssertNumeric(GetAttr(entity).Hp, GetExpectedFloat(state.expectedFloat, state.expectedValue), GetTolerance(state), comparator, BuildExpectationMessage("state", state.note, state.alias, state.actorId, "hp mismatch."));
                    return;
                case "mana":
                    AssertNumeric(GetAttr(entity).Mana, GetExpectedFloat(state.expectedFloat, state.expectedValue), GetTolerance(state), comparator, BuildExpectationMessage("state", state.note, state.alias, state.actorId, "mana mismatch."));
                    return;
                case "maxhp":
                    AssertNumeric(GetAttr(entity).MaxHp, GetExpectedFloat(state.expectedFloat, state.expectedValue), GetTolerance(state), comparator, BuildExpectationMessage("state", state.note, state.alias, state.actorId, "max hp mismatch."));
                    return;
                case "maxmana":
                    AssertNumeric(GetAttr(entity).MaxMana, GetExpectedFloat(state.expectedFloat, state.expectedValue), GetTolerance(state), comparator, BuildExpectationMessage("state", state.note, state.alias, state.actorId, "max mana mismatch."));
                    return;
                case "teamid":
                    Assert.IsTrue(entity.hasTeam, BuildExpectationMessage("state", state.note, state.alias, state.actorId, "team component missing."));
                    var teamId = (int)entity.team.Value;
                    AssertComparison(teamId, state.expectedInt != 0 ? state.expectedInt : ParseExpectedInt(state.expectedValue, teamId), comparator, BuildExpectationMessage("state", state.note, state.alias, state.actorId, "team id mismatch."));
                    return;
                case "buff":
                case "hasbuff":
                    AssertComparison(HasBuff(entity, state.expectedInt != 0 ? state.expectedInt : ParseExpectedInt(state.expectedValue, 0)), state.expectedBool, comparator, BuildExpectationMessage("state", state.note, state.alias, state.actorId, "buff presence mismatch."));
                    return;
                case "buffcount":
                    AssertComparison(CountBuffs(entity, state.expectedInt != 0 ? state.expectedInt : ParseExpectedInt(state.expectedValue, 0)), state.expectedInt, comparator, BuildExpectationMessage("state", state.note, state.alias, state.actorId, "buff count mismatch."));
                    return;
                case "position":
                case "transform.position":
                    Assert.IsTrue(entity.hasTransform, BuildExpectationMessage("state", state.note, state.alias, state.actorId, "transform component missing."));
                    AssertVector(entity.transform.Value.Position, state.expectedVector, state.tolerance, comparator, BuildExpectationMessage("state", state.note, state.alias, state.actorId, "position mismatch."));
                    return;
                default:
                    Assert.Fail(BuildExpectationMessage("state", state.note, state.alias, state.actorId, $"unsupported state property '{state.property}'."));
                    return;
            }
        }

        private static void AssertContextExpectation(MobaAcceptanceExpectation expectation, MobaAcceptanceTraceRecord[] records, int actorId, MobaAcceptanceContextExpectation context)
        {
            var property = NormalizePropertyName(context.property);
            var comparator = NormalizeComparator(context.comparator);
            var messageBase = BuildExpectationMessage("context", context.note, context.alias, context.actorId, $"property={property}, kind={context.kind}");

            switch (property)
            {
                case "exists":
                case "present":
                case "bound":
                    Assert.IsTrue(actorId > 0, messageBase + " actor id missing.");
                    return;
                case "sourceactorid":
                    AssertContextTraceValue(records, context.kind, context, actorId, r => (int)r.sourceActorId, comparator, messageBase + " sourceActorId mismatch.");
                    return;
                case "targetactorid":
                    AssertContextTraceValue(records, context.kind, context, actorId, r => (int)r.targetActorId, comparator, messageBase + " targetActorId mismatch.");
                    return;
                case "configid":
                    AssertContextTraceValue(records, context.kind, context, actorId, r => r.configId, comparator, messageBase + " configId mismatch.");
                    return;
                case "rootid":
                    AssertContextTraceValue(records, context.kind, context, actorId, r => (int)r.rootId, comparator, messageBase + " rootId mismatch.");
                    return;
                case "childcount":
                    AssertContextTraceValue(records, context.kind, context, actorId, r => r.childCount, comparator, messageBase + " childCount mismatch.");
                    return;
                default:
                    Assert.Fail(messageBase + $" unsupported context property '{context.property}'.");
                    return;
            }
        }

        private static void AssertContextTraceValue(MobaAcceptanceTraceRecord[] records, string kind, MobaAcceptanceContextExpectation context, int actorId, Func<MobaAcceptanceTraceRecord, int> selector, string comparator, string message)
        {
            var record = FindFirstRecord(records, kind, actorId);
            Assert.IsNotNull(record, BuildExpectationMessage("context", context.note, context.alias, context.actorId, $"missing trace node kind={kind}."));
            AssertComparison(selector(record), context.expectedInt != 0 ? context.expectedInt : ParseExpectedInt(context.expectedValue, selector(record)), comparator, message);
        }

        private static string BuildMissingEffectRootMessage(MobaAcceptanceExpectation expectation, MobaSkillConfigTestHarness harness)
        {
            var effectId = expectation != null && expectation.config != null ? expectation.config.effectId : 0;
            var message = $"Missing effect root trace for effect {effectId}.";
            if (harness == null)
            {
                return message;
            }

            if (TryGetPrimarySkillDiagnostic(expectation, harness, out var diagnostic))
            {
                return message + " " + diagnostic;
            }

            return message + " SkillRuntimeDiagnostic: unavailable.";
        }

        private static bool TryGetPrimarySkillDiagnostic(MobaAcceptanceExpectation expectation, MobaSkillConfigTestHarness harness, out string diagnostic)
        {
            diagnostic = null;
            if (expectation == null || harness == null) return false;

            if (TryGetPrimarySkillActorAndSlot(expectation, harness, out var actorId, out var slot, out var actorAlias))
            {
                diagnostic = $"SkillRuntimeDiagnostic: alias={actorAlias}, {harness.DescribeSkillRuntimeState(actorId, slot)}";
                return true;
            }

            return false;
        }

        private static bool TryGetPrimarySkillActorAndSlot(MobaAcceptanceExpectation expectation, MobaSkillConfigTestHarness harness, out int actorId, out int slot, out string actorAlias)
        {
            actorId = 0;
            slot = 0;
            actorAlias = null;
            if (expectation == null || harness == null) return false;

            if (TryGetPrimaryScenarioSkillStep(expectation, out var step))
            {
                actorAlias = ResolvePrimaryActorAlias(harness, step.actorAlias);
                slot = step.slot;
            }
            else if (expectation.input != null)
            {
                actorAlias = ResolvePrimaryActorAlias(harness, expectation.input.actorAlias);
                slot = expectation.input.slot;
            }

            if (string.IsNullOrEmpty(actorAlias) || slot <= 0) return false;

            actorId = ResolveActorId(harness, actorAlias, null);
            return actorId > 0;
        }

        private static bool TryGetPrimaryScenarioSkillStep(MobaAcceptanceExpectation expectation, out MobaAcceptanceTimelineStepExpectation step)
        {
            step = null;
            var timeline = expectation != null && expectation.scenario != null && expectation.scenario.timeline != null && expectation.scenario.timeline.Length > 0
                ? expectation.scenario.timeline
                : expectation != null ? expectation.timeline : null;
            if (timeline == null) return false;

            MobaAcceptanceTimelineStepExpectation best = null;
            var bestAtMs = int.MaxValue;
            for (var i = 0; i < timeline.Length; i++)
            {
                var candidate = timeline[i];
                if (candidate == null || !IsSkillActionName(candidate.action) || candidate.slot <= 0) continue;
                var atMs = Math.Max(0, candidate.atMs);
                if (best == null || atMs < bestAtMs)
                {
                    best = candidate;
                    bestAtMs = atMs;
                }
            }

            step = best;
            return step != null;
        }

        private static bool IsSkillActionName(string action)
        {
            if (string.IsNullOrEmpty(action)) return false;
            return string.Equals(action, "skill", StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "press", StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "hold", StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "release", StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "cancel", StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "skillpress", StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "skillhold", StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "skillrelease", StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "skillcancel", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolvePrimaryActorAlias(MobaSkillConfigTestHarness harness, string actorAlias)
        {
            if (!string.IsNullOrEmpty(actorAlias)) return actorAlias;
            if (harness != null && harness.ScenarioActors != null)
            {
                for (var i = 0; i < harness.ScenarioActors.Length; i++)
                {
                    var actor = harness.ScenarioActors[i];
                    if (actor == null || string.IsNullOrEmpty(actor.alias)) continue;
                    if (!string.IsNullOrEmpty(actor.playerId)) return actor.alias;
                }
            }

            return null;
        }

        private static MobaAttrs GetAttr(global::ActorEntity entity)
        {
            return new MobaAttrs(entity);
        }

        private static bool HasBuff(global::ActorEntity entity, int buffId)
        {
            return CountBuffs(entity, buffId) > 0;
        }

        private static int CountBuffs(global::ActorEntity entity, int buffId)
        {
            if (entity == null || !entity.hasBuffs || entity.buffs.Active == null) return 0;
            var count = 0;
            for (var i = 0; i < entity.buffs.Active.Count; i++)
            {
                var runtime = entity.buffs.Active[i];
                if (runtime == null) continue;
                if (buffId > 0 && runtime.BuffId != buffId) continue;
                count++;
            }

            return count;
        }

        private static void AssertComparison(int actual, int expected, string comparator, string message)
        {
            switch (comparator)
            {
                case "ne":
                case "neq":
                case "not_eq":
                    Assert.AreNotEqual(expected, actual, message);
                    return;
                case "gt":
                    Assert.Greater(actual, expected, message);
                    return;
                case "gte":
                case "ge":
                    Assert.GreaterOrEqual(actual, expected, message);
                    return;
                case "lt":
                    Assert.Less(actual, expected, message);
                    return;
                case "lte":
                case "le":
                    Assert.LessOrEqual(actual, expected, message);
                    return;
                default:
                    Assert.AreEqual(expected, actual, message);
                    return;
            }
        }

        private static void AssertComparison(bool actual, bool expected, string comparator, string message)
        {
            if (string.Equals(comparator, "ne", StringComparison.OrdinalIgnoreCase) || string.Equals(comparator, "neq", StringComparison.OrdinalIgnoreCase))
            {
                Assert.AreNotEqual(expected, actual, message);
                return;
            }

            Assert.AreEqual(expected, actual, message);
        }

        private static void AssertNumeric(float actual, float expected, float tolerance, string comparator, string message)
        {
            switch (comparator)
            {
                case "ne":
                case "neq":
                case "not_eq":
                    Assert.IsTrue(System.Math.Abs(actual - expected) > tolerance, message + $" actual={actual}, expected={expected}, tolerance={tolerance}");
                    return;
                case "gt":
                    Assert.Greater(actual, expected, message);
                    return;
                case "gte":
                case "ge":
                    Assert.GreaterOrEqual(actual, expected, message);
                    return;
                case "lt":
                    Assert.Less(actual, expected, message);
                    return;
                case "lte":
                case "le":
                    Assert.LessOrEqual(actual, expected, message);
                    return;
                default:
                    Assert.LessOrEqual(System.Math.Abs(actual - expected), tolerance, message + $" actual={actual}, expected={expected}, tolerance={tolerance}");
                    return;
            }
        }

        private static void AssertVector(Vec3 actual, MobaAcceptanceVector3Expectation expected, MobaAcceptanceVector3Expectation tolerance, string comparator, string message)
        {
            var expectedVec = ToVec3(expected);
            var toleranceVec = tolerance == null ? new Vec3(0.01f, 0.01f, 0.01f) : ToVec3(tolerance);
            switch (comparator)
            {
                case "ne":
                case "neq":
                case "not_eq":
                    Assert.IsTrue(System.Math.Abs(actual.X - expectedVec.X) > toleranceVec.X
                        || System.Math.Abs(actual.Y - expectedVec.Y) > toleranceVec.Y
                        || System.Math.Abs(actual.Z - expectedVec.Z) > toleranceVec.Z, message);
                    return;
                default:
                    Assert.LessOrEqual(System.Math.Abs(actual.X - expectedVec.X), toleranceVec.X, message + $" x actual={actual.X}, expected={expectedVec.X}, tolerance={toleranceVec.X}");
                    Assert.LessOrEqual(System.Math.Abs(actual.Y - expectedVec.Y), toleranceVec.Y, message + $" y actual={actual.Y}, expected={expectedVec.Y}, tolerance={toleranceVec.Y}");
                    Assert.LessOrEqual(System.Math.Abs(actual.Z - expectedVec.Z), toleranceVec.Z, message + $" z actual={actual.Z}, expected={expectedVec.Z}, tolerance={toleranceVec.Z}");
                    return;
            }
        }

        private static Vec3 ToVec3(MobaAcceptanceVector3Expectation value)
        {
            return value == null ? Vec3.Zero : new Vec3(value.x, value.y, value.z);
        }

        private static float GetTolerance(MobaAcceptanceStateExpectation state)
        {
            return state != null && state.tolerance != null ? System.Math.Max(System.Math.Max(System.Math.Abs(state.tolerance.x), System.Math.Abs(state.tolerance.y)), System.Math.Abs(state.tolerance.z)) : 0.01f;
        }

        private static float GetExpectedFloat(float expectedFloat, string expectedValue)
        {
            if (!string.IsNullOrEmpty(expectedValue) && float.TryParse(expectedValue, out var parsed)) return parsed;
            return expectedFloat;
        }

        private static int ParseExpectedInt(string expectedValue, int fallback)
        {
            return !string.IsNullOrEmpty(expectedValue) && int.TryParse(expectedValue, out var parsed) ? parsed : fallback;
        }

        private static string NormalizePropertyName(string property)
        {
            return string.IsNullOrEmpty(property) ? string.Empty : property.Trim().Replace(" ", string.Empty).ToLowerInvariant();
        }

        private static string NormalizeComparator(string comparator)
        {
            if (string.IsNullOrEmpty(comparator)) return "eq";
            return comparator.Trim().ToLowerInvariant();
        }

        private static string BuildExpectationMessage(string kind, string note, string alias, string actorId, string detail)
        {
            var prefix = $"{kind} expectation";
            if (!string.IsNullOrEmpty(alias)) prefix += $" alias={alias}";
            if (!string.IsNullOrEmpty(actorId)) prefix += $" actorId={actorId}";
            if (!string.IsNullOrEmpty(note)) prefix += $" note={note}";
            return prefix + ": " + detail;
        }

        private static int ResolveActorId(MobaSkillConfigTestHarness harness, string alias, string actorId)
        {
            if (harness == null) return TryParseActorId(actorId, out var parsed) ? parsed : 0;
            if (!string.IsNullOrEmpty(alias) && harness.TryGetActorId(alias, out var aliasActorId)) return aliasActorId;
            if (!string.IsNullOrEmpty(actorId) && harness.TryGetActorId(actorId, out var resolvedActorId)) return resolvedActorId;
            return TryParseActorId(actorId, out var fallbackActorId) ? fallbackActorId : 0;
        }

        private static bool TryParseActorId(string actorId, out int value)
        {
            return int.TryParse(actorId, out value) && value > 0;
        }

        private static MobaAcceptanceTraceRecord FindFirstRecord(MobaAcceptanceTraceRecord[] records, string kind, int actorId)
        {
            if (records == null) return null;
            for (var i = 0; i < records.Length; i++)
            {
                var record = records[i];
                if (!KindEquals(record.kind, kind)) continue;
                if (actorId > 0 && (record.sourceActorId != actorId && record.targetActorId != actorId)) continue;
                return record;
            }

            return null;
        }

        private static bool HasRelationship(MobaAcceptanceTraceRecord[] records, MobaAcceptanceRelationshipExpectation relationship, out string message)
        {
            message = string.Empty;
            if (records == null) return false;
            if (relationship == null) return false;

            for (var i = 0; i < records.Length; i++)
            {
                var parent = records[i];
                if (!KindEquals(parent.kind, relationship.parentKind) || parent.configId != relationship.parentConfigId) continue;

                for (var j = 0; j < records.Length; j++)
                {
                    var child = records[j];
                    if (!KindEquals(child.kind, relationship.childKind) || child.configId != relationship.childConfigId) continue;
                    if (child.rootId != parent.rootId) continue;

                    message = string.Empty;
                    return true;
                }
            }

            message = $"Missing relationship: parent={relationship.parentKind}({relationship.parentConfigId}) -> child={relationship.childKind}({relationship.childConfigId}).";
            return false;
        }

        private static bool Contains(MobaAcceptanceTraceRecord[] records, string kind, int configId, long requiredRootId)
        {
            return Count(records, kind, configId, requiredRootId) > 0;
        }

        private static int Count(MobaAcceptanceTraceRecord[] records, string kind, int configId, long requiredRootId)
        {
            if (records == null) return 0;

            var count = 0;
            for (var i = 0; i < records.Length; i++)
            {
                var record = records[i];
                if (!KindEquals(record.kind, kind)) continue;
                if (record.configId != configId) continue;
                if (requiredRootId > 0 && record.rootId != requiredRootId) continue;
                count++;
            }

            return count;
        }

        private static long FindFirstTraceRootId(MobaAcceptanceTraceRecord[] records, string kind, int configId)
        {
            if (records == null) return 0;

            for (var i = 0; i < records.Length; i++)
            {
                var record = records[i];
                if (!KindEquals(record.kind, kind)) continue;
                if (record.configId != configId) continue;
                return record.rootId;
            }

            return 0;
        }

        private static bool KindEquals(string left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }
    }
}
