#nullable enable

using System;
using AbilityKit.Protocol.Catalog;
using AbilityKit.Protocol.Generated;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;
using MemoryPack;

namespace AbilityKit.Protocol.Moba
{
    /// <summary>
    /// 注册 MOBA 战斗协议包的诊断负载解码器。
    /// </summary>
    public static class MobaProtocolDecoderModule
    {
        /// <summary>
        /// MOBA 战斗协议目录标识。
        /// </summary>
        public const string CatalogId = "abilitykit.moba.battle";

        /// <summary>
        /// 注册生成目录中的解码器和单独维护的帧同步诊断入口；保留已注册的解码器。
        /// </summary>
        public static void Register(ProtocolPayloadDecoderRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            Register<WireSubmitFrameInputReq>(registry, "frame-sync-submit-input.request");
            Register<WireSubmitFrameInputRes>(registry, "frame-sync-submit-input.response");
            registry.TryRegister(CatalogId, "frame-sync-metrics.request", payload => payload.ToArray());
            Register<WireFrameSyncMetrics>(registry, "frame-sync-metrics.response");
            ProjectMemoryPackCodecs.Register(registry);
        }

        private static void Register<T>(ProtocolPayloadDecoderRegistry registry, string messageId)
        {
            registry.TryRegister(CatalogId, messageId, payload =>
            {
                if (payload.Array == null || payload.Count == 0) return default(T);
                return MemoryPackSerializer.Deserialize<T>(
                    new ReadOnlySpan<byte>(payload.Array, payload.Offset, payload.Count));
            });
        }
    }
}
