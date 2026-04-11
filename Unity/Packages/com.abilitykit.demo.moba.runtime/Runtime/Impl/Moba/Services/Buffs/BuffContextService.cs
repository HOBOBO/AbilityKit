using System;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.Share.Impl.Moba.Components;
using AbilityKit.Core.Common.Log;
using AbilityKit.Effect;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.Impl.Moba;
using EffectSourceRegistry = AbilityKit.Ability.Impl.Moba.EffectSource.MobaTraceRegistry;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    internal sealed class BuffContextService
    {
        private readonly EffectSourceRegistry _effectSource;
        private readonly ITriggerActionRunner _actionRunner;
        private readonly IFrameTime _frameTime;

        public BuffContextService(EffectSourceRegistry effectSource, ITriggerActionRunner actionRunner, IFrameTime frameTime)
        {
            _effectSource = effectSource;
            _actionRunner = actionRunner;
            _frameTime = frameTime;
        }

        public void EnsureBuffContext(BuffRuntime rt, int buffId, int sourceActorId, int targetActorId, object originSource, object originTarget, long parentContextId)
        {
            if (rt == null) return;
            if (rt.SourceContextId != 0) return;
            if (_effectSource == null) return;

            var frame = GetFrame();

            if (parentContextId != 0)
            {
                rt.SourceContextId = _effectSource.CreateChild(
                    parentContextId,
                    kind: EffectSourceKind.Buff,
                    configId: buffId,
                    sourceActorId: sourceActorId,
                    targetActorId: targetActorId,
                    frame: frame,
                    originSource: originSource,
                    originTarget: originTarget);
            }
            else
            {
                rt.SourceContextId = _effectSource.CreateRoot(
                    kind: EffectSourceKind.Buff,
                    configId: buffId,
                    sourceActorId: sourceActorId,
                    targetActorId: targetActorId,
                    frame: frame,
                    originSource: originSource,
                    originTarget: originTarget);
            }
        }

        public void CancelAndEnd(BuffRuntime rt)
        {
            if (rt == null) return;

            if (rt.SourceContextId == 0) return;

            var frame = GetFrame();

            try
            {
                _actionRunner?.CancelByOwnerKey(rt.SourceContextId);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[BuffContextService] CancelByOwnerKey exception (sourceContextId={rt.SourceContextId})");
            }

            try
            {
                _effectSource?.End(rt.SourceContextId, frame, EffectSourceEndReason.Replaced);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[BuffContextService] EffectSource.End exception (sourceContextId={rt.SourceContextId})");
            }

            rt.SourceContextId = 0;
        }

        public void EndByRuntime(BuffRuntime rt, EffectSourceEndReason reason)
        {
            if (rt == null) return;

            EndByRuntimeNoClear(rt, reason);
            rt.SourceContextId = 0;
        }

        public void EndByRuntimeNoClear(BuffRuntime rt, EffectSourceEndReason reason)
        {
            if (rt == null) return;

            if (rt.SourceContextId == 0) return;

            var frame = GetFrame();

            try
            {
                _actionRunner?.CancelByOwnerKey(rt.SourceContextId);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[BuffContextService] CancelByOwnerKey exception (sourceContextId={rt.SourceContextId})");
            }

            try
            {
                _effectSource?.End(rt.SourceContextId, frame, reason);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[BuffContextService] EffectSource.End exception (sourceContextId={rt.SourceContextId}, reason={reason})");
            }
        }

        private int GetFrame()
        {
            try
            {
                return _frameTime != null ? _frameTime.Frame.Value : 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
