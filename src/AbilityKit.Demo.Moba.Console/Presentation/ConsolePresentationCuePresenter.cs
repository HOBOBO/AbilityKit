using System;
using System.Collections.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Console.Platform;
using AbilityKit.Demo.Moba.Console.View;
using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Demo.Moba.Console.Presentation
{
    public sealed class ConsolePresentationCuePresenter : IDisposable
    {
        private readonly ConsoleVfxManager _vfxManager;
        private readonly ConsoleEntityDisplayService _entities;
        private readonly Dictionary<string, int> _activeByRequestKey = new();
        private bool _disposed;

        public ConsolePresentationCuePresenter(ConsoleVfxManager vfxManager, ConsoleEntityDisplayService entities)
        {
            _vfxManager = vfxManager ?? throw new ArgumentNullException(nameof(vfxManager));
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        }

        public void Tick(double logicTimeSeconds)
        {
            _vfxManager.SetLogicTime(logicTimeSeconds);
        }

        public void Handle(in PresentationCueData data)
        {
            if (_disposed) return;

            if (ShouldStart(data.Stage))
            {
                Play(in data);
                return;
            }

            // KeepActive（Ticked/Refreshed/StackChanged）代表同一个表现对象仍然存活且需要续期，
            // 不能当作 No-op 丢弃，也不能重复 Create。
            if (ShouldKeepActive(data.Stage))
            {
                Refresh(in data);
                return;
            }

            if (ShouldStop(data.Stage))
            {
                Stop(GetRequestKey(in data), data.Stage);
            }
        }

        private void Refresh(in PresentationCueData data)
        {
            var requestKey = GetRequestKey(in data);
            if (!_activeByRequestKey.ContainsKey(requestKey)) return;
            Log.View($"[CuePresenter] Refresh request={requestKey}, stage={data.Stage}");
        }

        private void Play(in PresentationCueData data)
        {
            var requestKey = GetRequestKey(in data);
            if (_activeByRequestKey.ContainsKey(requestKey)) return;

            var templateId = data.VfxId != 0 ? data.VfxId : data.TemplateId;
            if (templateId == 0) return;

            ResolvePosition(in data, out var position);
            var followId = ResolveFollowActor(in data);
            var duration = data.DurationMsOverride > 0 ? data.DurationMsOverride / 1000f : 2f;
            var vfxId = _vfxManager.CreateVfx(templateId, followId, position.X, position.Y, position.Z, duration);
            if (vfxId <= 0) return;

            _activeByRequestKey[requestKey] = vfxId;
            Log.View($"[CuePresenter] Play request={requestKey}, template={templateId}, stage={data.Stage}, follow=#{followId}");
        }

        private void Stop(string requestKey, PresentationCueStage stage)
        {
            if (string.IsNullOrEmpty(requestKey)) return;
            if (!_activeByRequestKey.TryGetValue(requestKey, out var vfxId)) return;

            _activeByRequestKey.Remove(requestKey);
            _vfxManager.DestroyVfx(vfxId);
            Log.View($"[CuePresenter] Stop request={requestKey}, stage={stage}");
        }

        // 转发到共享权威实现。这里原先是一份独立的且不完整的表：
        // 缺 Started/Expired/Removed/Completed，导致 console 端所有 buff cue 永不生成、永不销毁。
        private static bool ShouldStart(PresentationCueStage stage)
        {
            return PresentationCueSemantics.Classify((int)stage) == PresentationCueTransition.Start;
        }

        private static bool ShouldKeepActive(PresentationCueStage stage)
        {
            return PresentationCueSemantics.Classify((int)stage) == PresentationCueTransition.KeepActive;
        }

        private static bool ShouldStop(PresentationCueStage stage)
        {
            return PresentationCueSemantics.Classify((int)stage) == PresentationCueTransition.Stop;
        }

        private static string GetRequestKey(in PresentationCueData data)
        {
            if (!string.IsNullOrWhiteSpace(data.RequestKey)) return data.RequestKey;
            return data.TriggerId > 0 ? $"cue:{data.TriggerId}:{data.Order}" : $"cue:{data.Order}";
        }

        private static int ResolveFollowActor(in PresentationCueData data)
        {
            if (data.TargetActorId > 0) return data.TargetActorId;
            if (data.SourceActorId > 0) return data.SourceActorId;
            if (data.Targets != null && data.Targets.Count > 0) return data.Targets[0];
            return 0;
        }

        private void ResolvePosition(in PresentationCueData data, out Vec3 position)
        {
            if (data.Positions != null && data.Positions.Count > 0)
            {
                var p = data.Positions[0];
                position = new Vec3(p.X + data.OffsetX, p.Y + data.OffsetY, p.Z + data.OffsetZ);
                return;
            }

            var followId = ResolveFollowActor(in data);
            if (followId > 0 && _entities.TryGet(followId, out var entity))
            {
                position = new Vec3(entity.X + data.OffsetX, entity.Y + data.OffsetY, entity.Z + data.OffsetZ);
                return;
            }

            position = new Vec3(data.OffsetX, data.OffsetY, data.OffsetZ);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _activeByRequestKey.Clear();
        }
    }
}
