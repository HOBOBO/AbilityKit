using System;

namespace AbilityKit.Game.Test.UnitTest
{
    [Serializable]
    public sealed class MobaAcceptanceExpectation
    {
        public string caseId;
        public string description;
        public string worldId;
        public int tickRate;
        public bool accelerated;
        public string category;
        public string[] tags;
        public string generatedFrom;
        public string lastReviewedAt;
        public MobaAcceptanceInputExpectation input;
        public MobaAcceptanceConfigExpectation config;
        public MobaAcceptanceScenarioExpectation scenario;
        public MobaAcceptanceActorExpectation[] actors;
        public MobaAcceptanceSetupActionExpectation[] setupActions;
        public MobaAcceptanceTimelineStepExpectation[] timeline;
        public MobaAcceptanceStateExpectation[] stateExpectations;
        public MobaAcceptanceContextExpectation[] contextExpectations;
        public MobaAcceptanceTraceExpectation[] mustContain;
        public MobaAcceptanceTraceExpectation[] mustNotContain;
        public MobaAcceptanceRelationshipExpectation[] relationships;
    }

    [Serializable]
    public sealed class MobaAcceptanceScenarioExpectation
    {
        public string scenarioId;
        public string name;
        public string description;
        public string worldId;
        public int tickRate;
        public bool accelerated;
        public string category;
        public string[] tags;
        public MobaAcceptanceActorExpectation[] actors;
        public MobaAcceptanceSetupActionExpectation[] setupActions;
        public MobaAcceptanceTimelineStepExpectation[] timeline;
        public MobaAcceptanceStateExpectation[] stateExpectations;
        public MobaAcceptanceContextExpectation[] contextExpectations;
    }

    [Serializable]
    public sealed class MobaAcceptanceActorExpectation
    {
        public string alias;
        public string actorId;
        public string playerId;
        public int teamId;
        public int heroId;
        public int attributeTemplateId;
        public int level;
        public int basicAttackSkillId;
        public int[] skillIds;
        public int spawnIndex;
        public int unitSubType;
        public int mainType;
        public bool hasSpawnPosition;
        public MobaAcceptanceVector3Expectation spawnPosition;
        public MobaAcceptanceVector3Expectation facingDirection;
        public int[] carriedSkillIds;
        public string configKey;
    }

    [Serializable]
    public sealed class MobaAcceptanceSetupActionExpectation
    {
        public string action;
        public string alias;
        public string actorAlias;
        public string targetAlias;
        public string sourceAlias;
        public string playerId;
        public string actorId;
        public int sourceActorId;
        public int slot;
        public int skillId;
        public int targetActorId;
        public int durationMs;
        public bool enabled;
        public int teamId;
        public int heroId;
        public int attributeTemplateId;
        public int level;
        public int unitSubType;
        public int mainType;
        public string kind;
        public string sourceKind;
        public int sourceId;
        public int ownerActorId;
        public string property;
        public float value;
        public int intValue;
        public int buffId;
        public int durationOverrideMs;
        public bool removeAll;
        public MobaAcceptanceVector3Expectation position;
        public MobaAcceptanceVector3Expectation direction;
        public string payload;
        public string note;
    }

    [Serializable]
    public sealed class MobaAcceptanceTimelineStepExpectation
    {
        public string stepId;
        public int atMs;
        public string action;
        public string actorAlias;
        public string targetAlias;
        public string playerId;
        public int slot;
        public int skillId;
        public int targetActorId;
        public int durationMs;
        public string property;
        public float value;
        public int intValue;
        public MobaAcceptanceVector3Expectation position;
        public MobaAcceptanceVector3Expectation direction;
        public string payload;
        public string note;
    }

    [Serializable]
    public sealed class MobaAcceptanceStateExpectation
    {
        public string alias;
        public string actorId;
        public string state;
        public string property;
        public string comparator;
        public string expectedValue;
        public float expectedFloat;
        public int expectedInt;
        public bool expectedBool;
        public MobaAcceptanceVector3Expectation expectedVector;
        public MobaAcceptanceVector3Expectation tolerance;
        public string note;
    }

    [Serializable]
    public sealed class MobaAcceptanceContextExpectation
    {
        public string alias;
        public string actorId;
        public string kind;
        public string property;
        public string comparator;
        public string expectedValue;
        public float expectedFloat;
        public int expectedInt;
        public bool expectedBool;
        public string note;
    }

    [Serializable]
    public sealed class MobaAcceptanceVector3Expectation
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public sealed class MobaAcceptanceInputExpectation
    {
        public string playerId;
        public string actorAlias;
        public string targetAlias;
        public int slot;
        public string phase;
        public MobaAcceptanceVector3Expectation position;
        public MobaAcceptanceVector3Expectation direction;
        public int targetActorId;
        public string note;
    }

    [Serializable]
    public sealed class MobaAcceptanceConfigExpectation
    {
        public int skillId;
        public int castFlowId;
        public int effectId;
        public int triggerId;
        public int[] effectIds;
        public int[] triggerIds;
        public MobaAcceptanceEffectExpectation[] effects;
        public MobaAcceptanceActionExpectation[] expectedActions;
        public MobaAcceptanceProjectileExpectation expectedProjectile;
    }

    [Serializable]
    public sealed class MobaAcceptanceEffectExpectation
    {
        public int effectId;
        public int triggerId;
        public MobaAcceptanceActionExpectation[] expectedActions;
        public MobaAcceptanceProjectileExpectation expectedProjectile;
    }

    [Serializable]
    public sealed class MobaAcceptanceActionExpectation
    {
        public int actionId;
        public string type;
    }

    [Serializable]
    public sealed class MobaAcceptanceProjectileExpectation
    {
        public int launcherId;
        public int projectileId;
    }

    [Serializable]
    public sealed class MobaAcceptanceTraceExpectation
    {
        public string kind;
        public int configId;
        public int underEffectId;
        public int minCount;
        public int maxCount;
    }

    [Serializable]
    public sealed class MobaAcceptanceRelationshipExpectation
    {
        public string parentKind;
        public int parentConfigId;
        public string childKind;
        public int childConfigId;
    }

    [Serializable]
    public sealed class MobaAcceptanceTraceRecord
    {
        public string caseId;
        public int frame;
        public int timeMs;
        public long rootId;
        public long parentId;
        public long nodeId;
        public string kind;
        public int kindValue;
        public int configId;
        public long sourceActorId;
        public long targetActorId;
        public long sourceId;
        public long targetId;
        public long originSourceId;
        public long originTargetId;
        public string originSource;
        public string originTarget;
        public bool isRoot;
        public bool isEnded;
        public int endedFrame;
        public int endReason;
        public int childCount;
        public string displayName;
        public string configLabel;
        public string runtimeLabel;
        public string actorLabel;
        public string sourceActorLabel;
        public string targetActorLabel;
        public string configSource;
        public string semanticVersion;
    }

    /// <summary>
    /// Controls an acceptance test run and its diagnostic artifacts.
    /// Machine-readable artifacts preserve the existing json/jsonl contract; the optional
    /// trace text is intended for direct inspection after individual or batch test runs.
    /// </summary>
    public sealed class MobaAcceptanceRunOptions
    {
        public string ArtifactDirectory { get; set; }
        public bool ExportArtifacts { get; set; } = true;
        public MobaAcceptanceTraceExportOptions TraceExport { get; set; }
        public bool Recursive { get; set; } = true;
        public string CategoryFilter { get; set; }
        public string TagFilter { get; set; }

        public bool ShouldExportTraceText => TraceExport != null && TraceExport.DetailedText;
    }

    public sealed class MobaAcceptanceTraceExportOptions
    {
        public bool DetailedText { get; set; } = true;
    }

    [Serializable]
    public sealed class MobaAcceptanceSummary
    {
        public string caseId;
        public string worldId;
        public string expectationPath;
        public int tickRate;
        public bool accelerated;
        public string category;
        public string[] tags;
        public string generatedFrom;
        public string lastReviewedAt;
        public MobaAcceptanceScenarioExpectation scenario;
        public MobaAcceptanceActorExpectation[] actors;
        public MobaAcceptanceSetupActionExpectation[] setupActions;
        public MobaAcceptanceTimelineStepExpectation[] timeline;
        public MobaAcceptanceStateExpectation[] stateExpectations;
        public MobaAcceptanceContextExpectation[] contextExpectations;
        public MobaAcceptanceInputExpectation input;
        public MobaAcceptanceConfigExpectation config;
        public MobaAcceptanceResult result;
        public MobaAcceptanceCoverageSummary coverage;
        public MobaAcceptanceTraceCount[] traceCounts;
        public MobaAcceptanceTraceDictionaryEntry[] traceDictionary;
        public string traceDictionaryVersion;
        public MobaAcceptanceDiagnosticsSummary diagnostics;
        public string traceJsonlPath;
        public string traceTextPath;
        public string summaryJsonPath;
    }

    [Serializable]
    public sealed class MobaAcceptanceTraceDictionaryEntry
    {
        public string key;
        public string kind;
        public string id;
        public string name;
        public string label;
        public string source;
        public string sourceVersion;
    }

    [Serializable]
    public sealed class MobaAcceptanceResult
    {
        public bool passed;
        public bool skillCastTraceFound;
        public bool effectExecutionTraceFound;
        public bool allExpectedActionsExecuted;
        public bool projectileLaunched;
        public bool areaSpawned;
        public bool buffApplied;
        public long effectRootId;
        public int finalFrame;
        public int finalTimeMs;
        public int traceNodeCount;
        public int expectedTraceNodeCount;
        public int matchedExpectedTraceNodeCount;
        public int missingExpectedTraceNodeCount;
        public int expectedActionCount;
        public int executedExpectedActionCount;
        public int expectedRelationshipCount;
        public int satisfiedRelationshipCount;
        public int expectedStateCount;
        public int satisfiedStateCount;
        public int expectedContextCount;
        public int satisfiedContextCount;
    }

    [Serializable]
    public sealed class MobaAcceptanceCoverageSummary
    {
        public int expectedTraceNodeCount;
        public int matchedExpectedTraceNodeCount;
        public int missingExpectedTraceNodeCount;
        public int forbiddenTraceNodeCount;
        public int unexpectedForbiddenTraceNodeCount;
        public int expectedActionCount;
        public int executedExpectedActionCount;
        public int expectedRelationshipCount;
        public int satisfiedRelationshipCount;
        public int expectedStateCount;
        public int satisfiedStateCount;
        public int expectedContextCount;
        public int satisfiedContextCount;
        public bool allRequiredTraceNodesMatched;
        public bool allForbiddenTraceNodesAbsent;
        public bool allExpectedActionsExecuted;
        public bool allRelationshipsSatisfied;
        public bool allStateExpectationsSatisfied;
        public bool allContextExpectationsSatisfied;
        public string missingTraceNodes;
        public string unexpectedTraceNodes;
        public string missingActions;
        public string missingRelationships;
        public string missingStates;
        public string missingContexts;
    }

    [Serializable]
    public sealed class MobaAcceptanceTraceCount
    {
        public string kind;
        public int count;
    }

    [Serializable]
    public sealed class MobaAcceptanceDiagnosticsSummary
    {
        public int warningCount;
        public MobaAcceptanceDiagnosticWarning[] warnings;
        public string planActionRejections;
        public string triggerRuntimeSnapshot;
    }

    [Serializable]
    public sealed class MobaAcceptanceDiagnosticWarning
    {
        public string key;
        public string message;
        public int count;
        public bool suppressedAtLimit;
    }

    [Serializable]
    public sealed class MobaAcceptanceCaseRunResult
    {
        public string caseId;
        public string expectationPath;
        public bool passed;
        public string errorType;
        public string errorMessage;
        public string startedUtc;
        public long durationMs;
        public MobaAcceptanceSummary summary;
    }

    [Serializable]
    public sealed class MobaAcceptanceBatchSummary
    {
        public string expectationDirectory;
        public string artifactDirectory;
        public string categoryFilter;
        public string tagFilter;
        public bool recursive;
        public string startedUtc;
        public long durationMs;
        public int total;
        public int passed;
        public int failed;
        public bool allPassed;
        public MobaAcceptanceCaseRunResult[] results;
        public string batchSummaryJsonPath;
    }
}
