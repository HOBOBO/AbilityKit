using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Generic;
using AbilityKit.Core.Common.Record.Core;
using UnityEngine;

namespace AbilityKit.World.Record.Samples
{
    public sealed class BattleLogRecordSample : MonoBehaviour
    {
        private static class BattleLogEventNames
        {
            public const string SkillCast = "battle.skill.cast";
            public const string BuffAdd = "battle.buff.add";
            public const string Damage = "battle.damage";
        }

        private static class BattleLogEventTypes
        {
            public static readonly RecordEventType SkillCast = RecordEventType.FromName(BattleLogEventNames.SkillCast);
            public static readonly RecordEventType BuffAdd = RecordEventType.FromName(BattleLogEventNames.BuffAdd);
            public static readonly RecordEventType Damage = RecordEventType.FromName(BattleLogEventNames.Damage);
        }

        public static class BattleSkillCastEventCodec
        {
            public static byte[] Encode(int casterActorId, int skillCode, int castInstanceId)
            {
                return BinaryObjectCodec.Encode(new Payload(casterActorId, skillCode, castInstanceId));
            }

            public static void Decode(byte[] payload, out int casterActorId, out int skillCode, out int castInstanceId)
            {
                var p = BinaryObjectCodec.Decode<Payload>(payload);
                casterActorId = p.CasterActorId;
                skillCode = p.SkillCode;
                castInstanceId = p.CastInstanceId;
            }

            public static void Write(IEventTrackWriter writer, FrameIndex frame, int casterActorId, int skillCode, int castInstanceId)
            {
                if (writer == null) return;
                writer.Append(frame, BattleLogEventTypes.SkillCast, Encode(casterActorId, skillCode, castInstanceId));
            }

            public static bool TryRead(in RecordEvent e, out int casterActorId, out int skillCode, out int castInstanceId)
            {
                if (e.EventType != BattleLogEventTypes.SkillCast)
                {
                    casterActorId = 0;
                    skillCode = 0;
                    castInstanceId = 0;
                    return false;
                }

                Decode(e.Payload, out casterActorId, out skillCode, out castInstanceId);
                return true;
            }

            public readonly struct Payload
            {
                [BinaryMember(0)] public readonly int CasterActorId;
                [BinaryMember(1)] public readonly int SkillCode;
                [BinaryMember(2)] public readonly int CastInstanceId;

                public Payload(int casterActorId, int skillCode, int castInstanceId)
                {
                    CasterActorId = casterActorId;
                    SkillCode = skillCode;
                    CastInstanceId = castInstanceId;
                }
            }
        }

        public static class BattleBuffAddEventCodec
        {
            public static byte[] Encode(int sourceActorId, int targetActorId, int buffCode, int buffInstanceId, int stacks, int durationFrames)
            {
                return BinaryObjectCodec.Encode(new Payload(sourceActorId, targetActorId, buffCode, buffInstanceId, stacks, durationFrames));
            }

            public static void Decode(byte[] payload, out int sourceActorId, out int targetActorId, out int buffCode, out int buffInstanceId, out int stacks, out int durationFrames)
            {
                var p = BinaryObjectCodec.Decode<Payload>(payload);
                sourceActorId = p.SourceActorId;
                targetActorId = p.TargetActorId;
                buffCode = p.BuffCode;
                buffInstanceId = p.BuffInstanceId;
                stacks = p.Stacks;
                durationFrames = p.DurationFrames;
            }

            public static void Write(IEventTrackWriter writer, FrameIndex frame, int sourceActorId, int targetActorId, int buffCode, int buffInstanceId, int stacks, int durationFrames)
            {
                if (writer == null) return;
                writer.Append(frame, BattleLogEventTypes.BuffAdd, Encode(sourceActorId, targetActorId, buffCode, buffInstanceId, stacks, durationFrames));
            }

            public static bool TryRead(in RecordEvent e, out int sourceActorId, out int targetActorId, out int buffCode, out int buffInstanceId, out int stacks, out int durationFrames)
            {
                if (e.EventType != BattleLogEventTypes.BuffAdd)
                {
                    sourceActorId = 0;
                    targetActorId = 0;
                    buffCode = 0;
                    buffInstanceId = 0;
                    stacks = 0;
                    durationFrames = 0;
                    return false;
                }

                Decode(e.Payload, out sourceActorId, out targetActorId, out buffCode, out buffInstanceId, out stacks, out durationFrames);
                return true;
            }

            public readonly struct Payload
            {
                [BinaryMember(0)] public readonly int SourceActorId;
                [BinaryMember(1)] public readonly int TargetActorId;
                [BinaryMember(2)] public readonly int BuffCode;
                [BinaryMember(3)] public readonly int BuffInstanceId;
                [BinaryMember(4)] public readonly int Stacks;
                [BinaryMember(5)] public readonly int DurationFrames;

                public Payload(int sourceActorId, int targetActorId, int buffCode, int buffInstanceId, int stacks, int durationFrames)
                {
                    SourceActorId = sourceActorId;
                    TargetActorId = targetActorId;
                    BuffCode = buffCode;
                    BuffInstanceId = buffInstanceId;
                    Stacks = stacks;
                    DurationFrames = durationFrames;
                }
            }
        }

        public static class BattleDamageEventCodec
        {
            public static byte[] Encode(int sourceActorId, int targetActorId, int skillCode, int damageAmount, int hpBefore, int hpAfter, byte critFlag)
            {
                return BinaryObjectCodec.Encode(new Payload(sourceActorId, targetActorId, skillCode, damageAmount, hpBefore, hpAfter, critFlag));
            }

            public static void Decode(byte[] payload, out int sourceActorId, out int targetActorId, out int skillCode, out int damageAmount, out int hpBefore, out int hpAfter, out byte critFlag)
            {
                var p = BinaryObjectCodec.Decode<Payload>(payload);
                sourceActorId = p.SourceActorId;
                targetActorId = p.TargetActorId;
                skillCode = p.SkillCode;
                damageAmount = p.DamageAmount;
                hpBefore = p.HpBefore;
                hpAfter = p.HpAfter;
                critFlag = p.CritFlag;
            }

            public static void Write(IEventTrackWriter writer, FrameIndex frame, int sourceActorId, int targetActorId, int skillCode, int damageAmount, int hpBefore, int hpAfter, bool isCrit)
            {
                if (writer == null) return;
                writer.Append(frame, BattleLogEventTypes.Damage, Encode(sourceActorId, targetActorId, skillCode, damageAmount, hpBefore, hpAfter, (byte)(isCrit ? 1 : 0)));
            }

            public static bool TryRead(in RecordEvent e, out int sourceActorId, out int targetActorId, out int skillCode, out int damageAmount, out int hpBefore, out int hpAfter, out byte critFlag)
            {
                if (e.EventType != BattleLogEventTypes.Damage)
                {
                    sourceActorId = 0;
                    targetActorId = 0;
                    skillCode = 0;
                    damageAmount = 0;
                    hpBefore = 0;
                    hpAfter = 0;
                    critFlag = 0;
                    return false;
                }

                Decode(e.Payload, out sourceActorId, out targetActorId, out skillCode, out damageAmount, out hpBefore, out hpAfter, out critFlag);
                return true;
            }

            public readonly struct Payload
            {
                [BinaryMember(0)] public readonly int SourceActorId;
                [BinaryMember(1)] public readonly int TargetActorId;
                [BinaryMember(2)] public readonly int SkillCode;
                [BinaryMember(3)] public readonly int DamageAmount;
                [BinaryMember(4)] public readonly int HpBefore;
                [BinaryMember(5)] public readonly int HpAfter;
                [BinaryMember(6)] public readonly byte CritFlag;

                public Payload(int sourceActorId, int targetActorId, int skillCode, int damageAmount, int hpBefore, int hpAfter, byte critFlag)
                {
                    SourceActorId = sourceActorId;
                    TargetActorId = targetActorId;
                    SkillCode = skillCode;
                    DamageAmount = damageAmount;
                    HpBefore = hpBefore;
                    HpAfter = hpAfter;
                    CritFlag = critFlag;
                }
            }
        }

        private sealed class BattleLogReplayHandler : IReplayEventHandler
        {
            public void Handle(in RecordEvent e)
            {
                if (BattleSkillCastEventCodec.TryRead(in e, out var caster, out var skillCode, out var castInstanceId))
                {
                    Debug.Log($"[BattleLog] frame={e.Frame.Value} cast caster={caster} skillCode={skillCode} castInstanceId={castInstanceId}");
                    return;
                }

                if (BattleBuffAddEventCodec.TryRead(in e, out var src, out var tar, out var buffCode, out var buffInstanceId, out var stacks, out var durFrames))
                {
                    Debug.Log($"[BattleLog] frame={e.Frame.Value} buff.add src={src} tar={tar} buffCode={buffCode} buffInstanceId={buffInstanceId} stacks={stacks} durFrames={durFrames}");
                    return;
                }

                if (BattleDamageEventCodec.TryRead(in e, out var s2, out var t2, out var skill2, out var dmg, out var hpBefore, out var hpAfter, out var critFlag))
                {
                    Debug.Log($"[BattleLog] frame={e.Frame.Value} damage src={s2} tar={t2} skillCode={skill2} dmg={dmg} hp={hpBefore}->{hpAfter} crit={(critFlag != 0 ? 1 : 0)}");
                }
            }
        }

        [ContextMenu("Run Battle Log Record Sample")]
        public void Run()
        {
            var profile = new RecordProfile
            {
                EnableInputs = false,
                EnableStateHash = false,
                EnableSnapshots = false,
            };

            var serializer = new JsonRecordContainerSerializer();
            var trackFactory = new DefaultRecordTrackFactory();

            using var session = new RecordSession(
                profile,
                container: null,
                serializer,
                writerFactory: trackFactory,
                readerFactory: trackFactory);

            var trackId = RecordTrackId.FromName("battle.log.sample");
            if (!session.TryGetWriter(trackId, out var writer) || writer == null)
            {
                Debug.LogError("[BattleLogRecordSample] Failed to get track writer");
                return;
            }

            BattleSkillCastEventCodec.Write(writer, new FrameIndex(10), casterActorId: 1001, skillCode: 20001, castInstanceId: 1);
            BattleBuffAddEventCodec.Write(writer, new FrameIndex(11), sourceActorId: 1001, targetActorId: 1002, buffCode: 30001, buffInstanceId: 101, stacks: 1, durationFrames: 60);
            BattleDamageEventCodec.Write(writer, new FrameIndex(12), sourceActorId: 1001, targetActorId: 1002, skillCode: 20001, damageAmount: 88, hpBefore: 500, hpAfter: 412, isCrit: false);

            var bytes = session.Serialize();

            using var replaySession = new RecordSession(
                profile,
                container: null,
                serializer,
                writerFactory: trackFactory,
                readerFactory: trackFactory);

            if (!replaySession.TryLoad(bytes))
            {
                Debug.LogError("[BattleLogRecordSample] Failed to load serialized record container");
                return;
            }

            if (!replaySession.TryGetReader(trackId, out var reader) || reader == null)
            {
                Debug.LogError("[BattleLogRecordSample] Failed to get track reader");
                return;
            }

            var handler = new BattleLogReplayHandler();

            var clock = new FixedStepReplayClock(fixedDelta: 1f);
            clock.Reset(new FrameIndex(9));

            var controller = new BasicReplayController(clock, reader, handler);
            controller.Play();
            controller.Tick(1f);
            controller.Tick(1f);
            controller.Tick(1f);
        }
    }
}
