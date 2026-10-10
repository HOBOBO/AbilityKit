using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Demo.Moba.Services
{
    [MobaSnapshotEmitter(93)]
    [WorldService(typeof(MobaCharacterActionSnapshotService))]
    public sealed class MobaCharacterActionSnapshotService : IService, IMobaSnapshotEmitter
    {
        private readonly MobaActorRegistry _actors;
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
                current.Add(new CharacterActionData(id, _actors.GetEntityVersion(id), runtime.Frame,
                    action.Path, action.InstanceId, action.StartFrame, action.LocalFrame,
                    action.CastInstanceId, action.SkillId));
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
                    a.SkillId != b.SkillId) return false;
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
