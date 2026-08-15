#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;

namespace AbilityKit.Demo.Shooter.View
{
    /// <summary>
    /// 创建与请求的 <see cref="NetworkSyncModel"/> 匹配的 <see cref="IShooterClientSyncController"/>。
    /// 这是 Shooter 会话选择同步策略的唯一入口。新模型（权威插值、批量状态同步等）
    /// 只需接入这里，不需要改动会话门面。
    /// </summary>
    public static class ShooterClientSyncControllerFactory
    {
        public const NetworkSyncModel DefaultSyncModel = NetworkSyncModel.PredictRollback;

        private static readonly NetworkSyncProfileControllerRegistry<IShooterClientSyncController, ShooterClientSyncControllerFactoryContext> Registry =
            new NetworkSyncProfileControllerRegistry<IShooterClientSyncController, ShooterClientSyncControllerFactoryContext>(CreateDefaultBuilders());

        public delegate IShooterClientSyncController ShooterClientSyncControllerBuilder(
            in ShooterClientSyncControllerFactoryContext context);

        public static void Register(
            NetworkSyncModel syncModel,
            ShooterClientSyncControllerBuilder builder)
        {
            Register(NetworkSyncProfileRegistry.Resolve(syncModel), builder);
        }

        public static void Register(
            in NetworkSyncProfile syncProfile,
            ShooterClientSyncControllerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            Registry.Register(syncProfile, (in ShooterClientSyncControllerFactoryContext context) => builder(in context));
        }

        public static void ResetToDefaults()
        {
            Registry.ResetToDefaults();
        }

        public static IShooterClientSyncController Create(
            NetworkSyncModel syncModel,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            int tickRate,
            ShooterGatewaySnapshotDecoder? decoder,
            IShooterRoomGatewayClient? gateway)
        {
            return Create(syncModel, runtime, presentation, tickRate, decoder, gateway, interpolationConfig: null);
        }

        /// <summary>
        /// 创建同步控制器，并可选为 <see cref="NetworkSyncModel.AuthoritativeInterpolation"/> 模型提供
        /// <see cref="InterpolationConfig"/>。该配置会被不做插值的模型（例如预测回滚）忽略；
        /// 省略时插值模型会回退到 <see cref="InterpolationConfig.Default"/>。
        /// </summary>
        public static IShooterClientSyncController Create(
            NetworkSyncModel syncModel,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            int tickRate,
            ShooterGatewaySnapshotDecoder? decoder,
            IShooterRoomGatewayClient? gateway,
            InterpolationConfig? interpolationConfig)
        {
            return Create(
                new ShooterClientSyncAssemblyOptions(syncModel, decoder, interpolationConfig),
                runtime,
                presentation,
                tickRate,
                gateway);
        }

        public static IShooterClientSyncController Create(
            in NetworkSyncProfile syncProfile,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            int tickRate,
            ShooterGatewaySnapshotDecoder? decoder,
            IShooterRoomGatewayClient? gateway,
            InterpolationConfig? interpolationConfig = null)
        {
            return Create(
                new ShooterClientSyncAssemblyOptions(in syncProfile, decoder, interpolationConfig),
                runtime,
                presentation,
                tickRate,
                gateway);
        }

        public static IShooterClientSyncController Create(
            in ShooterClientSyncAssemblyOptions assemblyOptions,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            int tickRate,
            IShooterRoomGatewayClient? gateway)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));

            return CreateSession(
                in assemblyOptions,
                runtime,
                presentation,
                tickRate,
                gateway).Controller;
        }

        /// <summary>
        /// 在创建控制器前完成 Profile、能力和协议版本协商，并返回可供诊断的会话描述。
        /// </summary>
        public static NetworkSyncSessionBuildResult<IShooterClientSyncController> CreateSession(
            in ShooterClientSyncAssemblyOptions assemblyOptions,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            int tickRate,
            IShooterRoomGatewayClient? gateway)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));

            var context = new ShooterClientSyncControllerFactoryContext(
                assemblyOptions.SyncProfile,
                runtime,
                presentation,
                tickRate,
                assemblyOptions.Decoder,
                gateway,
                assemblyOptions.InterpolationConfig);
            var sessionOptions = new NetworkSyncSessionOptions
            {
                ProfileCatalog = assemblyOptions.ProfileCatalog,
                RequiredProfileName = assemblyOptions.ProfileName,
                RequiredProfile = assemblyOptions.SyncProfile,
                RequiredMinimumSchemaVersion = assemblyOptions.MinimumSchemaVersion,
                RequiredMaximumSchemaVersion = assemblyOptions.MaximumSchemaVersion,
                AvailableCapabilities = assemblyOptions.AvailableCapabilities,
                RemoteCapabilities = assemblyOptions.RemoteCapabilities,
                RemoteCapabilityPolicy = assemblyOptions.RemoteCapabilityPolicy,
                ControllerSubjectName = "Shooter 客户端同步控制器"
            };
            return new NetworkSyncSessionBuilder<IShooterClientSyncController, ShooterClientSyncControllerFactoryContext>(
                Registry,
                sessionOptions).Build(in context);
        }

        private static IReadOnlyDictionary<NetworkSyncProfile, NetworkSyncProfileControllerBuilder<IShooterClientSyncController, ShooterClientSyncControllerFactoryContext>> CreateDefaultBuilders()
        {
            var builders = new Dictionary<NetworkSyncProfile, NetworkSyncProfileControllerBuilder<IShooterClientSyncController, ShooterClientSyncControllerFactoryContext>>
            {
                [NetworkSyncProfiles.Unspecified] = CreatePredictRollbackController,
                [NetworkSyncProfiles.PredictRollback] = CreatePredictRollbackController,
                [NetworkSyncProfiles.AuthoritativeInterpolation] = CreateAuthoritativeInterpolationController,
                [NetworkSyncProfiles.BatchStateSync] = CreateAuthoritativeInterpolationController,
                [NetworkSyncProfiles.MassBattleLodSync] = CreateAuthoritativeInterpolationController,
                [NetworkSyncProfiles.HybridHeroPrediction] = CreateHybridHeroPredictionController
            };
            return builders;
        }

        private static IShooterClientSyncController CreatePredictRollbackController(
            in ShooterClientSyncControllerFactoryContext context)
        {
            return new ShooterClientPredictRollbackSyncController(
                context.Runtime,
                context.Presentation,
                context.TickRate,
                context.Decoder,
                context.Gateway);
        }

        private static IShooterClientSyncController CreateAuthoritativeInterpolationController(
            in ShooterClientSyncControllerFactoryContext context)
        {
            return new ShooterClientAuthoritativeInterpolationSyncController(
                context.Runtime,
                context.Presentation,
                context.TickRate,
                context.Decoder,
                context.Gateway,
                context.InterpolationConfig ?? InterpolationConfig.Default,
                context.SyncModel);
        }

        private static IShooterClientSyncController CreateHybridHeroPredictionController(
            in ShooterClientSyncControllerFactoryContext context)
        {
            return new ShooterClientHybridHeroPredictionSyncController(
                context.Runtime,
                context.Presentation,
                context.TickRate,
                context.Decoder,
                context.Gateway,
                context.InterpolationConfig ?? InterpolationConfig.Default);
        }
    }

    public readonly struct ShooterClientSyncControllerFactoryContext
    {
        public ShooterClientSyncControllerFactoryContext(
            NetworkSyncProfile syncProfile,
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            int tickRate,
            ShooterGatewaySnapshotDecoder? decoder,
            IShooterRoomGatewayClient? gateway,
            InterpolationConfig? interpolationConfig)
        {
            SyncProfile = syncProfile;
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            Presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            TickRate = tickRate;
            Decoder = decoder;
            Gateway = gateway;
            InterpolationConfig = interpolationConfig;
        }

        public NetworkSyncProfile SyncProfile { get; }

        public NetworkSyncModel SyncModel => SyncProfile.CompatibilityModel;

        public IShooterBattleRuntimePort Runtime { get; }

        public ShooterPresentationFacade Presentation { get; }

        public int TickRate { get; }

        public ShooterGatewaySnapshotDecoder? Decoder { get; }

        public IShooterRoomGatewayClient? Gateway { get; }

        public InterpolationConfig? InterpolationConfig { get; }
    }
}
