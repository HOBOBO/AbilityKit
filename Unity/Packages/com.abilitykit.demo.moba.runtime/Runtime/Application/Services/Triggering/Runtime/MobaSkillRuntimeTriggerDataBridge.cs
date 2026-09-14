using System;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Demo.Moba.Services.Triggering
{
    public static class MobaSkillRuntimeTriggerBoards
    {
        public static readonly int Cast = BlackboardIdMapper.BoardId("skill_runtime.cast");
        public static readonly int Effect = BlackboardIdMapper.BoardId("skill_runtime.effect");
        public static readonly int Target = BlackboardIdMapper.BoardId("skill_runtime.target");
        public static readonly int Child = BlackboardIdMapper.BoardId("skill_runtime.child");
    }

    public sealed class MobaSkillRuntimeBlackboardAdapter : IBlackboard, IDynamicBlackboardSchema
    {
        private readonly MobaSkillRuntimeBlackboard _blackboard;
        private readonly MobaSkillRuntimeBlackboardAddress _address;

        public MobaSkillRuntimeBlackboardAdapter(MobaSkillRuntimeBlackboard blackboard, in MobaSkillRuntimeBlackboardAddress address)
        {
            _blackboard = blackboard ?? throw new ArgumentNullException(nameof(blackboard));
            _address = address;
        }

        public bool TryGetInt(int keyId, out int value)
        {
            value = default;
            if (!_blackboard.TryGetValue(keyId, in _address, out _, out var raw)) return false;
            if (raw.Kind != MobaSkillRuntimeValueKind.Int && raw.Kind != MobaSkillRuntimeValueKind.ActorId) return false;
            value = raw.IntValue;
            return true;
        }

        public void SetInt(int keyId, int value) => Set(keyId, MobaSkillRuntimeValueKind.Int, MobaSkillRuntimeValue.FromInt(value));

        public bool TryGetBool(int keyId, out bool value)
        {
            value = default;
            if (!_blackboard.TryGetValue(keyId, in _address, out _, out var raw) || raw.Kind != MobaSkillRuntimeValueKind.Bool) return false;
            value = raw.BoolValue;
            return true;
        }

        public void SetBool(int keyId, bool value) => Set(keyId, MobaSkillRuntimeValueKind.Bool, MobaSkillRuntimeValue.FromBool(value));

        public bool TryGetFloat(int keyId, out float value)
        {
            value = default;
            if (!_blackboard.TryGetValue(keyId, in _address, out _, out var raw)) return false;
            if (raw.Kind == MobaSkillRuntimeValueKind.Float) value = raw.FloatValue;
            else if (raw.Kind == MobaSkillRuntimeValueKind.Double) value = (float)raw.DoubleValue;
            else return false;
            return true;
        }

        public void SetFloat(int keyId, float value) => Set(keyId, MobaSkillRuntimeValueKind.Float, MobaSkillRuntimeValue.FromFloat(value));

        public bool TryGetDouble(int keyId, out double value)
        {
            value = default;
            if (!_blackboard.TryGetValue(keyId, in _address, out _, out var raw)) return false;
            switch (raw.Kind)
            {
                case MobaSkillRuntimeValueKind.Double: value = raw.DoubleValue; return true;
                case MobaSkillRuntimeValueKind.Float: value = raw.FloatValue; return true;
                case MobaSkillRuntimeValueKind.Int:
                case MobaSkillRuntimeValueKind.ActorId: value = raw.IntValue; return true;
                case MobaSkillRuntimeValueKind.Long:
                case MobaSkillRuntimeValueKind.ContextId: value = raw.LongValue; return true;
                default: return false;
            }
        }

        public void SetDouble(int keyId, double value) => Set(keyId, MobaSkillRuntimeValueKind.Double, MobaSkillRuntimeValue.FromDouble(value));

        public bool IsSnapshotCaptured(int keyId) => _blackboard.IsSnapshotCaptured(keyId, in _address);

        public void MarkSnapshotCaptured(int keyId) => _blackboard.MarkSnapshotCaptured(keyId, in _address);

        internal MobaSkillRuntimeBlackboardAdapter ForScopeOwner(long scopeOwnerId)
        {
            var address = new MobaSkillRuntimeBlackboardAddress(_address.Scope, scopeOwnerId);
            return address.Equals(_address) ? this : new MobaSkillRuntimeBlackboardAdapter(_blackboard, in address);
        }

        public bool TryGetString(int keyId, out string value)
        {
            value = default;
            if (!_blackboard.TryGetValue(keyId, in _address, out _, out var raw) || raw.Kind != MobaSkillRuntimeValueKind.String) return false;
            value = raw.StringValue;
            return true;
        }

        public void SetString(int keyId, string value) => Set(keyId, MobaSkillRuntimeValueKind.String, MobaSkillRuntimeValue.FromString(value));

        public bool TryGetKeySchema(int keyId, out BlackboardKeySchema schema)
        {
            schema = default;
            if (!_blackboard.TryGetKey(keyId, in _address, out var key)) return false;
            if (!TryMapType(key.ValueKind, out var type)) return false;
            schema = new BlackboardKeySchema(type, canRead: true, canWrite: true);
            return true;
        }

        public bool TryDefineKey(int keyId, BlackboardKeyType type, bool canRead = true, bool canWrite = true)
        {
            if (keyId == 0 || !canRead || !canWrite || !TryMapType(type, out var kind)) return false;
            var key = new MobaSkillRuntimeBlackboardKey(
                keyId,
                "trigger." + keyId,
                kind,
                _address.Scope,
                MobaSkillRuntimeBlackboardFlags.Rollback | MobaSkillRuntimeBlackboardFlags.Debug);
            return _blackboard.Register(in key, in _address);
        }

        private void Set(int keyId, MobaSkillRuntimeValueKind kind, in MobaSkillRuntimeValue value)
        {
            if (keyId == 0) throw new ArgumentOutOfRangeException(nameof(keyId));
            if (_blackboard.TryGetValue(keyId, in _address, out var existing, out _) && existing.ValueKind != kind)
                throw new InvalidOperationException($"Skill runtime Blackboard key '{keyId}' is already registered as {existing.ValueKind}.");

            var key = new MobaSkillRuntimeBlackboardKey(
                keyId,
                "trigger." + keyId,
                kind,
                _address.Scope,
                MobaSkillRuntimeBlackboardFlags.Rollback | MobaSkillRuntimeBlackboardFlags.Debug);
            if (!_blackboard.Set(in key, in value, in _address))
                throw new InvalidOperationException($"Failed to write skill runtime Blackboard key '{keyId}'.");
        }

        private static bool TryMapType(MobaSkillRuntimeValueKind kind, out BlackboardKeyType type)
        {
            switch (kind)
            {
                case MobaSkillRuntimeValueKind.Int:
                case MobaSkillRuntimeValueKind.ActorId: type = BlackboardKeyType.Int; return true;
                case MobaSkillRuntimeValueKind.Float: type = BlackboardKeyType.Float; return true;
                case MobaSkillRuntimeValueKind.Double:
                case MobaSkillRuntimeValueKind.Long:
                case MobaSkillRuntimeValueKind.ContextId: type = BlackboardKeyType.Double; return true;
                case MobaSkillRuntimeValueKind.Bool: type = BlackboardKeyType.Bool; return true;
                case MobaSkillRuntimeValueKind.String: type = BlackboardKeyType.String; return true;
                default: type = BlackboardKeyType.Unknown; return false;
            }
        }

        private static bool TryMapType(BlackboardKeyType type, out MobaSkillRuntimeValueKind kind)
        {
            switch (type)
            {
                case BlackboardKeyType.Int: kind = MobaSkillRuntimeValueKind.Int; return true;
                case BlackboardKeyType.Float: kind = MobaSkillRuntimeValueKind.Float; return true;
                case BlackboardKeyType.Double: kind = MobaSkillRuntimeValueKind.Double; return true;
                case BlackboardKeyType.Bool: kind = MobaSkillRuntimeValueKind.Bool; return true;
                case BlackboardKeyType.String: kind = MobaSkillRuntimeValueKind.String; return true;
                default: kind = MobaSkillRuntimeValueKind.None; return false;
            }
        }
    }

    public sealed class MobaSkillRuntimeBlackboardResolver : IBlackboardResolver
    {
        private readonly IBlackboardResolver _fallback;
        private readonly MobaSkillRuntimeBlackboardAdapter _cast;
        private readonly MobaSkillRuntimeBlackboardAdapter _effect;
        private readonly MobaSkillRuntimeBlackboardAdapter _target;
        private readonly MobaSkillRuntimeBlackboardAdapter _child;

        public MobaSkillRuntimeBlackboardResolver(
            MobaSkillRuntimeBlackboard blackboard,
            long effectContextId,
            int targetActorId,
            long childContextId,
            IBlackboardResolver fallback = null)
        {
            _fallback = fallback;
            _cast = Create(blackboard, MobaSkillRuntimeBlackboardScope.Cast, 0L);
            _effect = Create(blackboard, MobaSkillRuntimeBlackboardScope.Effect, effectContextId);
            _target = Create(blackboard, MobaSkillRuntimeBlackboardScope.Target, targetActorId);
            _child = Create(blackboard, MobaSkillRuntimeBlackboardScope.Child, childContextId);
        }

        public bool TryResolve(int boardId, out IBlackboard blackboard)
        {
            blackboard = null;
            if (boardId == MobaSkillRuntimeTriggerBoards.Cast) blackboard = _cast;
            else if (boardId == MobaSkillRuntimeTriggerBoards.Effect) blackboard = _effect;
            else if (boardId == MobaSkillRuntimeTriggerBoards.Target) blackboard = _target;
            else if (boardId == MobaSkillRuntimeTriggerBoards.Child) blackboard = _child;
            else return _fallback != null && _fallback.TryResolve(boardId, out blackboard);
            return blackboard != null;
        }

        private static MobaSkillRuntimeBlackboardAdapter Create(MobaSkillRuntimeBlackboard blackboard, MobaSkillRuntimeBlackboardScope scope, long ownerId)
        {
            var address = new MobaSkillRuntimeBlackboardAddress(scope, ownerId);
            return new MobaSkillRuntimeBlackboardAdapter(blackboard, in address);
        }
    }

    /// <summary>RPN domain keys use "cast.foo", "effect.foo", "target.foo" or "child.foo".</summary>
    public sealed class MobaSkillRuntimeNumericVarDomain : INumericVarDomain
    {
        public const string Domain = "skill_runtime";
        public string DomainId => Domain;

        public bool TryGet<TCtx>(in ExecCtx<TCtx> ctx, string key, out double value)
        {
            value = default;
            return TryResolve(ctx.Blackboards, key, out var board, out var keyId) && board.TryGetDouble(keyId, out value);
        }

        public bool TrySet<TCtx>(in ExecCtx<TCtx> ctx, string key, double value)
        {
            if (!TryResolve(ctx.Blackboards, key, out var board, out var keyId)) return false;
            board.SetDouble(keyId, value);
            return true;
        }

        private static bool TryResolve(IBlackboardResolver resolver, string key, out IBlackboard board, out int keyId)
        {
            board = null;
            keyId = 0;
            if (resolver == null || string.IsNullOrWhiteSpace(key)) return false;
            var separator = key.IndexOf('.');
            var scope = separator > 0 ? key.Substring(0, separator) : "cast";
            var name = separator > 0 ? key.Substring(separator + 1) : key;
            if (string.IsNullOrWhiteSpace(name)) return false;

            var boardId = scope.ToLowerInvariant() switch
            {
                "cast" => MobaSkillRuntimeTriggerBoards.Cast,
                "effect" => MobaSkillRuntimeTriggerBoards.Effect,
                "target" => MobaSkillRuntimeTriggerBoards.Target,
                "child" => MobaSkillRuntimeTriggerBoards.Child,
                _ => 0,
            };
            if (boardId == 0 || !resolver.TryResolve(boardId, out board) || board == null) return false;
            keyId = BlackboardIdMapper.KeyId($"{Domain}.{scope}.{name}");
            return keyId != 0;
        }
    }

    public readonly struct MobaTriggerActionOutputPort
    {
        private readonly MobaSkillRuntimeBlackboard _blackboard;
        private readonly MobaSkillRuntimeBlackboardAddress _address;

        public MobaTriggerActionOutputPort(MobaSkillRuntimeBlackboard blackboard, in MobaSkillRuntimeBlackboardAddress address)
        {
            _blackboard = blackboard;
            _address = address;
        }

        public bool IsValid => _blackboard != null;

        public bool WriteActorId(int keyId, int actorId, string name = null)
        {
            var key = new MobaSkillRuntimeBlackboardKey(keyId, name, MobaSkillRuntimeValueKind.ActorId, _address.Scope,
                MobaSkillRuntimeBlackboardFlags.Rollback | MobaSkillRuntimeBlackboardFlags.Debug);
            var value = MobaSkillRuntimeValue.FromActorId(actorId);
            return actorId > 0 && _blackboard != null && _blackboard.Set(in key, in value, in _address);
        }

        public bool WriteContextId(int keyId, long contextId, string name = null)
        {
            var key = new MobaSkillRuntimeBlackboardKey(keyId, name, MobaSkillRuntimeValueKind.ContextId, _address.Scope,
                MobaSkillRuntimeBlackboardFlags.Rollback | MobaSkillRuntimeBlackboardFlags.Debug);
            var value = MobaSkillRuntimeValue.FromContextId(contextId);
            return contextId != 0L && _blackboard != null && _blackboard.Set(in key, in value, in _address);
        }

        public bool WriteFloat(int keyId, float number, string name = null)
        {
            var key = new MobaSkillRuntimeBlackboardKey(keyId, name, MobaSkillRuntimeValueKind.Float, _address.Scope,
                MobaSkillRuntimeBlackboardFlags.Rollback | MobaSkillRuntimeBlackboardFlags.Debug);
            var value = MobaSkillRuntimeValue.FromFloat(number);
            return _blackboard != null && _blackboard.Set(in key, in value, in _address);
        }
    }
}
