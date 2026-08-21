using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Triggering.Validation;
using NUnit.Framework;

namespace AbilityKit.Triggering.Tests
{
    public sealed class ValidatingTriggerPlanJsonDatabaseExecutionRootTests
    {
        [Test]
        public void Validate_RejectsTimelineActionInsideJsonExecutionRoot()
        {
            var triggerId = 4101;
            var actionId = StableStringId.Get("test:validating_json_execution_root:timeline_action");
            var json = $@"
{{
  ""FormatVersion"": 1,
  ""Triggers"": [
    {{
      ""TriggerId"": {triggerId},
      ""EventName"": ""test:validating_json_execution_root:event:timeline_action"",
      ""ExecutionRoot"": {{
        ""Kind"": ""Action"",
        ""Action"": {{
          ""ActionId"": {actionId},
          ""ScheduleMode"": ""Timeline"",
          ""MaxExecutions"": -1
        }}
      }}
    }}
  ]
}}";
            var database = new ValidatingTriggerPlanJsonDatabase();

            database.LoadFromJson(json, "json-execution-root-timeline-test");
            var result = database.Validate();

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Matches<ValidationIssue>(issue => issue.Code == ValidationErrorCodes.UNSUPPORTED_ACTION_SCHEDULE));
        }

        [Test]
        public void Validate_WarnsForEmptyCompositeJsonExecutionRoot()
        {
            var triggerId = 4102;
            var json = $@"
{{
  ""FormatVersion"": 1,
  ""Triggers"": [
    {{
      ""TriggerId"": {triggerId},
      ""EventName"": ""test:validating_json_execution_root:event:empty_sequence"",
      ""ExecutionRoot"": {{
        ""Kind"": ""Sequence"",
        ""Children"": []
      }}
    }}
  ]
}}";
            var database = new ValidatingTriggerPlanJsonDatabase();

            database.LoadFromJson(json, "json-execution-root-empty-sequence-test");
            var result = database.Validate();

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Warnings, Has.Some.Matches<ValidationIssue>(issue => issue.Code == ValidationErrorCodes.EMPTY_EXECUTION_NODE));
        }
        [Test]
        public void LoadFromJson_AcceptsSourceTriggerArrayRoot()
        {
            var actionId = StableStringId.Get("test:array_source_root:debug");
            var json = $@"
[
  {{
    ""id"": 4201,
    ""event"": ""test:array_source_root:event"",
    ""enabled"": true,
    ""actions"": [
      {{ ""type"": ""debug_log"", ""id"": {actionId} }}
    ]
  }}
]";
            var database = new ValidatingTriggerPlanJsonDatabase();

            database.LoadFromJson(json, "array-source-root-test");

            Assert.That(database.InnerDatabase.TryGetPlanByTriggerId(4201, out var plan), Is.True);
            Assert.That(plan.Actions, Has.Length.EqualTo(1));
        }

        [Test]
        public void LoadFromJson_ResolvesTemplateBindingsInFallbackExecutionRoot()
        {
            var actionId = StableStringId.Get("test:template_execution_root:debug");
            var json = $@"
{{
  ""FormatVersion"": 1,
  ""Triggers"": [
    {{
      ""TriggerId"": 4202,
      ""EventName"": ""test:template_execution_root:event"",
      ""Template"": {{
        ""TemplateId"": ""template.debug"",
        ""Bindings"": {{
          ""message"": {{ ""Kind"": ""Const"", ""ConstValue"": 7.0 }}
        }}
      }},
      ""Actions"": [
        {{
          ""ActionId"": {actionId},
          ""Arity"": 1,
          ""Args"": {{
            ""message"": {{ ""Kind"": ""TemplateParam"", ""Key"": ""message"" }}
          }}
        }}
      ]
    }}
  ]
}}";
            var database = new TriggerPlanJsonDatabase();

            Assert.DoesNotThrow(() => database.LoadFromJson(json, "template-execution-root-test"));
            Assert.That(database.TryGetExecutionRootByTriggerId(4202, out var root), Is.True);
            Assert.That(root, Is.Not.Null);
        }

        [Test]
        public void LoadFromJson_InitializesDeclaredBlackboards()
        {
            var boardId = BlackboardIdMapper.BoardId("skill");
            var keyId = BlackboardIdMapper.KeyId("skill.hitCount");
            var json = $@"
{{
  ""FormatVersion"": 1,
  ""Triggers"": [],
  ""Blackboards"": [
    {{
      ""BoardId"": {boardId},
      ""Name"": ""skill"",
      ""Scope"": ""global"",
      ""Keys"": [
        {{ ""KeyId"": {keyId}, ""Name"": ""skill.hitCount"", ""Type"": ""Int"", ""IntValue"": 4 }}
      ]
    }}
  ]
}}";
            var database = new TriggerPlanJsonDatabase();
            database.LoadFromJson(json, "blackboard-initialization-test");
            var resolver = new DictionaryBlackboardResolver();

            database.InitializeBlackboards(resolver);

            Assert.That(database.Blackboards, Has.Count.EqualTo(1));
            Assert.That(resolver.TryResolve(boardId, out var board), Is.True);
            Assert.That(board.TryGetInt(keyId, out var value), Is.True);
            Assert.That(value, Is.EqualTo(4));
        }

        [Test]
        public void OwnerBlackboardStore_IsolatesOwnersAndFallsBackToGlobal()
        {
            var localBoardId = BlackboardIdMapper.BoardId("local.module:test");
            var localKeyId = BlackboardIdMapper.KeyId("hitCount");
            var globalBoardId = BlackboardIdMapper.BoardId("global");
            var globalKeyId = BlackboardIdMapper.KeyId("match.round");
            var globalResolver = new DictionaryBlackboardResolver();
            var globalBoard = new DictionaryBlackboard();
            globalBoard.SetInt(globalKeyId, 7);
            globalResolver.Register(globalBoardId, globalBoard);
            var store = new OwnerBlackboardStore(globalResolver);
            store.Configure(new[]
            {
                new BlackboardInitializationPlan
                {
                    BoardId = localBoardId,
                    Scope = BlackboardInitializationScopes.Owner,
                    Keys = new System.Collections.Generic.List<BlackboardInitializationKey>
                    {
                        new BlackboardInitializationKey
                        {
                            KeyId = localKeyId,
                            Type = BlackboardKeyType.Int,
                            IntValue = 3
                        }
                    }
                }
            });

            var first = store.GetOrCreate(101);
            var second = store.GetOrCreate(202);
            Assert.That(first.TryResolve(localBoardId, out var firstLocal), Is.True);
            Assert.That(second.TryResolve(localBoardId, out var secondLocal), Is.True);
            firstLocal.SetInt(localKeyId, 11);

            Assert.That(firstLocal.TryGetInt(localKeyId, out var firstValue), Is.True);
            Assert.That(secondLocal.TryGetInt(localKeyId, out var secondValue), Is.True);
            Assert.That(firstValue, Is.EqualTo(11));
            Assert.That(secondValue, Is.EqualTo(3));
            Assert.That(first.TryResolve(globalBoardId, out var firstGlobal), Is.True);
            Assert.That(second.TryResolve(globalBoardId, out var secondGlobal), Is.True);
            Assert.That(firstGlobal, Is.SameAs(globalBoard));
            Assert.That(secondGlobal, Is.SameAs(globalBoard));
        }

        [Test]
        public void OwnerBlackboardStore_ReleaseAndRecreateRestoresDefaults()
        {
            var boardId = BlackboardIdMapper.BoardId("local.trigger:test:1");
            var keyId = BlackboardIdMapper.KeyId("count");
            var store = new OwnerBlackboardStore();
            store.Configure(new[]
            {
                new BlackboardInitializationPlan
                {
                    BoardId = boardId,
                    Scope = BlackboardInitializationScopes.Owner,
                    Keys = new System.Collections.Generic.List<BlackboardInitializationKey>
                    {
                        new BlackboardInitializationKey { KeyId = keyId, Type = BlackboardKeyType.Int, IntValue = 2 }
                    }
                }
            });

            var first = store.GetOrCreate(9);
            first.TryResolve(boardId, out var firstBoard);
            firstBoard.SetInt(keyId, 99);
            Assert.That(store.Release(9), Is.True);

            var recreated = store.GetOrCreate(9);
            Assert.That(recreated, Is.Not.SameAs(first));
            Assert.That(recreated.TryResolve(boardId, out var recreatedBoard), Is.True);
            Assert.That(recreatedBoard.TryGetInt(keyId, out var value), Is.True);
            Assert.That(value, Is.EqualTo(2));
        }

        [Test]
        public void BlackboardMutation_SetAndAdd_PreserveDeclaredNumericType()
        {
            var boardId = BlackboardIdMapper.BoardId("local.module:test");
            var keyId = BlackboardIdMapper.KeyId("boost");
            var resolver = new DictionaryBlackboardResolver();
            BlackboardInitialization.Apply(new[]
            {
                new BlackboardInitializationPlan
                {
                    BoardId = boardId,
                    Keys = new System.Collections.Generic.List<BlackboardInitializationKey>
                    {
                        new BlackboardInitializationKey
                        {
                            KeyId = keyId,
                            Type = BlackboardKeyType.Double,
                            DoubleValue = 1.5,
                            CanWrite = true
                        }
                    }
                }
            }, resolver);
            var target = new BlackboardWriteTarget(boardId, keyId, BlackboardKeyType.Double, "owner");

            Assert.That(BlackboardMutation.TrySetNumeric(resolver, in target, 4, out var setError), Is.True, setError);
            Assert.That(BlackboardMutation.TryAddNumeric(resolver, in target, 2.5, out var addError), Is.True, addError);
            Assert.That(resolver.TryResolve(boardId, out var board), Is.True);
            Assert.That(board.TryGetDouble(keyId, out var value), Is.True);
            Assert.That(value, Is.EqualTo(6.5));
        }

        [Test]
        public void BlackboardMutation_RejectsReadOnlyAndTypeMismatch()
        {
            var boardId = BlackboardIdMapper.BoardId("global");
            var keyId = BlackboardIdMapper.KeyId("match.score");
            var writableKeyId = BlackboardIdMapper.KeyId("match.multiplier");
            var resolver = new DictionaryBlackboardResolver();
            BlackboardInitialization.Apply(new[]
            {
                new BlackboardInitializationPlan
                {
                    BoardId = boardId,
                    Keys = new System.Collections.Generic.List<BlackboardInitializationKey>
                    {
                        new BlackboardInitializationKey
                        {
                            KeyId = keyId,
                            Type = BlackboardKeyType.Int,
                            IntValue = 3,
                            CanWrite = false
                        },
                        new BlackboardInitializationKey
                        {
                            KeyId = writableKeyId,
                            Type = BlackboardKeyType.Int,
                            IntValue = 1,
                            CanWrite = true
                        }
                    }
                }
            }, resolver);
            var readOnly = new BlackboardWriteTarget(boardId, keyId, BlackboardKeyType.Int, "global");
            var wrongType = new BlackboardWriteTarget(boardId, writableKeyId, BlackboardKeyType.Double, "global");

            Assert.That(BlackboardMutation.TrySetNumeric(resolver, in readOnly, 8, out var readOnlyError), Is.False);
            StringAssert.Contains("read-only", readOnlyError);
            Assert.That(BlackboardMutation.TrySetNumeric(resolver, in wrongType, 8, out var typeError), Is.False);
            StringAssert.Contains("type mismatch", typeError);
        }

        [Test]
        public void BlackboardMutation_SetsBooleanAndStringValuesWithDeclaredTypes()
        {
            var boardId = BlackboardIdMapper.BoardId("local.module:typed");
            var boolKeyId = BlackboardIdMapper.KeyId("enabled");
            var stringKeyId = BlackboardIdMapper.KeyId("state");
            var resolver = new DictionaryBlackboardResolver();
            BlackboardInitialization.Apply(new[]
            {
                new BlackboardInitializationPlan
                {
                    BoardId = boardId,
                    Keys = new System.Collections.Generic.List<BlackboardInitializationKey>
                    {
                        new BlackboardInitializationKey { KeyId = boolKeyId, Type = BlackboardKeyType.Bool, CanWrite = true },
                        new BlackboardInitializationKey { KeyId = stringKeyId, Type = BlackboardKeyType.String, CanWrite = true }
                    }
                }
            }, resolver);
            var boolTarget = new BlackboardWriteTarget(boardId, boolKeyId, BlackboardKeyType.Bool, "owner");
            var stringTarget = new BlackboardWriteTarget(boardId, stringKeyId, BlackboardKeyType.String, "owner");
            var boolValue = ActionArgValue.OfBool(true, "value");
            var stringValue = ActionArgValue.OfString("armed", "value");

            Assert.That(BlackboardMutation.TrySetValue(resolver, in boolTarget, in boolValue, out var boolError), Is.True, boolError);
            Assert.That(BlackboardMutation.TrySetValue(resolver, in stringTarget, in stringValue, out var stringError), Is.True, stringError);
            Assert.That(resolver.TryResolve(boardId, out var board), Is.True);
            Assert.That(board.TryGetBool(boolKeyId, out var boolResult), Is.True);
            Assert.That(boolResult, Is.True);
            Assert.That(board.TryGetString(stringKeyId, out var stringResult), Is.True);
            Assert.That(stringResult, Is.EqualTo("armed"));

            Assert.That(BlackboardMutation.TrySetValue(resolver, in boolTarget, in stringValue, out var mismatchError), Is.False);
            StringAssert.Contains("type mismatch", mismatchError);
        }

        [Test]
        public void OwnerBlackboardStore_SnapshotRoundTripsTypedValuesAndJson()
        {
            var boardId = BlackboardIdMapper.BoardId("local.snapshot");
            var intKeyId = BlackboardIdMapper.KeyId("count");
            var boolKeyId = BlackboardIdMapper.KeyId("enabled");
            var floatKeyId = BlackboardIdMapper.KeyId("ratio");
            var doubleKeyId = BlackboardIdMapper.KeyId("score");
            var stringKeyId = BlackboardIdMapper.KeyId("state");
            var store = new OwnerBlackboardStore();
            store.Configure(new[]
            {
                new BlackboardInitializationPlan
                {
                    BoardId = boardId,
                    Scope = BlackboardInitializationScopes.Owner,
                    Keys = new System.Collections.Generic.List<BlackboardInitializationKey>
                    {
                        new BlackboardInitializationKey { KeyId = intKeyId, Type = BlackboardKeyType.Int, IntValue = 1 },
                        new BlackboardInitializationKey { KeyId = boolKeyId, Type = BlackboardKeyType.Bool, BoolValue = false },
                        new BlackboardInitializationKey { KeyId = floatKeyId, Type = BlackboardKeyType.Float, FloatValue = 1.5f },
                        new BlackboardInitializationKey { KeyId = doubleKeyId, Type = BlackboardKeyType.Double, DoubleValue = 2.5d },
                        new BlackboardInitializationKey { KeyId = stringKeyId, Type = BlackboardKeyType.String, StringValue = "idle" }
                    }
                }
            });

            var resolver = store.GetOrCreate(77);
            Assert.That(resolver.TryResolve(boardId, out var board), Is.True);
            board.SetInt(intKeyId, 9);
            board.SetBool(boolKeyId, true);
            board.SetFloat(floatKeyId, 3.25f);
            board.SetDouble(doubleKeyId, 6.5d);
            board.SetString(stringKeyId, "armed");

            Assert.That(store.TryCaptureSnapshot(77, out var snapshot, out var captureError), Is.True, captureError);
            var json = snapshot.ToJson();
            var restoredSnapshot = BlackboardSnapshot.FromJson(json);
            board.SetInt(intKeyId, -1);
            board.SetBool(boolKeyId, false);
            board.SetFloat(floatKeyId, -1f);
            board.SetDouble(doubleKeyId, -1d);
            board.SetString(stringKeyId, "broken");

            Assert.That(store.TryRestoreSnapshot(77, restoredSnapshot, out var restoreError), Is.True, restoreError);
            Assert.That(board.TryGetInt(intKeyId, out var intValue) && intValue == 9, Is.True);
            Assert.That(board.TryGetBool(boolKeyId, out var boolValue) && boolValue, Is.True);
            Assert.That(board.TryGetFloat(floatKeyId, out var floatValue) && floatValue == 3.25f, Is.True);
            Assert.That(board.TryGetDouble(doubleKeyId, out var doubleValue) && doubleValue == 6.5d, Is.True);
            Assert.That(board.TryGetString(stringKeyId, out var stringValue) && stringValue == "armed", Is.True);
        }

        [Test]
        public void OwnerBlackboardStore_SnapshotExcludesGlobalFallbackAndRequiresExistingOwner()
        {
            var localBoardId = BlackboardIdMapper.BoardId("local.snapshot.scope");
            var localKeyId = BlackboardIdMapper.KeyId("local");
            var globalBoardId = BlackboardIdMapper.BoardId("global.snapshot.scope");
            var globalKeyId = BlackboardIdMapper.KeyId("global");
            var globalResolver = new DictionaryBlackboardResolver();
            var global = new DictionaryBlackboard();
            global.DefineKey(globalKeyId, BlackboardKeyType.Int);
            global.SetInt(globalKeyId, 42);
            globalResolver.Register(globalBoardId, global);
            var store = new OwnerBlackboardStore(globalResolver);
            store.Configure(new[]
            {
                new BlackboardInitializationPlan
                {
                    BoardId = localBoardId,
                    Scope = BlackboardInitializationScopes.Owner,
                    Keys = new System.Collections.Generic.List<BlackboardInitializationKey>
                    {
                        new BlackboardInitializationKey { KeyId = localKeyId, Type = BlackboardKeyType.Int, IntValue = 1 }
                    }
                }
            });

            var ownerResolver = store.GetOrCreate(88);
            Assert.That(store.TryCaptureSnapshot(88, out var snapshot, out var captureError), Is.True, captureError);
            Assert.That(snapshot.Boards, Has.Count.EqualTo(1));
            Assert.That(snapshot.Boards[0].BoardId, Is.EqualTo(localBoardId));

            Assert.That(store.Release(88), Is.True);
            Assert.That(store.TryRestoreSnapshot(88, snapshot, out var missingOwnerError), Is.False);
            StringAssert.Contains("resolver was not found", missingOwnerError);
            Assert.That(ownerResolver.TryResolve(globalBoardId, out var fallback), Is.True);
            Assert.That(fallback, Is.SameAs(global));
        }

        [Test]
        public void OwnerBlackboardStore_SnapshotRejectsVersionOrSchemaMismatchWithoutChangingState()
        {
            var boardId = BlackboardIdMapper.BoardId("local.snapshot.validation");
            var keyId = BlackboardIdMapper.KeyId("value");
            var store = new OwnerBlackboardStore();
            store.Configure(new[]
            {
                new BlackboardInitializationPlan
                {
                    BoardId = boardId,
                    Scope = BlackboardInitializationScopes.Owner,
                    Keys = new System.Collections.Generic.List<BlackboardInitializationKey>
                    {
                        new BlackboardInitializationKey { KeyId = keyId, Type = BlackboardKeyType.Int, IntValue = 5 }
                    }
                }
            });
            var resolver = store.GetOrCreate(99);
            Assert.That(resolver.TryResolve(boardId, out var board), Is.True);
            board.SetInt(keyId, 7);
            Assert.That(store.TryCaptureSnapshot(99, out var snapshot, out _), Is.True);

            snapshot.Version = BlackboardSnapshot.CurrentVersion + 1;
            Assert.That(store.TryRestoreSnapshot(99, snapshot, out var versionError), Is.False);
            StringAssert.Contains("Unsupported Blackboard snapshot version", versionError);
            Assert.That(board.TryGetInt(keyId, out var afterVersion) && afterVersion == 7, Is.True);

            snapshot.Version = BlackboardSnapshot.CurrentVersion;
            var mismatchedEntry = snapshot.Boards[0].Entries[0];
            mismatchedEntry.Type = BlackboardKeyType.String;
            snapshot.Boards[0].Entries[0] = mismatchedEntry;
            Assert.That(store.TryRestoreSnapshot(99, snapshot, out var schemaError), Is.False);
            StringAssert.Contains("type mismatch", schemaError);
            Assert.That(board.TryGetInt(keyId, out var afterSchema) && afterSchema == 7, Is.True);
        }

        [Test]
        public void OwnerBlackboardStore_ValidatesEveryBoardBeforeRestoringAnyBoard()
        {
            var firstBoardId = BlackboardIdMapper.BoardId("local.snapshot.atomic.first");
            var secondBoardId = BlackboardIdMapper.BoardId("local.snapshot.atomic.second");
            var firstKeyId = BlackboardIdMapper.KeyId("first");
            var secondKeyId = BlackboardIdMapper.KeyId("second");
            var store = new OwnerBlackboardStore();
            store.Configure(new[]
            {
                new BlackboardInitializationPlan
                {
                    BoardId = firstBoardId,
                    Scope = BlackboardInitializationScopes.Owner,
                    Keys = new System.Collections.Generic.List<BlackboardInitializationKey>
                    {
                        new BlackboardInitializationKey { KeyId = firstKeyId, Type = BlackboardKeyType.Int, IntValue = 1 }
                    }
                },
                new BlackboardInitializationPlan
                {
                    BoardId = secondBoardId,
                    Scope = BlackboardInitializationScopes.Owner,
                    Keys = new System.Collections.Generic.List<BlackboardInitializationKey>
                    {
                        new BlackboardInitializationKey { KeyId = secondKeyId, Type = BlackboardKeyType.Int, IntValue = 2 }
                    }
                }
            });
            var resolver = store.GetOrCreate(111);
            Assert.That(resolver.TryResolve(firstBoardId, out var firstBoard), Is.True);
            Assert.That(resolver.TryResolve(secondBoardId, out var secondBoard), Is.True);
            firstBoard.SetInt(firstKeyId, 10);
            secondBoard.SetInt(secondKeyId, 20);
            Assert.That(store.TryCaptureSnapshot(111, out var snapshot, out _), Is.True);
            firstBoard.SetInt(firstKeyId, 100);
            secondBoard.SetInt(secondKeyId, 200);

            var invalidEntry = snapshot.Boards.Find(board => board.BoardId == secondBoardId).Entries[0];
            invalidEntry.Type = BlackboardKeyType.String;
            snapshot.Boards.Find(board => board.BoardId == secondBoardId).Entries[0] = invalidEntry;

            Assert.That(store.TryRestoreSnapshot(111, snapshot, out var error), Is.False);
            StringAssert.Contains("type mismatch", error);
            Assert.That(firstBoard.TryGetInt(firstKeyId, out var firstValue) && firstValue == 100, Is.True);
            Assert.That(secondBoard.TryGetInt(secondKeyId, out var secondValue) && secondValue == 200, Is.True);
        }

        [Test]
        public void NumericValueResolver_RejectsDeclaredUnreadableKey()
        {
            var boardId = BlackboardIdMapper.BoardId("global");
            var keyId = BlackboardIdMapper.KeyId("server.secret");
            var resolver = new DictionaryBlackboardResolver();
            BlackboardInitialization.Apply(new[]
            {
                new BlackboardInitializationPlan
                {
                    BoardId = boardId,
                    Keys = new System.Collections.Generic.List<BlackboardInitializationKey>
                    {
                        new BlackboardInitializationKey
                        {
                            KeyId = keyId,
                            Type = BlackboardKeyType.Double,
                            DoubleValue = 12,
                            CanRead = false,
                            CanWrite = true
                        }
                    }
                }
            }, resolver);
            var valueRef = NumericValueRef.Blackboard(boardId, keyId);
            var ctx = new ExecCtx<object>(
                null, null, null, null, resolver, null, null, null, null, default, null);

            Assert.That(NumericValueRefResolver.TryResolve(in valueRef, new object(), in ctx, out _), Is.False);
        }

        [Test]
        public void InitializeBlackboards_DoesNotMaterializeOwnerPlansInGlobalResolver()
        {
            var globalBoardId = BlackboardIdMapper.BoardId("global");
            var ownerBoardId = BlackboardIdMapper.BoardId("local.module:test");
            var json = $@"
{{
  ""FormatVersion"": 1,
  ""Triggers"": [],
  ""Blackboards"": [
    {{ ""BoardId"": {globalBoardId}, ""Scope"": ""global"", ""Keys"": [] }},
    {{ ""BoardId"": {ownerBoardId}, ""Scope"": ""owner"", ""Keys"": [] }}
  ]
}}";
            var database = new TriggerPlanJsonDatabase();
            database.LoadFromJson(json, "owner-blackboard-scope-test");
            var global = new DictionaryBlackboardResolver();
            var owners = new OwnerBlackboardStore(global);

            database.InitializeBlackboards(global);
            database.ConfigureOwnerBlackboards(owners);

            Assert.That(global.TryResolve(globalBoardId, out _), Is.True);
            Assert.That(global.TryResolve(ownerBoardId, out _), Is.False);
            Assert.That(owners.GetOrCreate(1).TryResolve(ownerBoardId, out _), Is.True);
        }

        [Test]
        public void AggregateCompiler_RejectsConflictingBlackboardInitialization()
        {
            var boardId = BlackboardIdMapper.BoardId("skill");
            var keyId = BlackboardIdMapper.KeyId("skill.hitCount");
            var first = BlackboardDocument(boardId, keyId, 1);
            var second = BlackboardDocument(boardId, keyId, 2);

            var error = Assert.Throws<System.InvalidOperationException>(() =>
                TriggerPlanAggregateCompiler.Compile(new[]
                {
                    new TriggerPlanAggregateCompiler.SourceDocument("first", first),
                    new TriggerPlanAggregateCompiler.SourceDocument("second", second)
                }));

            StringAssert.Contains("Conflicting Blackboard board ID", error.Message);
        }

        private static string BlackboardDocument(int boardId, int keyId, int value)
        {
            return $@"
{{
  ""FormatVersion"": 1,
  ""Triggers"": [],
  ""Blackboards"": [
    {{
      ""BoardId"": {boardId},
      ""Name"": ""skill"",
      ""Scope"": ""global"",
      ""Keys"": [
        {{ ""KeyId"": {keyId}, ""Name"": ""skill.hitCount"", ""Type"": ""Int"", ""IntValue"": {value} }}
      ]
    }}
  ]
}}";
        }
    }
}
