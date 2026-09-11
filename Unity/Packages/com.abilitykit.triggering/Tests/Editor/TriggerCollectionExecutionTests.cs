using System.Collections.Generic;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Collections;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Triggering.Validation;
using NUnit.Framework;

namespace AbilityKit.Triggering.Tests
{
    public sealed class TriggerCollectionExecutionTests
    {
        [Test]
        public void ForEach_WritesItemsAndHonorsIterationLimit()
        {
            var boardId = BlackboardIdMapper.BoardId("test.foreach");
            var keyId = BlackboardIdMapper.KeyId("item");
            var boards = new DictionaryBlackboardResolver();
            var board = new DictionaryBlackboard();
            board.DefineKey(keyId, BlackboardKeyType.Int);
            boards.Register(boardId, board);

            using (var collections = new TriggerCollectionStore())
            {
                var handle = collections.Register(TriggerCollection.FromInt32(new[] { 11, 22, 33 }, "test.entity"));
                var target = new BlackboardWriteTarget(boardId, keyId, BlackboardKeyType.Int, "owner");
                var recorder = new RecordCurrentIntExecutable(boardId, keyId);
                var node = new ForEachTriggerPlanExecutable(
                    NumericValueRef.Const(handle.Value),
                    in target,
                    recorder,
                    maxIterations: 2);
                var control = new ExecutionControl();
                control.Reset();
                var ctx = new ExecCtx<object>(
                    new object(), null, null, null, boards, null, null, null, null,
                    default, control, collections: collections);

                var result = node.Execute(new object(), in ctx);

                Assert.That(result.IsSuccess, Is.True, result.Reason);
                Assert.That(recorder.Values, Is.EqualTo(new[] { 11, 22 }));
            }
        }

        [Test]
        public void ForEach_JsonRoundTripBuildsTypedExecutionNode()
        {
            var boardId = BlackboardIdMapper.BoardId("test.foreach.json");
            var keyId = BlackboardIdMapper.KeyId("item");
            var json = $@"
{{
  ""FormatVersion"": 1,
  ""Triggers"": [
    {{
      ""TriggerId"": 7301,
      ""EventName"": ""test:foreach"",
      ""ExecutionRoot"": {{
        ""Kind"": ""ForEach"",
        ""Collection"": {{ ""Kind"": ""Const"", ""ConstValue"": 9 }},
        ""ItemTarget"": {{
          ""Kind"": ""BlackboardTarget"",
          ""BoardId"": {boardId},
          ""KeyId"": {keyId},
          ""KeyType"": ""Int"",
          ""Scope"": ""owner""
        }},
        ""MaxIterations"": 16,
        ""Children"": [{{ ""Kind"": ""Succeed"" }}]
      }}
    }}
  ]
}}";
            var database = new TriggerPlanJsonDatabase();

            database.LoadFromJson(json, "foreach-json-test");

            Assert.That(database.TryGetExecutionRootByTriggerId(7301, out var root), Is.True);
            Assert.That(root, Is.TypeOf<ForEachTriggerPlanExecutable>());
            var forEach = (ForEachTriggerPlanExecutable)root;
            Assert.That(forEach.MaxIterations, Is.EqualTo(16));
            Assert.That(forEach.ItemTarget.BoardId, Is.EqualTo(boardId));
            Assert.That(forEach.ItemTarget.KeyId, Is.EqualTo(keyId));
        }

        [Test]
        public void Validator_RejectsUnboundedForEach()
        {
            var target = new BlackboardWriteTarget(1, 2, BlackboardKeyType.Int, "owner");
            var node = new ForEachTriggerPlanExecutable(
                NumericValueRef.Const(1),
                in target,
                new SucceedTriggerPlanExecutable(null),
                maxIterations: 0);

            var result = new TriggerPlanExecutableValidator().Validate(node);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Matches<ValidationIssue>(issue =>
                issue.Code == ValidationErrorCodes.INVALID_EXECUTION_NODE &&
                issue.Path.EndsWith(".maxIterations")));
        }

        [Test]
        public void Store_DisposeInvalidatesAllHandles()
        {
            var store = new TriggerCollectionStore();
            var handle = store.Register(TriggerCollection.FromInt32(new[] { 1 }));

            store.Dispose();

            Assert.That(store.TryResolve(handle, out _), Is.False);
            Assert.Throws<System.ObjectDisposedException>(() =>
                store.Register(TriggerCollection.FromInt32(new[] { 2 })));
        }

        private sealed class RecordCurrentIntExecutable : TriggerPlanExecutableBase
        {
            private readonly int _boardId;
            private readonly int _keyId;

            public RecordCurrentIntExecutable(int boardId, int keyId)
            {
                _boardId = boardId;
                _keyId = keyId;
            }

            public List<int> Values { get; } = new List<int>();
            public override string Name => "RecordCurrentInt";
            public override ETriggerPlanExecutableKind Kind => ETriggerPlanExecutableKind.Action;

            protected override TriggerPlanExecutionResult ExecuteCore<TCtx>(object args, in ExecCtx<TCtx> ctx)
            {
                if (ctx.Blackboards != null &&
                    ctx.Blackboards.TryResolve(_boardId, out var board) &&
                    board.TryGetInt(_keyId, out var value))
                {
                    Values.Add(value);
                    return TriggerPlanExecutionResult.Success();
                }

                return TriggerPlanExecutionResult.Failed("current item unavailable");
            }
        }
    }
}
