using System;
using System.Collections.Generic;
using System.IO;

namespace AbilityKit.Ability.Behavior
{
    /// <summary>
    /// 行为运行时的可选纯状态恢复；不包含 Decision 所属快照和运行依赖。
    /// </summary>
    public partial class BehaviorRuntime
    {
        /// <summary>
        /// 独立备份当前存活行为的完整核心值；不支持字段在此处显式诊断。
        /// </summary>
        public byte[] CaptureRecoveryState()
        {
            if ((Phase != BehaviorPhase.Running && Phase != BehaviorPhase.Paused) || CurrentFrame < 0 || _elapsedRaw < 0)
                throw new InvalidOperationException("Behavior rollback requires a surviving running or paused runtime.");
            return BehaviorRecoveryCodec.Write(writer =>
            {
                writer.Write(1); writer.Write((int)Phase); writer.Write(CurrentFrame); writer.Write(_elapsedRaw);
                writer.Write(_requestComplete); BehaviorRecoveryCodec.Text(writer, _interruptReason);
                BehaviorRecoveryCodec.Map(writer, ((BehaviorState)State).RecoveryValues);
                ((BehaviorOutput)Output).WriteRecovery(writer);
                BehaviorRecoveryCodec.Map(writer, Config);
            });
        }

        /// <summary>
        /// 只读验证完整核心载荷及冻结配置，不调用业务生命周期。
        /// </summary>
        public void ValidateRecoveryState(byte[] payload) => ParseRecovery(payload);

        /// <summary>
        /// 完成整份预检后静默替换值状态；保留 State、Output 和运行依赖实例。
        /// </summary>
        public void RestoreRecoveryState(byte[] payload)
        {
            var value = ParseRecovery(payload);
            Phase = value.Phase; CurrentFrame = value.Frame; _elapsedRaw = value.Elapsed;
            _requestComplete = value.Complete; _interruptReason = value.Reason;
            ((BehaviorState)State).RestoreValues(value.Values);
            ((BehaviorOutput)Output).RestoreValues(value.Output);
        }

        private CoreRecovery ParseRecovery(byte[] payload) => BehaviorRecoveryCodec.Read(payload, reader =>
        {
            if (reader.ReadInt32() != 1) throw new InvalidOperationException("Unsupported behavior core version.");
            var value = new CoreRecovery
            {
                Phase = (BehaviorPhase)reader.ReadInt32(), Frame = reader.ReadInt64(), Elapsed = reader.ReadInt64(),
                Complete = BehaviorRecoveryCodec.Boolean(reader), Reason = BehaviorRecoveryCodec.Text(reader),
                Values = BehaviorRecoveryCodec.Map(reader), Output = BehaviorOutput.ReadRecovery(reader)
            };
            var config = BehaviorRecoveryCodec.Map(reader);
            var expected = BehaviorRecoveryCodec.Write(w => BehaviorRecoveryCodec.Map(w, Config));
            var actual = BehaviorRecoveryCodec.Write(w => BehaviorRecoveryCodec.Map(w, config));
            if ((Phase != BehaviorPhase.Running && Phase != BehaviorPhase.Paused) ||
                (value.Phase != BehaviorPhase.Running && value.Phase != BehaviorPhase.Paused) ||
                value.Frame < 0 || value.Elapsed < 0 || value.Values == null || !expected.AsSpan().SequenceEqual(actual))
                throw new InvalidOperationException("Invalid behavior core state or incompatible frozen configuration.");
            return value;
        });

        /// <summary>
        /// 已解析并通过验证的核心值，不持有运行依赖。
        /// </summary>
        private sealed class CoreRecovery
        {
            internal BehaviorPhase Phase;
            internal long Frame, Elapsed;
            internal bool Complete;
            internal string Reason;
            internal Dictionary<string, object> Values;
            internal BehaviorOutput Output;
        }
    }

    /// <summary>
    /// State 的恢复扩展只暴露内部值视图，不改变普通 Set 的允许类型。
    /// </summary>
    public partial class BehaviorState
    {
        internal IReadOnlyDictionary<string, object> RecoveryValues => _data;

        internal void RestoreValues(Dictionary<string, object> values)
        {
            _data.Clear();
            foreach (var pair in values) _data.Add(pair.Key, pair.Value);
        }
    }

    /// <summary>
    /// 输出的当前值恢复；不提交或补发列表中的任何通知。
    /// </summary>
    public partial class BehaviorOutput
    {
        internal void WriteRecovery(BinaryWriter writer)
        {
            writer.Write(ShouldComplete); writer.Write(ShouldInterrupt); BehaviorRecoveryCodec.Text(writer, InterruptReason);
            writer.Write(Movement.HasValue);
            if (Movement.HasValue)
            {
                var m = Movement.Value;
                writer.Write(m.TargetPosition.HasValue);
                if (m.TargetPosition.HasValue) BehaviorRecoveryCodec.Vector(writer, m.TargetPosition.Value);
                writer.Write(m.TargetEntity.HasValue);
                if (m.TargetEntity.HasValue) writer.Write(m.TargetEntity.Value.Value);
                if (float.IsNaN(m.Speed) || float.IsInfinity(m.Speed)) throw new InvalidOperationException("Invalid behavior movement speed.");
                writer.Write(m.Speed);
            }
            BehaviorRecoveryCodec.Count(writer, _events.Count);
            foreach (var e in _events) { writer.Write(e.EventId); BehaviorRecoveryCodec.Map(writer, e.Payload); }
            BehaviorRecoveryCodec.Count(writer, _effects.Count);
            foreach (var e in _effects)
            {
                writer.Write(e.EffectId); writer.Write(e.TargetId.HasValue);
                if (e.TargetId.HasValue) writer.Write(e.TargetId.Value.Value);
                BehaviorRecoveryCodec.Map(writer, e.Params);
            }
        }

        internal static BehaviorOutput ReadRecovery(BinaryReader reader)
        {
            var output = new BehaviorOutput
            {
                ShouldComplete = BehaviorRecoveryCodec.Boolean(reader), ShouldInterrupt = BehaviorRecoveryCodec.Boolean(reader),
                InterruptReason = BehaviorRecoveryCodec.Text(reader)
            };
            if (BehaviorRecoveryCodec.Boolean(reader))
            {
                var position = BehaviorRecoveryCodec.Boolean(reader) ? BehaviorRecoveryCodec.Vector(reader) : (AbilityKit.Core.Mathematics.Vec3?)null;
                var target = BehaviorRecoveryCodec.Boolean(reader) ? new BehaviorEntityId(reader.ReadInt64()) : (BehaviorEntityId?)null;
                output.SetMovement(position, target, BehaviorRecoveryCodec.Float(reader));
            }
            var count = BehaviorRecoveryCodec.Count(reader);
            for (var i = 0; i < count; i++) output.AddEvent(reader.ReadString(), BehaviorRecoveryCodec.Map(reader));
            count = BehaviorRecoveryCodec.Count(reader);
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadString();
                var target = BehaviorRecoveryCodec.Boolean(reader) ? new BehaviorEntityId(reader.ReadInt64()) : (BehaviorEntityId?)null;
                output.AddEffect(id, target, BehaviorRecoveryCodec.Map(reader));
            }
            return output;
        }

        internal void RestoreValues(BehaviorOutput value)
        {
            Clear(); ShouldComplete = value.ShouldComplete; ShouldInterrupt = value.ShouldInterrupt;
            InterruptReason = value.InterruptReason; Movement = value.Movement;
            _events.AddRange(value._events); _effects.AddRange(value._effects);
        }
    }
}
