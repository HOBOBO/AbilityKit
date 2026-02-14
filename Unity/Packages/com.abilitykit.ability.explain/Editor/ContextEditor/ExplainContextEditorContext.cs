using System;
using AbilityKit.Ability.Explain;

namespace AbilityKit.Ability.Explain.Editor
{
    public sealed class ExplainContextEditorContext
    {
        public readonly PipelineItemKey Key;
        public readonly ExplainResolveContext ResolveContext;

        private readonly Action _requestResolve;
        private readonly Action _close;

        public ExplainContextEditorContext(in PipelineItemKey key, ExplainResolveContext resolveContext, Action requestResolve, Action close)
        {
            Key = key;
            ResolveContext = resolveContext;
            _requestResolve = requestResolve;
            _close = close;
        }

        public void RequestResolve() => _requestResolve?.Invoke();
        public void Close() => _close?.Invoke();
    }
}
