using System;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Triggering.Variables.Numeric.Domains
{
    public sealed class BlackboardNumericVarDomain : INumericVarDomain
    {
        private readonly string _domainId;
        private readonly int _boardId;
        private readonly IBlackboardDomainResolver _domainResolver;

        public BlackboardNumericVarDomain(string domainId, int boardId)
        {
            if (string.IsNullOrEmpty(domainId)) throw new ArgumentNullException(nameof(domainId));
            if (boardId == 0) throw new ArgumentOutOfRangeException(nameof(boardId), "boardId must not be 0");

            _domainId = domainId;
            _boardId = boardId;
            _domainResolver = null;
        }

        public BlackboardNumericVarDomain(string domainId, IBlackboardDomainResolver domainResolver)
        {
            if (string.IsNullOrEmpty(domainId)) throw new ArgumentNullException(nameof(domainId));
            if (domainResolver == null) throw new ArgumentNullException(nameof(domainResolver));

            _domainId = domainId;
            _boardId = 0;
            _domainResolver = domainResolver;
        }

        public string DomainId => _domainId;

        public bool TryGet<TCtx>(in ExecCtx<TCtx> ctx, string key, out double value)
        {
            value = 0d;
            if (string.IsNullOrEmpty(key)) return false;

            var boardId = _boardId;
            if (boardId == 0)
            {
                if (_domainResolver == null) return false;
                if (!_domainResolver.TryResolveBoardId(in ctx, _domainId, out boardId)) return false;
                if (boardId == 0) return false;
            }

            var resolver = ctx.Blackboards;
            if (resolver == null) return false;
            if (!resolver.TryResolve(boardId, out var bb) || bb == null) return false;

            var normalizedDomain = BlackboardNameUtil.Normalize(_domainId);
            var normalizedKey = BlackboardNameUtil.Normalize(key);
            var keyId = BlackboardIdMapper.KeyId($"{normalizedDomain}.{normalizedKey}");
            return bb.TryGetDouble(keyId, out value);
        }

        public bool TrySet<TCtx>(in ExecCtx<TCtx> ctx, string key, double value)
        {
            if (string.IsNullOrEmpty(key)) return false;

            var boardId = _boardId;
            if (boardId == 0)
            {
                if (_domainResolver == null) return false;
                if (!_domainResolver.TryResolveBoardId(in ctx, _domainId, out boardId)) return false;
                if (boardId == 0) return false;
            }

            var resolver = ctx.Blackboards;
            if (resolver == null) return false;
            if (!resolver.TryResolve(boardId, out var bb) || bb == null) return false;

            var normalizedDomain = BlackboardNameUtil.Normalize(_domainId);
            var normalizedKey = BlackboardNameUtil.Normalize(key);
            var keyId = BlackboardIdMapper.KeyId($"{normalizedDomain}.{normalizedKey}");
            bb.SetDouble(keyId, value);
            return true;
        }
    }
}
