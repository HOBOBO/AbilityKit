using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Services
{
    [MobaSnapshotEmitter(93)]
    [WorldService(typeof(MobaCharacterActionSnapshotService))]
    public sealed class MobaCharacterActionSnapshotService : IService, IMobaSnapshotEmitter
    {
        private readonly MobaActorRegistry _actors;
        [WorldInject(required: false)]
        private MobaSkillCastRuntimeService _casts = null;
        [WorldInject(required: false)]
        private SkillCastCoordinator _coordinator = null;
        [WorldInject(required: false)]
        private MobaConfigDatabase _configs = null;
        private CharacterActionData[] _lastPublished = Array.Empty<CharacterActionData>();
        private int _lastFrame = -1;
        private int _lastPublishedFrame = -1;

        public MobaCharacterActionSnapshotService(MobaActorRegistry actors)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            snapshot = default;
            if (frame.Value == _lastFrame) return false;
            _lastFrame = frame.Value;

            var ids = _actors.CopyActorIdsInOrder();
            var current = new System.Collections.Generic.List<CharacterActionData>(ids.Count);
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                if (!_actors.TryGet(id, out var actor) || !actor.hasCharacterHfsm ||
                    actor.characterHfsm.Runtime == null) continue;
                var runtime = actor.characterHfsm.Runtime;
                var action = runtime.Action;
                var castFrame = default(CastPresentationFrame);
                _casts?.TryGetPresentationFrame(action.CastInstanceId, id, out castFrame);
                var castStartFrame = -1;
                var castDurationMs = 0;
                if (runtime.IsAuxiliaryPresentationCast && _coordinator != null &&
                    _coordinator.TryGetRunningByInstanceId(id, action.CastInstanceId, out var running) &&
                    _configs != null && _configs.TryGetSkill(action.SkillId, out var skill) &&
                    _configs.TryGetSkillFlow(skill.CastFlowId, out var flow) && flow.Phases.Count == 1 &&
                    flow.Phases[0].Type == (int)SkillPhaseType.Timeline && flow.Phases[0].Timeline.DurationMs > 0)
                {
                    castStartFrame = running.StartFrame;
                    castDurationMs = flow.Phases[0].Timeline.DurationMs;
                }
                current.Add(new CharacterActionData(id, _actors.GetEntityVersion(id), runtime.Frame,
                    action.Path, action.InstanceId, action.StartFrame, action.LocalFrame,
                    action.CastInstanceId, action.SkillId, action.ModeId, action.ModeInstanceId,
                    action.ModeStartFrame, action.ModeLocalFrame, castFrame, castStartFrame, castDurationMs));
            }

            var entries = current.ToArray();
            if (entries.Length == 0 && _lastPublished.Length == 0) return false;
            if (SameActions(entries, _lastPublished) && frame.Value >= _lastPublishedFrame &&
                frame.Value - _lastPublishedFrame < 30) return false;

            snapshot = new WorldStateSnapshot(CharacterActionSnapshotCodec.OpCode,
                CharacterActionSnapshotCodec.Encode(entries));
            _lastPublished = entries;
            _lastPublishedFrame = frame.Value;
            return true;
        }

        private static bool SameActions(CharacterActionData[] left, CharacterActionData[] right)
        {
            if (left.Length != right.Length) return false;
            for (var i = 0; i < left.Length; i++)
            {
                var a = left[i];
                var b = right[i];
                if (a.ActorId != b.ActorId || a.EntityVersion != b.EntityVersion ||
                    a.Path != b.Path || a.InstanceId != b.InstanceId ||
                    a.StartFrame != b.StartFrame || a.CastInstanceId != b.CastInstanceId ||
                    a.SkillId != b.SkillId || a.CastStartFrame != b.CastStartFrame ||
                    a.CastDurationMs != b.CastDurationMs || a.ModeId != b.ModeId ||
                    a.ModeInstanceId != b.ModeInstanceId ||
                    a.ModeStartFrame != b.ModeStartFrame ||
                    a.ModeLocalFrame != b.ModeLocalFrame || !a.CastFrame.Equals(b.CastFrame)) return false;
            }
            return true;
        }

        public void Dispose()
        {
            _lastFrame = -1;
            _lastPublishedFrame = -1;
            _lastPublished = Array.Empty<CharacterActionData>();
        }
    }
}
