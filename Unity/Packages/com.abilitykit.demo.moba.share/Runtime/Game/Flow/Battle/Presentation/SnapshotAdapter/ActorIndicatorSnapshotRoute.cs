using System;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// 本地和远程共用的只读状态快照路由。
    /// </summary>
    public static class ActorIndicatorSnapshotRoute
    {
        public const int OpCode = MobaOpCodes.Snapshot.ActorIndicatorState;

        /// <summary>
        /// 空字节表示没有载荷，编码后的空集合表示清除。
        /// </summary>
        public static bool TryDecode(in WorldStateSnapshot snapshot, out ActorIndicatorStateData[] states)
        {
            states = null;
            if (snapshot.Payload == null || snapshot.Payload.Length == 0) return false;
            states = ActorIndicatorSnapshotDecoder.Decode(snapshot.Payload);
            return true;
        }

        /// <summary>
        /// 注册到现行快照解码器，不引入 Unity 直连玩法入口。
        /// </summary>
        public static void RegisterDecoder(ISnapshotDecoderRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            registry.RegisterDecoder<ActorIndicatorStateData[]>(OpCode, TryDecode);
        }
    }
}
