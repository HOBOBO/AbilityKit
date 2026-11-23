using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Services
{
    public enum MobaTriggerExecutionBlockReason : byte
    {
        None = 0,
        MaxDepth = 1,
        MaxFrameExecutions = 2,
        MaxRootExecutionsPerFrame = 3,
        MaxSameTriggerPerRoot = 4,
    }

    public readonly struct MobaTriggerExecutionBudgetOptions
    {
        public MobaTriggerExecutionBudgetOptions(
            int maxDepth,
            int maxExecutionsPerFrame,
            int maxExecutionsPerRootPerFrame,
            int maxSameTriggerPerRoot)
        {
            MaxDepth = maxDepth;
            MaxExecutionsPerFrame = maxExecutionsPerFrame;
            MaxExecutionsPerRootPerFrame = maxExecutionsPerRootPerFrame;
            MaxSameTriggerPerRoot = maxSameTriggerPerRoot;
        }

        public int MaxDepth { get; }
        public int MaxExecutionsPerFrame { get; }
        public int MaxExecutionsPerRootPerFrame { get; }
        public int MaxSameTriggerPerRoot { get; }

        public static MobaTriggerExecutionBudgetOptions Default => new MobaTriggerExecutionBudgetOptions(
            maxDepth: 32,
            maxExecutionsPerFrame: 2048,
            maxExecutionsPerRootPerFrame: 256,
            maxSameTriggerPerRoot: 64);
    }

    public readonly struct MobaTriggerExecutionRequest
    {
        public MobaTriggerExecutionRequest(
            int triggerId,
            int frame,
            long rootContextId,
            long parentContextId,
            int sourceActorId,
            int targetActorId,
            EffectContextKind contextKind,
            MobaExecutionKind originKind)
        {
            TriggerId = triggerId;
            Frame = frame;
            RootContextId = rootContextId;
            ParentContextId = parentContextId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            ContextKind = contextKind;
            OriginKind = originKind;
        }

        public int TriggerId { get; }
        public int Frame { get; }
        public long RootContextId { get; }
        public long ParentContextId { get; }
        public int SourceActorId { get; }
        public int TargetActorId { get; }
        public EffectContextKind ContextKind { get; }
        public MobaExecutionKind OriginKind { get; }

        public long BudgetRootKey => RootContextId != 0 ? RootContextId : ParentContextId;
    }

    public readonly struct MobaTriggerExecutionBudgetToken
    {
        internal MobaTriggerExecutionBudgetToken(int frame, long rootKey, int triggerId)
        {
            Frame = frame;
            RootKey = rootKey;
            TriggerId = triggerId;
            IsValid = true;
        }

        public int Frame { get; }
        public long RootKey { get; }
        public int TriggerId { get; }
        public bool IsValid { get; }
    }

    public readonly struct MobaTriggerExecutionBlock
    {
        public MobaTriggerExecutionBlock(
            MobaTriggerExecutionBlockReason reason,
            int currentDepth,
            int currentFrameCount,
            int currentRootCount,
            int currentSameTriggerCount)
        {
            Reason = reason;
            CurrentDepth = currentDepth;
            CurrentFrameCount = currentFrameCount;
            CurrentRootCount = currentRootCount;
            CurrentSameTriggerCount = currentSameTriggerCount;
        }

        public MobaTriggerExecutionBlockReason Reason { get; }
        public int CurrentDepth { get; }
        public int CurrentFrameCount { get; }
        public int CurrentRootCount { get; }
        public int CurrentSameTriggerCount { get; }
    }

    internal sealed class MobaTriggerExecutionBudget
    {
        private readonly Dictionary<long, int> _rootCounts = new Dictionary<long, int>();
        private readonly Dictionary<RootTriggerKey, int> _sameTriggerCounts = new Dictionary<RootTriggerKey, int>();
        private readonly MobaTriggerExecutionBudgetOptions _options;
        private int _frame = int.MinValue;
        private int _frameCount;
        private int _depth;

        public MobaTriggerExecutionBudget()
            : this(MobaTriggerExecutionBudgetOptions.Default)
        {
        }

        public MobaTriggerExecutionBudget(in MobaTriggerExecutionBudgetOptions options)
        {
            _options = options;
        }

        public int CurrentDepth => _depth;
        public int CurrentFrameCount => _frameCount;

        public bool TryEnter(in MobaTriggerExecutionRequest request, out MobaTriggerExecutionBudgetToken token, out MobaTriggerExecutionBlock block)
        {
            token = default;
            block = default;
            ResetFrameIfNeeded(request.Frame);

            if (_options.MaxDepth > 0 && _depth >= _options.MaxDepth)
            {
                block = new MobaTriggerExecutionBlock(MobaTriggerExecutionBlockReason.MaxDepth, _depth, _frameCount, 0, 0);
                return false;
            }

            if (_options.MaxExecutionsPerFrame > 0 && _frameCount >= _options.MaxExecutionsPerFrame)
            {
                block = new MobaTriggerExecutionBlock(MobaTriggerExecutionBlockReason.MaxFrameExecutions, _depth, _frameCount, 0, 0);
                return false;
            }

            var rootKey = request.BudgetRootKey;
            var rootCount = 0;
            if (rootKey != 0 && _rootCounts.TryGetValue(rootKey, out rootCount) && _options.MaxExecutionsPerRootPerFrame > 0 && rootCount >= _options.MaxExecutionsPerRootPerFrame)
            {
                block = new MobaTriggerExecutionBlock(MobaTriggerExecutionBlockReason.MaxRootExecutionsPerFrame, _depth, _frameCount, rootCount, 0);
                return false;
            }

            var sameTriggerKey = default(RootTriggerKey);
            var sameTriggerCount = 0;
            if (rootKey != 0 && request.TriggerId > 0)
            {
                sameTriggerKey = new RootTriggerKey(rootKey, request.TriggerId);
                if (_sameTriggerCounts.TryGetValue(sameTriggerKey, out sameTriggerCount) && _options.MaxSameTriggerPerRoot > 0 && sameTriggerCount >= _options.MaxSameTriggerPerRoot)
                {
                    block = new MobaTriggerExecutionBlock(MobaTriggerExecutionBlockReason.MaxSameTriggerPerRoot, _depth, _frameCount, rootCount, sameTriggerCount);
                    return false;
                }
            }

            _depth++;
            _frameCount++;
            if (rootKey != 0) _rootCounts[rootKey] = rootCount + 1;
            if (rootKey != 0 && request.TriggerId > 0) _sameTriggerCounts[sameTriggerKey] = sameTriggerCount + 1;

            token = new MobaTriggerExecutionBudgetToken(request.Frame, rootKey, request.TriggerId);
            return true;
        }

        public void Exit(in MobaTriggerExecutionBudgetToken token)
        {
            if (!token.IsValid) return;
            if (_depth > 0) _depth--;
        }

        private void ResetFrameIfNeeded(int frame)
        {
            if (_frame == frame) return;
            _frame = frame;
            _frameCount = 0;
            _rootCounts.Clear();
            _sameTriggerCounts.Clear();
        }

        private readonly struct RootTriggerKey : IEquatable<RootTriggerKey>
        {
            public RootTriggerKey(long rootContextId, int triggerId)
            {
                RootContextId = rootContextId;
                TriggerId = triggerId;
            }

            public long RootContextId { get; }
            public int TriggerId { get; }

            public bool Equals(RootTriggerKey other)
            {
                return RootContextId == other.RootContextId && TriggerId == other.TriggerId;
            }

            public override bool Equals(object obj)
            {
                return obj is RootTriggerKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (RootContextId.GetHashCode() * 397) ^ TriggerId;
                }
            }
        }
    }
}
