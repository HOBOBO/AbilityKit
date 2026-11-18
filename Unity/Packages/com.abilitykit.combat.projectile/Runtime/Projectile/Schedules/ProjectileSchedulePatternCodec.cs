using System;
using System.IO;

namespace AbilityKit.Combat.Projectile
{
    /// <summary>
    /// 编解码内置冻结发射模式，不执行 Build 或消耗随机源。
    /// </summary>
    internal static class ProjectileSchedulePatternCodec
    {
        internal static ProjectileSchedulePatternSnapshot Capture(object pattern)
        {
            if (pattern is IProjectileSchedulePatternSnapshotSource source)
                return source.CaptureSchedulePattern();
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(1);
                int kind;
                switch (pattern)
                {
                    case SingleShotPattern _: kind = 1; break;
                    case BurstPattern burst: kind = 2; writer.Write(burst.Count); break;
                    case FanPattern fan: kind = 3; writer.Write(fan.Count); writer.Write(fan.TotalAngleDeg); break;
                    case ScatterPattern scatter:
                        kind = 4; writer.Write(scatter.Count); writer.Write(scatter.MaxAngleDeg); writer.Write(scatter.Seed); break;
                    default: throw new InvalidOperationException("Scheduled projectile pattern does not provide rollback data.");
                }
                return new ProjectileSchedulePatternSnapshot(kind, stream.ToArray());
            }
        }

        internal static bool IsBuiltIn(int kind) => kind >= 1 && kind <= 4;

        internal static void Validate(in ProjectileSchedulePatternSnapshot snapshot, bool isProvider)
        {
            if (snapshot.Data == null || snapshot.Data.Length == 0 || snapshot.Kind <= 0 ||
                !IsBuiltIn(snapshot.Kind) && snapshot.Kind < 1024)
                throw new InvalidOperationException("Invalid scheduled projectile pattern descriptor.");
            if (!IsBuiltIn(snapshot.Kind)) return;
            if (isProvider) throw new InvalidOperationException("Built-in projectile patterns cannot be dynamic providers.");
            using (var reader = new BinaryReader(new MemoryStream(snapshot.Data, false)))
            {
                if (reader.ReadInt32() != 1) throw new InvalidOperationException("Unsupported projectile pattern data version.");
                if (snapshot.Kind > 1)
                {
                    var count = reader.ReadInt32();
                    if (count <= 0 || snapshot.Kind > 2 && count > 1024)
                        throw new InvalidOperationException("Invalid projectile pattern count.");
                }
                if (snapshot.Kind > 2)
                {
                    var angle = reader.ReadSingle();
                    if (float.IsNaN(angle) || float.IsInfinity(angle) || snapshot.Kind == 4 && angle < 0)
                        throw new InvalidOperationException("Invalid projectile pattern angle.");
                }
                if (snapshot.Kind == 4) reader.ReadUInt32();
                if (reader.BaseStream.Position != reader.BaseStream.Length)
                    throw new InvalidOperationException("Unexpected projectile pattern data.");
            }
        }

        internal static IProjectileSpawnPattern Restore(in ProjectileSchedulePatternSnapshot snapshot)
        {
            using (var reader = new BinaryReader(new MemoryStream(snapshot.Data, false)))
            {
                reader.ReadInt32();
                switch (snapshot.Kind)
                {
                    case 1: return new SingleShotPattern();
                    case 2: return new BurstPattern(reader.ReadInt32());
                    case 3: return new FanPattern(reader.ReadInt32(), reader.ReadSingle());
                    case 4: return new ScatterPattern(reader.ReadInt32(), reader.ReadSingle(), reader.ReadInt32());
                    default: throw new InvalidOperationException("Not a built-in projectile pattern.");
                }
            }
        }
    }
}
