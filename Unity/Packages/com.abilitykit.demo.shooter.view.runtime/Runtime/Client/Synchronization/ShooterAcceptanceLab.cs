#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View.PlayMode;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Demo.Harness;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    /// <summary>
    /// 验收验证可选择的同步模式。封装 <see cref="NetworkSyncModel"/> 与面向人的显示名。
    /// Unity 外壳无需了解框架内部即可展示支持的同步策略。
    /// </summary>
    public readonly struct ShooterAcceptanceSyncOption
    {
        public ShooterAcceptanceSyncOption(NetworkSyncModel model, string displayName, bool implemented)
        {
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.", nameof(displayName));

            Model = model;
            DisplayName = displayName;
            Implemented = implemented;
        }

        /// <summary>该选项选择的框架无关同步模型。</summary>
        public NetworkSyncModel Model { get; }

        /// <summary>显示在 Unity 验收外壳中的标签。</summary>
        public string DisplayName { get; }

        /// <summary>
        /// 当 Shooter 客户端已经落地该模型对应控制器时为 true。Unity 外壳可将未实现选项置灰。
        /// 使用未实现模型调用 <see cref="ShooterAcceptanceLab"/> 会抛出异常。
        /// </summary>
        public bool Implemented { get; }

        /// <summary>该模型对应的完整框架同步档案（播放、快照、校验策略）。</summary>
        public NetworkSyncProfile Profile => NetworkSyncProfiles.FromCompatibilityModel(Model);
    }

    /// <summary>
    /// 验收验证可选择的模拟网络环境。封装 <see cref="NetworkConditionProfile"/> 预设、稳定 id 与标签。
    /// </summary>
    public readonly struct ShooterAcceptanceNetworkOption
    {
        public ShooterAcceptanceNetworkOption(string id, string displayName, NetworkConditionProfile profile)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.", nameof(displayName));

            Id = id;
            DisplayName = displayName;
            Profile = profile;
        }

        /// <summary>稳定标识，适合持久化或命令行选择。</summary>
        public string Id { get; }

        /// <summary>显示在 Unity 验收外壳中的标签。</summary>
        public string DisplayName { get; }

        /// <summary>该选项应用的模拟网络条件。</summary>
        public NetworkConditionProfile Profile { get; }
    }

    public enum ShooterSyncTemplateConvergenceKind
    {
        RuntimeSnapshot,
        PresentationInterpolation,
        RuntimeSnapshotWithRemoteInterpolation,
        BatchedPresentation,
        MassBattleLodPresentation
    }

    public enum ShooterSyncTemplateStatus
    {
        Implemented,
        Experimental,
        Reserved
    }

    public readonly struct ShooterSyncTemplateSendPolicy
    {
        public ShooterSyncTemplateSendPolicy(
            int snapshotIntervalFrames,
            int batchWindowFrames,
            int keyFrameIntervalFrames,
            int maxEntityCount,
            int activeEntityBudget,
            float aoiRadius,
            int nearLodIntervalFrames,
            int midLodIntervalFrames,
            int farLodIntervalFrames,
            int interpolationDelayFrames)
            : this(
                snapshotIntervalFrames,
                batchWindowFrames,
                keyFrameIntervalFrames,
                maxEntityCount,
                activeEntityBudget,
                aoiRadius,
                aoiRadius,
                nearLodIntervalFrames,
                midLodIntervalFrames,
                farLodIntervalFrames,
                interpolationDelayFrames)
        {
        }

        public ShooterSyncTemplateSendPolicy(
            int snapshotIntervalFrames,
            int batchWindowFrames,
            int keyFrameIntervalFrames,
            int maxEntityCount,
            int activeEntityBudget,
            float aoiRadius,
            float aoiBoundaryRadius,
            int nearLodIntervalFrames,
            int midLodIntervalFrames,
            int farLodIntervalFrames,
            int interpolationDelayFrames)
        {
            SnapshotIntervalFrames = snapshotIntervalFrames < 1 ? 1 : snapshotIntervalFrames;
            BatchWindowFrames = batchWindowFrames < 1 ? 1 : batchWindowFrames;
            KeyFrameIntervalFrames = keyFrameIntervalFrames < 1 ? SnapshotIntervalFrames : keyFrameIntervalFrames;
            MaxEntityCount = maxEntityCount < 1 ? 1 : maxEntityCount;
            ActiveEntityBudget = activeEntityBudget < 1 ? 1 : activeEntityBudget;
            AoiRadius = aoiRadius < 0f ? 0f : aoiRadius;
            AoiBoundaryRadius = aoiBoundaryRadius < AoiRadius ? AoiRadius : aoiBoundaryRadius;
            NearLodIntervalFrames = nearLodIntervalFrames < 1 ? 1 : nearLodIntervalFrames;
            MidLodIntervalFrames = midLodIntervalFrames < 1 ? NearLodIntervalFrames : midLodIntervalFrames;
            FarLodIntervalFrames = farLodIntervalFrames < 1 ? MidLodIntervalFrames : farLodIntervalFrames;
            InterpolationDelayFrames = interpolationDelayFrames < 0 ? 0 : interpolationDelayFrames;
        }

        public int SnapshotIntervalFrames { get; }
        public int BatchWindowFrames { get; }
        public int KeyFrameIntervalFrames { get; }
        public int MaxEntityCount { get; }
        public int ActiveEntityBudget { get; }
        public float AoiRadius { get; }
        public float AoiBoundaryRadius { get; }
        public int NearLodIntervalFrames { get; }
        public int MidLodIntervalFrames { get; }
        public int FarLodIntervalFrames { get; }
        public int InterpolationDelayFrames { get; }

        public ShooterPureStateSyncSettings ToPureStateSettings()
        {
            return new ShooterPureStateSyncSettings(
                MaxEntityCount,
                ActiveEntityBudget,
                KeyFrameIntervalFrames,
                SnapshotIntervalFrames,
                FarLodIntervalFrames,
                InterpolationDelayFrames);
        }

        public static ShooterSyncTemplateSendPolicy Realtime { get; } = new ShooterSyncTemplateSendPolicy(
            snapshotIntervalFrames: 1,
            batchWindowFrames: 1,
            keyFrameIntervalFrames: 60,
            maxEntityCount: 10000,
            activeEntityBudget: 512,
            aoiRadius: 0f,
            aoiBoundaryRadius: 0f,
            nearLodIntervalFrames: 1,
            midLodIntervalFrames: 2,
            farLodIntervalFrames: 15,
            interpolationDelayFrames: 3);

        public static ShooterSyncTemplateSendPolicy LowFrequencyBatch { get; } = new ShooterSyncTemplateSendPolicy(
            snapshotIntervalFrames: 60,
            batchWindowFrames: 60,
            keyFrameIntervalFrames: 300,
            maxEntityCount: 10000,
            activeEntityBudget: 1024,
            aoiRadius: 0f,
            aoiBoundaryRadius: 0f,
            nearLodIntervalFrames: 15,
            midLodIntervalFrames: 30,
            farLodIntervalFrames: 60,
            interpolationDelayFrames: 60);

        public static ShooterSyncTemplateSendPolicy MassBattleLod { get; } = new ShooterSyncTemplateSendPolicy(
            snapshotIntervalFrames: 90,
            batchWindowFrames: 90,
            keyFrameIntervalFrames: 450,
            maxEntityCount: 20000,
            activeEntityBudget: 2048,
            aoiRadius: 24f,
            aoiBoundaryRadius: 30f,
            nearLodIntervalFrames: 10,
            midLodIntervalFrames: 30,
            farLodIntervalFrames: 90,
            interpolationDelayFrames: 90);
    }

    public readonly struct ShooterSyncAcceptanceCriterion
    {
        public ShooterSyncAcceptanceCriterion(string id, string description)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required.", nameof(description));

            Id = id;
            Description = description;
        }

        public string Id { get; }
        public string Description { get; }
    }

    public readonly struct ShooterSyncModeMatrixRow
    {
        public ShooterSyncModeMatrixRow(
            ShooterSyncTemplate template,
            NetworkSyncProfile profile,
            IReadOnlyList<ShooterSyncAcceptanceCriterion> acceptanceCriteria)
        {
            Template = template;
            Profile = profile;
            AcceptanceCriteria = acceptanceCriteria ?? throw new ArgumentNullException(nameof(acceptanceCriteria));
        }

        public ShooterSyncTemplate Template { get; }
        public NetworkSyncProfile Profile { get; }
        public IReadOnlyList<ShooterSyncAcceptanceCriterion> AcceptanceCriteria { get; }
        public string TemplateId => Template.Id;
        public NetworkSyncModel SyncModel => Template.SyncModel;
        public string ExpectedCarrierName => Template.ExpectedCarrierName;
        public ShooterSyncTemplateConvergenceKind ConvergenceKind => Template.ConvergenceKind;
        public bool RequiresAuthoritativeWorld => Template.EnableAuthoritativeWorld;
        public bool ExposesInterpolationDiagnostics => Template.ExpectsInterpolationDiagnostics;
        public ShooterSyncTemplateStatus Status => Template.Status;
        public bool IsRunnable => Template.IsRunnable;
        public ShooterSyncTemplateSendPolicy SendPolicy => Template.SendPolicy;
    }

    public readonly struct ShooterSyncModeMatrix
    {
        public ShooterSyncModeMatrix(IReadOnlyList<ShooterSyncModeMatrixRow> rows)
        {
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
        }

        public IReadOnlyList<ShooterSyncModeMatrixRow> Rows { get; }

        public ShooterSyncModeMatrixRow GetRow(string templateId)
        {
            if (string.IsNullOrWhiteSpace(templateId)) throw new ArgumentException("Template id is required.", nameof(templateId));

            for (var i = 0; i < Rows.Count; i++)
            {
                if (string.Equals(Rows[i].TemplateId, templateId, StringComparison.OrdinalIgnoreCase))
                {
                    return Rows[i];
                }
            }

            throw new KeyNotFoundException($"Shooter sync matrix row '{templateId}' was not found.");
        }
    }

    public readonly struct ShooterSyncTemplate
    {
        public ShooterSyncTemplate(
            string id,
            string displayName,
            string description,
            NetworkSyncModel syncModel,
            string networkEnvironmentId,
            string expectedCarrierName,
            int recommendedPlayerCount,
            bool enableAuthoritativeWorld,
            bool expectsInterpolationDiagnostics,
            ShooterSyncTemplateConvergenceKind convergenceKind,
            InterpolationConfig interpolationConfig)
            : this(
                id,
                displayName,
                description,
                syncModel,
                networkEnvironmentId,
                expectedCarrierName,
                recommendedPlayerCount,
                enableAuthoritativeWorld,
                expectsInterpolationDiagnostics,
                convergenceKind,
                interpolationConfig,
                ShooterSyncTemplateSendPolicy.Realtime,
                ShooterSyncTemplateStatus.Implemented)
        {
        }

        public ShooterSyncTemplate(
            string id,
            string displayName,
            string description,
            NetworkSyncModel syncModel,
            string networkEnvironmentId,
            string expectedCarrierName,
            int recommendedPlayerCount,
            bool enableAuthoritativeWorld,
            bool expectsInterpolationDiagnostics,
            ShooterSyncTemplateConvergenceKind convergenceKind,
            InterpolationConfig interpolationConfig,
            ShooterSyncTemplateSendPolicy sendPolicy,
            ShooterSyncTemplateStatus status)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.", nameof(displayName));
            if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required.", nameof(description));
            if (string.IsNullOrWhiteSpace(networkEnvironmentId)) throw new ArgumentException("Network environment id is required.", nameof(networkEnvironmentId));
            if (string.IsNullOrWhiteSpace(expectedCarrierName)) throw new ArgumentException("Expected carrier name is required.", nameof(expectedCarrierName));

            Id = id;
            DisplayName = displayName;
            Description = description;
            SyncModel = syncModel;
            NetworkEnvironmentId = networkEnvironmentId;
            ExpectedCarrierName = expectedCarrierName;
            RecommendedPlayerCount = recommendedPlayerCount < 1 ? 1 : recommendedPlayerCount;
            EnableAuthoritativeWorld = enableAuthoritativeWorld;
            ExpectsInterpolationDiagnostics = expectsInterpolationDiagnostics;
            ConvergenceKind = convergenceKind;
            InterpolationConfig = interpolationConfig;
            SendPolicy = sendPolicy;
            Status = status;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public NetworkSyncModel SyncModel { get; }
        public string NetworkEnvironmentId { get; }
        public string ExpectedCarrierName { get; }
        public int RecommendedPlayerCount { get; }
        public bool EnableAuthoritativeWorld { get; }
        public bool ExpectsInterpolationDiagnostics { get; }
        public ShooterSyncTemplateConvergenceKind ConvergenceKind { get; }
        public InterpolationConfig InterpolationConfig { get; }
        public ShooterSyncTemplateSendPolicy SendPolicy { get; }
        public ShooterSyncTemplateStatus Status { get; }
        public bool IsRunnable => Status != ShooterSyncTemplateStatus.Reserved;
    }

    public static class ShooterSyncTemplateIds
    {
        public const string StateSyncAuthority = "state-sync-authority";
        public const string PredictRollbackAuthority = "predict-rollback-authority";
        public const string AuthoritativeInterpolationPresentation = "authoritative-interpolation-presentation";
        public const string BatchStateLowFrequency = "batch-state-low-frequency";
        public const string MassBattleLodAoi = "mass-battle-lod-aoi";
        public const string HybridHeroPrediction = "hybrid-hero-prediction";
        public const string FastReconnectResume = "fast-reconnect-resume";
    }

    /// <summary>
    /// Shooter 验收提供的固定同步模式、网络环境与同步方案模板菜单。Unity 外壳直接绑定这些列表。
    /// 确保可选项与纯 C# 层实际可构建、可运行的能力保持一致。
    /// </summary>
    public static class ShooterAcceptanceCatalog
    {
        /// <summary>验收提供的同步模式，只有已实现模式可运行。</summary>
        public static IReadOnlyList<ShooterAcceptanceSyncOption> SyncModes { get; } = new[]
        {
            new ShooterAcceptanceSyncOption(NetworkSyncModel.PredictRollback, "Predict + Rollback", implemented: true),
            new ShooterAcceptanceSyncOption(NetworkSyncModel.AuthoritativeInterpolation, "Authoritative Interpolation", implemented: true),
            new ShooterAcceptanceSyncOption(NetworkSyncModel.BatchStateSync, "Batch State Sync", implemented: true),
            new ShooterAcceptanceSyncOption(NetworkSyncModel.MassBattleLodSync, "Mass Battle LOD Sync", implemented: true),
            new ShooterAcceptanceSyncOption(NetworkSyncModel.HybridHeroPrediction, "Hybrid (Predict + Interpolation)", implemented: true),
            new ShooterAcceptanceSyncOption(NetworkSyncModel.FastReconnect, "Fast Reconnect", implemented: false)
        };

        /// <summary>模拟网络环境，从理想基线到压力场景排序。</summary>
        public static IReadOnlyList<ShooterAcceptanceNetworkOption> NetworkEnvironments { get; } = new[]
        {
            new ShooterAcceptanceNetworkOption("ideal", "Ideal (0ms)", NetworkConditionProfile.Ideal),
            new ShooterAcceptanceNetworkOption("lan", "LAN (5ms)", NetworkConditionProfile.Lan),
            new ShooterAcceptanceNetworkOption("mobile4g", "Mobile 4G (60ms)", NetworkConditionProfile.Mobile4G),
            new ShooterAcceptanceNetworkOption("crossregion", "Cross Region (150ms)", NetworkConditionProfile.CrossRegion),
            new ShooterAcceptanceNetworkOption("poorwifi", "Poor WiFi (80ms, loss)", NetworkConditionProfile.PoorWifi),
            new ShooterAcceptanceNetworkOption("limitedbw", "Limited BW (128 Kbps)", NetworkConditionProfile.LimitedBandwidth)
        };

        public static IReadOnlyList<ShooterSyncTemplate> SyncTemplates { get; } = new[]
        {
            new ShooterSyncTemplate(
                ShooterSyncTemplateIds.StateSyncAuthority,
                "State Sync / Server Authority",
                "服务器权威状态快照驱动客户端插值播放，作为 Shooter 正式默认同步模板。",
                NetworkSyncModel.AuthoritativeInterpolation,
                "ideal",
                ShooterInterpolationDemoHarnessCarrier.DefaultCarrierName,
                recommendedPlayerCount: 2,
                enableAuthoritativeWorld: false,
                expectsInterpolationDiagnostics: true,
                ShooterSyncTemplateConvergenceKind.PresentationInterpolation,
                InterpolationConfig.Default),
            new ShooterSyncTemplate(
                ShooterSyncTemplateIds.PredictRollbackAuthority,
                "Predict Rollback / Authority Compare",
                "本地预测回滚，并保留权威世界用于漂移、回滚与最终快照收敛验证。",
                NetworkSyncModel.PredictRollback,
                "ideal",
                ShooterDemoHarnessCarrier.DefaultCarrierName,
                recommendedPlayerCount: 2,
                enableAuthoritativeWorld: true,
                expectsInterpolationDiagnostics: false,
                ShooterSyncTemplateConvergenceKind.RuntimeSnapshot,
                InterpolationConfig.Default),
            new ShooterSyncTemplate(
                ShooterSyncTemplateIds.AuthoritativeInterpolationPresentation,
                "Authoritative Interpolation / Remote Presentation",
                "客户端只播放权威远端样本，重点验证插值缓存、时间线与表现层播放稳定性。",
                NetworkSyncModel.AuthoritativeInterpolation,
                "lan",
                ShooterInterpolationDemoHarnessCarrier.DefaultCarrierName,
                recommendedPlayerCount: 2,
                enableAuthoritativeWorld: false,
                expectsInterpolationDiagnostics: true,
                ShooterSyncTemplateConvergenceKind.PresentationInterpolation,
                InterpolationConfig.Default),
            new ShooterSyncTemplate(
                ShooterSyncTemplateIds.BatchStateLowFrequency,
                "Batch State Sync / Low Frequency Playback",
                "服务端以低频批量快照合并实体状态，客户端延迟播放批次数据，适合数千单位的低实时性场景。",
                NetworkSyncModel.BatchStateSync,
                "mobile4g",
                ShooterInterpolationDemoHarnessCarrier.DefaultCarrierName,
                recommendedPlayerCount: 8,
                enableAuthoritativeWorld: false,
                expectsInterpolationDiagnostics: true,
                ShooterSyncTemplateConvergenceKind.BatchedPresentation,
                InterpolationConfig.Default,
                ShooterSyncTemplateSendPolicy.LowFrequencyBatch,
                ShooterSyncTemplateStatus.Experimental),
            new ShooterSyncTemplate(
                ShooterSyncTemplateIds.MassBattleLodAoi,
                "Mass Battle LOD / AOI Budget Playback",
                "面向上万单位的距离 AOI、优先级预算与 LOD 频率同步模板，复用纯状态低频导出参数。",
                NetworkSyncModel.MassBattleLodSync,
                "limitedbw",
                ShooterInterpolationDemoHarnessCarrier.DefaultCarrierName,
                recommendedPlayerCount: 16,
                enableAuthoritativeWorld: false,
                expectsInterpolationDiagnostics: true,
                ShooterSyncTemplateConvergenceKind.MassBattleLodPresentation,
                InterpolationConfig.Default,
                ShooterSyncTemplateSendPolicy.MassBattleLod,
                ShooterSyncTemplateStatus.Experimental),
            new ShooterSyncTemplate(
                ShooterSyncTemplateIds.HybridHeroPrediction,
                "Hybrid Hero Prediction / Remote Interpolation",
                "本地英雄预测回滚，远端对象权威插值，验证混合同步方案的双路径行为。",
                NetworkSyncModel.HybridHeroPrediction,
                "lan",
                ShooterHybridDemoHarnessCarrier.DefaultCarrierName,
                recommendedPlayerCount: 4,
                enableAuthoritativeWorld: true,
                expectsInterpolationDiagnostics: true,
                ShooterSyncTemplateConvergenceKind.RuntimeSnapshotWithRemoteInterpolation,
                InterpolationConfig.Default),
            new ShooterSyncTemplate(
                ShooterSyncTemplateIds.FastReconnectResume,
                "Fast Reconnect / Resume Snapshot",
                "预留给断线重连恢复流程的模板；当前目录展示状态，但不进入可运行矩阵。",
                NetworkSyncModel.FastReconnect,
                "crossregion",
                ShooterDemoHarnessCarrier.DefaultCarrierName,
                recommendedPlayerCount: 2,
                enableAuthoritativeWorld: true,
                expectsInterpolationDiagnostics: false,
                ShooterSyncTemplateConvergenceKind.RuntimeSnapshot,
                InterpolationConfig.Default,
                ShooterSyncTemplateSendPolicy.LowFrequencyBatch,
                ShooterSyncTemplateStatus.Reserved)
        };

        public static ShooterSyncModeMatrix SyncModeMatrix { get; } = BuildSyncModeMatrix();

        public static ShooterSyncTemplate GetSyncTemplate(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Template id is required.", nameof(id));

            for (var i = 0; i < SyncTemplates.Count; i++)
            {
                if (string.Equals(SyncTemplates[i].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return SyncTemplates[i];
                }
            }

            throw new KeyNotFoundException($"Shooter sync template '{id}' was not found.");
        }

        public static ShooterSyncModeMatrixRow GetSyncModeMatrixRow(string templateId)
        {
            return SyncModeMatrix.GetRow(templateId);
        }

        public static ShooterAcceptanceNetworkOption GetNetworkEnvironment(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Network environment id is required.", nameof(id));

            for (var i = 0; i < NetworkEnvironments.Count; i++)
            {
                if (string.Equals(NetworkEnvironments[i].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return NetworkEnvironments[i];
                }
            }

            throw new KeyNotFoundException($"Shooter network environment '{id}' was not found.");
        }

        private static ShooterSyncModeMatrix BuildSyncModeMatrix()
        {
            var rows = new ShooterSyncModeMatrixRow[SyncTemplates.Count];
            for (var i = 0; i < SyncTemplates.Count; i++)
            {
                var template = SyncTemplates[i];
                rows[i] = new ShooterSyncModeMatrixRow(
                    template,
                    NetworkSyncProfiles.FromCompatibilityModel(template.SyncModel),
                    BuildAcceptanceCriteria(in template));
            }

            return new ShooterSyncModeMatrix(rows);
        }

        private static IReadOnlyList<ShooterSyncAcceptanceCriterion> BuildAcceptanceCriteria(in ShooterSyncTemplate template)
        {
            switch (template.ConvergenceKind)
            {
                case ShooterSyncTemplateConvergenceKind.PresentationInterpolation:
                    return new[]
                    {
                        new ShooterSyncAcceptanceCriterion("remote-buffer", "权威远端样本必须进入延迟播放缓冲并拒绝过期样本。"),
                        new ShooterSyncAcceptanceCriterion("presentation-convergence", "表现层帧号、实体投影与插值诊断必须持续更新。"),
                        new ShooterSyncAcceptanceCriterion("server-authority", "客户端本地输入只作为占位预测，最终状态以服务器快照为准。")
                    };
                case ShooterSyncTemplateConvergenceKind.BatchedPresentation:
                    return new[]
                    {
                        new ShooterSyncAcceptanceCriterion("batch-window", "服务端发送策略必须声明批量窗口、快照间隔与关键帧间隔。"),
                        new ShooterSyncAcceptanceCriterion("delayed-playback", "客户端必须以模板延迟帧数播放低频批量状态。"),
                        new ShooterSyncAcceptanceCriterion("pure-state-settings", "模板必须能转换为纯状态同步导出参数。")
                    };
                case ShooterSyncTemplateConvergenceKind.MassBattleLodPresentation:
                    return new[]
                    {
                        new ShooterSyncAcceptanceCriterion("aoi-budget", "同步策略必须声明 AOI 半径、活跃实体预算与最大实体数。"),
                        new ShooterSyncAcceptanceCriterion("lod-frequency", "近/中/远 LOD 频率必须从模板中可读取并用于低频纯状态导出。"),
                        new ShooterSyncAcceptanceCriterion("delayed-lod-playback", "客户端必须以延迟播放方式消费大规模 LOD 批次。")
                    };
                case ShooterSyncTemplateConvergenceKind.RuntimeSnapshotWithRemoteInterpolation:
                    return new[]
                    {
                        new ShooterSyncAcceptanceCriterion("local-hero-prediction", "本地英雄路径必须支持预测、回滚快照与权威快照追帧。"),
                        new ShooterSyncAcceptanceCriterion("remote-interpolation", "远端对象必须通过权威样本插值播放。"),
                        new ShooterSyncAcceptanceCriterion("full-snapshot-recovery", "漂移、导入失败或重连后必须能进入完整快照恢复。")
                    };
                default:
                    return new[]
                    {
                        new ShooterSyncAcceptanceCriterion("prediction-reconciliation", "客户端预测世界必须与权威世界按帧比较并完成回滚收敛。"),
                        new ShooterSyncAcceptanceCriterion("packed-snapshot", "权威快照必须能覆盖运行时状态并刷新表现层。"),
                        new ShooterSyncAcceptanceCriterion("deterministic-replay", "相同输入与种子必须产出可复现的矩阵运行结果。")
                    };
            }
        }
    }

    /// <summary>
    /// 指定帧下客户端预测世界与权威世界的逐实体偏差。Unity 外壳可并排渲染两个世界，
    /// 并高亮 <see cref="Distance"/> 超过容差的实体，让预测误差可视化。
    /// </summary>
    public readonly struct ShooterWorldDivergence
    {
        public ShooterWorldDivergence(int playerId, float clientX, float clientY, float authorityX, float authorityY)
        {
            PlayerId = playerId;
            ClientX = clientX;
            ClientY = clientY;
            AuthorityX = authorityX;
            AuthorityY = authorityY;
            Distance = Math.Sqrt(((clientX - authorityX) * (clientX - authorityX))
                                 + ((clientY - authorityY) * (clientY - authorityY)));
        }

        public int PlayerId { get; }

        public float ClientX { get; }

        public float ClientY { get; }

        public float AuthorityX { get; }

        public float AuthorityY { get; }

        /// <summary>预测位置与权威位置之间的欧氏距离。</summary>
        public double Distance { get; }
    }

    /// <summary>
    /// 客户端预测世界相对权威世界漂移程度的快照。由 <see cref="ShooterAcceptanceSession.CompareWorlds"/> 生成。
    /// Unity 外壳读取 <see cref="MaxDistance"/> / <see cref="Divergences"/> 来驱动偏差叠加显示。
    /// </summary>
    public readonly struct ShooterWorldComparison
    {
        public ShooterWorldComparison(int clientFrame, int authorityFrame, IReadOnlyList<ShooterWorldDivergence> divergences)
        {
            ClientFrame = clientFrame;
            AuthorityFrame = authorityFrame;
            Divergences = divergences;

            var max = 0d;
            for (var i = 0; i < divergences.Count; i++)
            {
                if (divergences[i].Distance > max)
                {
                    max = divergences[i].Distance;
                }
            }

            MaxDistance = max;
        }

        public int ClientFrame { get; }

        public int AuthorityFrame { get; }

        public IReadOnlyList<ShooterWorldDivergence> Divergences { get; }

        /// <summary>本次比较中观测到的最大逐实体位置偏差。</summary>
        public double MaxDistance { get; }
    }

    internal readonly struct ShooterAuthoritySnapshotPublishOptions
    {
        public ShooterAuthoritySnapshotPublishOptions(ShooterSyncTemplateSendPolicy sendPolicy, ShooterSyncTemplateConvergenceKind convergenceKind)
        {
            SendPolicy = sendPolicy;
            ConvergenceKind = convergenceKind;
        }

        public ShooterSyncTemplateSendPolicy SendPolicy { get; }
        public ShooterSyncTemplateConvergenceKind ConvergenceKind { get; }
        public bool UsesPureStatePayload => ConvergenceKind == ShooterSyncTemplateConvergenceKind.BatchedPresentation || ConvergenceKind == ShooterSyncTemplateConvergenceKind.MassBattleLodPresentation;
        public bool UsesAoiScope => UsesPureStatePayload && SendPolicy.AoiRadius > 0f;

        public static ShooterAuthoritySnapshotPublishOptions RealtimePacked { get; } = new ShooterAuthoritySnapshotPublishOptions(
            ShooterSyncTemplateSendPolicy.Realtime,
            ShooterSyncTemplateConvergenceKind.RuntimeSnapshot);
    }

    /// <summary>
    /// 已完整装配、可运行的 Shooter 验收会话：包含 runtime port、表现门面、已选择同步控制器与 demo-harness carrier。
    /// 并已启动到可推进状态。同一个对象既可由 xUnit 无头驱动，也可由 Unity 外壳可视化驱动；
    /// 外壳读取 <see cref="Runtime"/> / <see cref="Presentation"/> 渲染已验证状态。
    /// 当 <see cref="HasAuthoritativeWorld"/> 为 true 时，会并行运行独立权威模拟，便于两个世界并排渲染与比较。
    /// </summary>
    public sealed class ShooterAcceptanceSession : IDisposable
    {
        public const int DefaultStepCount = 120;

        private NetworkConditionProfile _networkProfile;
        private string _networkName;

        private readonly ShooterBattleWorldSession _runtimeWorld;
        private readonly ShooterBattleWorldSession? _authoritativeWorldSession;
        private ShooterAuthoritativeComparisonDriver? _authoritativeDriver;
        private bool _disposed;

        internal ShooterAcceptanceSession(
            ShooterBattleWorldSession runtimeWorld,
            ShooterPresentationFacade presentation,
            IShooterClientSyncController controller,
            ISyncDemoCarrier carrier,
            NetworkSyncModel syncModel,
            NetworkConditionProfile networkProfile,
            string networkName,
            ShooterBattleWorldSession? authoritativeWorldSession,
            ShooterPresentationFacade? authoritativePresentation,
            ShooterAuthoritySnapshotPublishOptions authoritySnapshotPublishOptions,
            int networkSeed = 0)
        {
            _runtimeWorld = runtimeWorld ?? throw new ArgumentNullException(nameof(runtimeWorld));
            _authoritativeWorldSession = authoritativeWorldSession;
            Runtime = runtimeWorld.Runtime;
            Presentation = presentation;
            Controller = controller;
            Carrier = carrier;
            SyncModel = syncModel;
            _networkProfile = networkProfile;
            _networkName = networkName;
            AuthoritativeWorld = authoritativeWorldSession?.Runtime;
            AuthoritativePresentation = authoritativePresentation;
            if (AuthoritativeWorld != null)
            {
                _authoritativeDriver = new ShooterAuthoritativeComparisonDriver(
                    Controller,
                    AuthoritativeWorld,
                    AuthoritativePresentation,
                    _networkProfile,
                    authoritySnapshotPublishOptions,
                    networkSeed);
            }
        }

        /// <summary>控制器每步推进的确定性战斗运行时（客户端预测世界）。</summary>
        public ShooterBattleRuntimePort Runtime { get; }

        /// <summary>Unity 外壳用于渲染客户端预测会话的表现门面。</summary>
        public ShooterPresentationFacade Presentation { get; }

        /// <summary>当前选择的同步控制器（预测回滚、插值等）。</summary>
        public IShooterClientSyncController Controller { get; }

        /// <summary>将控制器桥接到框架 DemoHarness Carrier。</summary>
        public ISyncDemoCarrier Carrier { get; }

        public NetworkSyncModel SyncModel { get; }

        /// <summary>
        /// 当前生效的网络环境。可通过 <see cref="ApplyNetwork"/> 修改。
        /// Unity 外壳在会话持续推进时实时调节延迟、丢包与抖动。
        /// </summary>
        public NetworkConditionProfile NetworkProfile => _networkProfile;

        /// <summary>当前网络环境的可读标签。</summary>
        public string NetworkName => _networkName;

        /// <summary>存在用于并排比较的独立权威世界时为 true。</summary>
        public bool HasAuthoritativeWorld => AuthoritativeWorld != null;

        /// <summary>
        /// 可选的独立权威模拟（无预测，仅纯推进）。启动时未启用比较模式则为 null。
        /// Unity 外壳将其作为“真实基准”世界渲染。
        /// </summary>
        public ShooterBattleRuntimePort? AuthoritativeWorld { get; }

        /// <summary>
        /// 可选的权威世界表现门面。比较模式禁用时为 null。
        /// 让外壳可以复用现有 view binder 渲染第二个世界。
        /// </summary>
        public ShooterPresentationFacade? AuthoritativePresentation { get; }

        /// <summary>Carrier 侧网络中间件链路的实时统计。</summary>
        public NetworkConditioningStats? CarrierNetworkStats => _authoritativeDriver?.Stats;

        /// <summary>最近一次带网络条件的权威快照应用到控制器后的结果。</summary>
        public ShooterSnapshotApplyResult? LastCarrierSnapshotApplyResult => _authoritativeDriver?.LastApplyResult;

        /// <summary>最近一次通过 Carrier 链路发布权威快照时使用的时间锚点。</summary>
        public SyncTimeAnchor LastCarrierTimeAnchor => _authoritativeDriver?.LastCarrierTimeAnchor ?? default;

        /// <summary>从权威世界采集到的当前 LagComp 历史遥测。</summary>
        public ShooterLagCompensationTelemetry? LagCompensationTelemetry => _authoritativeDriver?.Telemetry;

        public int LastAuthorityDeliveredInputCount => _authoritativeDriver?.LastDeliveredInputCount ?? 0;
 
        public ShooterFrameworkSnapshotPipelineDiagnostics FrameworkSnapshotPipelineDiagnostics => Controller.FrameworkSnapshotPipelineDiagnostics;
  
        /// <summary>最近一次服务端回溯命中验证结果。</summary>
        public ShooterLagCompensationEvaluation? LastLagCompensationEvaluation => _authoritativeDriver?.LastLagCompensationEvaluation;

        /// <summary>
        /// 使用权威世界历史，将客户端上报的射击与回溯后的玩家碰撞盒进行验证。
        /// 当比较模式禁用或射击被拒绝时返回 false。
        /// </summary>
        public bool TryEvaluateLagCompensationShot(
            in ShooterLagCompensationShot shot,
            out ShooterLagCompensationEvaluation evaluation)
        {
            if (_authoritativeDriver == null)
            {
                evaluation = default;
                return false;
            }

            return _authoritativeDriver.TryEvaluateShot(in shot, out evaluation);
        }
 
        /// <summary>
        /// 在不重建会话的情况下实时调节网络环境。下一次 <see cref="Run"/> 或单步推进会使用新的 profile。
        /// 可接收目录预设，也可接收由运行时滑条构建的临时 <see cref="NetworkConditionProfile"/>。
        /// </summary>
        public void ApplyNetwork(NetworkConditionProfile profile, string? displayName = null)
        {
            _networkProfile = profile;
            _networkName = string.IsNullOrWhiteSpace(displayName) ? DescribeNetwork(profile) : displayName!;
            _authoritativeDriver?.ApplyNetwork(_networkProfile);
        }

        /// <summary>
        /// 在当前网络 profile 下通过框架 DemoHarness 推进会话，并返回包含指标的四态运行结果。
        /// 当存在权威世界时，它会按锁步推进，确保两个世界保持帧对齐以便比较。
        /// 复用 <see cref="DemoHarnessRunner"/> 意味着验收路径会覆盖无头测试套件已经验证过的同一套机制。
        /// </summary>
        public DemoHarnessRunResult Run(int stepCount = DefaultStepCount, float deltaSeconds = 1f / 30f, int seed = 0)
        {
            if (stepCount <= 0) throw new ArgumentOutOfRangeException(nameof(stepCount));
            if (deltaSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

            var scenario = new DemoHarnessScenario(
                name: $"Shooter {SyncModel} @ {_networkName}",
                syncModel: SyncModel,
                networkProfile: _networkProfile,
                carrierName: Carrier.CarrierName,
                stepCount: stepCount,
                deltaSeconds: deltaSeconds,
                seed: seed);

            var runner = new DemoHarnessRunner();
            var result = runner.Run(in scenario, Carrier);

            // 让权威世界在批量运行后与客户端世界保持同一帧范围，便于后续 CompareWorlds 对齐比较。
            AdvanceAuthoritativeWorld(result.Metrics.StepsRun, deltaSeconds);

            return result;
        }

        public void EnqueueAuthoritativeInput(int commandFrame, in ShooterPlayerCommand command)
        {
            _authoritativeDriver?.EnqueueInput(commandFrame, in command);
        }

        /// <summary>
        /// 推进权威世界一个 tick。Unity 外壳逐帧驱动会话（通过 Carrier.Step / Controller.Tick）时，
        /// 可调用它保持权威世界对齐；比较模式禁用时该操作为空。
        /// </summary>
        public void TickAuthoritativeWorld(float deltaSeconds)
        {
            AdvanceAuthoritativeWorld(1, deltaSeconds);
        }

        /// <summary>
        /// 比较客户端预测世界与权威世界，并返回逐实体位置偏差。比较模式禁用时返回空比较结果。
        /// Unity 外壳使用它来可视化高亮预测误差。
        /// </summary>
        public ShooterWorldComparison CompareWorlds()
        {
            if (AuthoritativeWorld == null)
            {
                return new ShooterWorldComparison(Runtime.CurrentFrame, 0, Array.Empty<ShooterWorldDivergence>());
            }

            var clientSnapshot = Runtime.GetSnapshot();
            var authoritySnapshot = AuthoritativeWorld.GetSnapshot();

            var divergences = new List<ShooterWorldDivergence>();
            var authorityPlayers = IndexByPlayerId(authoritySnapshot.Players);
            for (var i = 0; i < clientSnapshot.Players.Length; i++)
            {
                var client = clientSnapshot.Players[i];
                if (authorityPlayers.TryGetValue(client.PlayerId, out var authority))
                {
                    divergences.Add(new ShooterWorldDivergence(
                        client.PlayerId, client.X, client.Y, authority.X, authority.Y));
                }
            }

            return new ShooterWorldComparison(
                clientSnapshot.Frame,
                authoritySnapshot.Frame,
                divergences);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _authoritativeDriver?.Dispose();
            _authoritativeDriver = null;
            _authoritativeWorldSession?.Dispose();
            _runtimeWorld.Dispose();
        }

        private void AdvanceAuthoritativeWorld(int stepCount, float deltaSeconds)
        {
            _authoritativeDriver?.Advance(stepCount, deltaSeconds);
        }

        private static Dictionary<int, ShooterPlayerSnapshot> IndexByPlayerId(ShooterPlayerSnapshot[] players)
        {
            var map = new Dictionary<int, ShooterPlayerSnapshot>(players.Length);
            for (var i = 0; i < players.Length; i++)
            {
                map[players[i].PlayerId] = players[i];
            }

            return map;
        }

        private static string DescribeNetwork(NetworkConditionProfile profile)
        {
            return $"{profile.BaseLatencyMs}ms/{profile.JitterMs}ms jitter";
        }
    }

    /// <summary>
    /// Shooter 验收会话的一站式工厂。给定同步模式与网络环境后，装配完整的 C# 会话
    /// （runtime + presentation + controller + carrier）、启动对局，并返回可运行的 <see cref="ShooterAcceptanceSession"/>。
    /// 这是 Unity 验收外壳依赖的单一接缝：选择模式、选择网络、可选启用权威比较世界，然后调用 Create、推进并观察。
    /// </summary>
    public static class ShooterAcceptanceLab
    {
        public const int DefaultTickRate = 30;

        /// <summary>
        /// 根据显式同步模型与网络 profile 构建可运行会话。
        /// </summary>
        /// <param name="syncModel">要验证的同步策略，必须是已实现模型。</param>
        /// <param name="networkProfile">模拟网络环境。</param>
        /// <param name="networkName">可选网络标签；默认使用 profile 延迟描述。</param>
        /// <param name="tickRate">模拟 tick rate，也会写入 start payload。</param>
        /// <param name="players">可选玩家名单；默认生成两个玩家。</param>
        /// <param name="matchId">可选对局 id；默认从模型派生验收 id。</param>
        /// <param name="randomSeed">战斗运行时的确定性随机种子。</param>
        /// <param name="interpolationConfig">插值模型的可选配置。</param>
        /// <param name="enableAuthoritativeWorld">
        /// 为 true 时，在客户端世界旁启动独立权威模拟，让 Unity 外壳可以同时渲染与比较二者。默认为 false（仅客户端世界）。
        /// </param>
        /// <param name="gameplayScenario">可选玩法场景，会注入到 runtime world 的刷怪与竞技场配置。</param>
        /// <param name="networkStats">可选的实时网络统计源，会暴露给 harness。</param>
        /// <param name="remoteJitter">可选的实时远端抖动源。</param>
        /// <param name="acceptedHits">可选的命中接受计数源。</param>
        /// <param name="rejectedHits">可选的命中拒绝计数源。</param>
        public static ShooterAcceptanceSession Create(
            NetworkSyncModel syncModel,
            NetworkConditionProfile networkProfile,
            string? networkName = null,
            int tickRate = DefaultTickRate,
            IReadOnlyList<ShooterStartPlayer>? players = null,
            string? matchId = null,
            int randomSeed = 0,
            InterpolationConfig? interpolationConfig = null,
            bool enableAuthoritativeWorld = false,
            ShooterSveltoGameplayScenarioConfig? gameplayScenario = null,
            Func<NetworkConditioningStats>? networkStats = null,
            Func<double>? remoteJitter = null,
            Func<long>? acceptedHits = null,
            Func<long>? rejectedHits = null)
        {
            return Create(
                syncModel,
                networkProfile,
                networkName,
                tickRate,
                players,
                matchId,
                randomSeed,
                interpolationConfig,
                enableAuthoritativeWorld,
                gameplayScenario,
                networkStats,
                remoteJitter,
                acceptedHits,
                rejectedHits,
                ShooterAuthoritySnapshotPublishOptions.RealtimePacked);
        }

        internal static ShooterAcceptanceSession CreateForTemplate(
            string syncTemplateId,
            NetworkSyncModel syncModel,
            NetworkConditionProfile networkProfile,
            string? networkName,
            int tickRate,
            IReadOnlyList<ShooterStartPlayer> players,
            int randomSeed,
            bool enableAuthoritativeWorld,
            ShooterSveltoGameplayScenarioConfig gameplayScenario)
        {
            var template = ShooterAcceptanceCatalog.GetSyncTemplate(syncTemplateId);
            return Create(
                syncModel,
                networkProfile,
                networkName,
                tickRate,
                players,
                matchId: null,
                randomSeed,
                template.InterpolationConfig,
                enableAuthoritativeWorld,
                gameplayScenario,
                networkStats: null,
                remoteJitter: null,
                acceptedHits: null,
                rejectedHits: null,
                new ShooterAuthoritySnapshotPublishOptions(template.SendPolicy, template.ConvergenceKind));
        }

        public static ShooterAcceptanceSession Create(in ShooterSyncTemplate template)
        {
            if (!template.IsRunnable)
            {
                throw new NotSupportedException(
                    $"Shooter sync template '{template.DisplayName}' ({template.Id}) is {template.Status} and cannot be run.");
            }

            var network = ShooterAcceptanceCatalog.GetNetworkEnvironment(template.NetworkEnvironmentId);
            return Create(
                template.SyncModel,
                network.Profile,
                template.DisplayName,
                DefaultTickRate,
                BuildTemplatePlayers(template.RecommendedPlayerCount),
                matchId: null,
                randomSeed: 0,
                template.InterpolationConfig,
                template.EnableAuthoritativeWorld,
                gameplayScenario: null,
                networkStats: null,
                remoteJitter: null,
                acceptedHits: null,
                rejectedHits: null,
                new ShooterAuthoritySnapshotPublishOptions(template.SendPolicy, template.ConvergenceKind));
        }

        /// <summary>
        /// 由目录菜单选项直接构建会话的重载。
        /// </summary>
        public static ShooterAcceptanceSession Create(
            in ShooterAcceptanceSyncOption sync,
            in ShooterAcceptanceNetworkOption network,
            int tickRate = DefaultTickRate,
            InterpolationConfig? interpolationConfig = null,
            bool enableAuthoritativeWorld = false)
        {
            if (!sync.Implemented)
            {
                throw new NotSupportedException(
                    $"Sync mode '{sync.DisplayName}' ({sync.Model}) is not implemented yet and cannot be run.");
            }

            return Create(
                sync.Model,
                network.Profile,
                network.DisplayName,
                tickRate,
                interpolationConfig: interpolationConfig,
                enableAuthoritativeWorld: enableAuthoritativeWorld);
        }

        /// <summary>
        /// 将每个 capability matrix 行运行在每个目录化网络环境上，并返回包含聚合摘要的四态批量结果。
        /// 这是逐个点击完整 Unity 验收矩阵的无头等价路径，展开依据是 <see cref="NetworkSyncProfile"/>
        /// 与 carrier capability，而不是同步模式名称匹配。
        /// </summary>
        public static DemoHarnessBatchResult RunCatalogMatrix(
            int stepCount = ShooterAcceptanceSession.DefaultStepCount,
            float deltaSeconds = 1f / 30f,
            int seed = 0)
        {
            var results = new List<DemoHarnessRunResult>();
            foreach (var row in ShooterAcceptanceCatalog.SyncModeMatrix.Rows)
            {
                if (!row.IsRunnable)
                {
                    continue;
                }

                foreach (var network in ShooterAcceptanceCatalog.NetworkEnvironments)
                {
                    using var session = Create(
                        row.SyncModel,
                        network.Profile,
                        network.DisplayName,
                        DefaultTickRate,
                        BuildTemplatePlayers(row.Template.RecommendedPlayerCount),
                        matchId: null,
                        seed,
                        row.Template.InterpolationConfig,
                        row.Template.EnableAuthoritativeWorld,
                        gameplayScenario: null,
                        networkStats: null,
                        remoteJitter: null,
                        acceptedHits: null,
                        rejectedHits: null,
                        new ShooterAuthoritySnapshotPublishOptions(row.Template.SendPolicy, row.Template.ConvergenceKind));
                    results.Add(session.Run(stepCount, deltaSeconds, seed));
                }
            }

            return new DemoHarnessBatchResult(results.AsReadOnly());
        }

        private static ShooterAcceptanceSession Create(
            NetworkSyncModel syncModel,
            NetworkConditionProfile networkProfile,
            string? networkName,
            int tickRate,
            IReadOnlyList<ShooterStartPlayer>? players,
            string? matchId,
            int randomSeed,
            InterpolationConfig? interpolationConfig,
            bool enableAuthoritativeWorld,
            ShooterSveltoGameplayScenarioConfig? gameplayScenario,
            Func<NetworkConditioningStats>? networkStats,
            Func<double>? remoteJitter,
            Func<long>? acceptedHits,
            Func<long>? rejectedHits,
            ShooterAuthoritySnapshotPublishOptions authoritySnapshotPublishOptions)
        {
            if (tickRate <= 0) throw new ArgumentOutOfRangeException(nameof(tickRate));

            var start = BuildStartPayload(matchId, tickRate, randomSeed, players, syncModel);
            var worldHost = ShooterGameplayScenarioWorldHostFactory.Create(gameplayScenario);
            var runtimeWorld = ShooterBattleWorldSession.Create($"{start.MatchId}-client", worldHost);
            ShooterBattleWorldSession? authoritativeWorldSession = null;

            try
            {
                var runtime = runtimeWorld.Runtime;
                var presentation = new ShooterPresentationFacade();
                var controller = ShooterClientSyncControllerFactory.Create(
                    syncModel, runtime, presentation, tickRate, decoder: null, gateway: null, interpolationConfig);

                if (!controller.StartGame(in start))
                {
                    throw new InvalidOperationException(
                        $"Shooter acceptance session failed to start the '{syncModel}' controller.");
                }

                var syncProfile = NetworkSyncProfiles.FromCompatibilityModel(syncModel);
                var carrier = CreateCarrier(
                    in syncProfile,
                    in networkProfile,
                    controller,
                    networkStats,
                    remoteJitter,
                    acceptedHits,
                    rejectedHits);

                ShooterPresentationFacade? authoritativePresentation = null;
                if (enableAuthoritativeWorld)
                {
                    authoritativeWorldSession = ShooterBattleWorldSession.Create($"{start.MatchId}-authority", worldHost);
                    authoritativePresentation = new ShooterPresentationFacade();
                    if (!authoritativeWorldSession.Runtime.StartGame(in start))
                    {
                        throw new InvalidOperationException(
                            $"Shooter acceptance session failed to start the '{syncModel}' authoritative world.");
                    }

                    var initialSnapshot = authoritativeWorldSession.Runtime.GetSnapshot();
                    authoritativePresentation.ApplyLocalAuthoritativeSnapshot(in initialSnapshot);
                }

                return new ShooterAcceptanceSession(
                    runtimeWorld,
                    presentation,
                    controller,
                    carrier,
                    syncModel,
                    networkProfile,
                    string.IsNullOrWhiteSpace(networkName) ? DescribeNetwork(networkProfile) : networkName!,
                    authoritativeWorldSession,
                    authoritativePresentation,
                    authoritySnapshotPublishOptions,
                    randomSeed);
            }
            catch
            {
                authoritativeWorldSession?.Dispose();
                runtimeWorld.Dispose();
                throw;
            }
        }

        private static ISyncDemoCarrier CreateCarrier(
            in NetworkSyncProfile profile,
            in NetworkConditionProfile networkProfile,
            IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample> controller,
            Func<NetworkConditioningStats>? networkStats,
            Func<double>? remoteJitter,
            Func<long>? acceptedHits,
            Func<long>? rejectedHits)
        {
            var candidates = new ISyncDemoCarrier[]
            {
                new ShooterDemoHarnessCarrier(controller, networkStats, remoteJitter, acceptedHits, rejectedHits),
                new ShooterInterpolationDemoHarnessCarrier(controller, networkStats, remoteJitter, acceptedHits, rejectedHits),
                new ShooterHybridDemoHarnessCarrier(controller, networkStats, remoteJitter, acceptedHits, rejectedHits)
            };

            return DemoHarnessCarrierSelector.SelectFirstOrThrow(
                candidates,
                in profile,
                in networkProfile,
                "Shooter demo harness carrier");
        }

        private static ShooterStartGamePayload BuildStartPayload(
            string? matchId,
            int tickRate,
            int randomSeed,
            IReadOnlyList<ShooterStartPlayer>? players,
            NetworkSyncModel syncModel)
        {
            var id = string.IsNullOrWhiteSpace(matchId)
                ? $"acceptance-{syncModel}".ToLowerInvariant()
                : matchId!;

            var roster = (players == null || players.Count == 0)
                ? DefaultPlayers()
                : ToArray(players);

            return new ShooterStartGamePayload(id, tickRate, randomSeed, roster);
        }

        private static ShooterStartPlayer[] DefaultPlayers()
        {
            return new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 4f, 0f)
            };
        }

        private static ShooterStartPlayer[] BuildTemplatePlayers(int playerCount)
        {
            var count = playerCount < 1 ? 1 : playerCount;
            var players = new ShooterStartPlayer[count];
            for (var i = 0; i < count; i++)
            {
                players[i] = new ShooterStartPlayer(i + 1, $"P{i + 1}", i * 4f, 0f);
            }

            return players;
        }

        private static ShooterStartPlayer[] ToArray(IReadOnlyList<ShooterStartPlayer> players)
        {
            var buffer = new ShooterStartPlayer[players.Count];
            for (var i = 0; i < players.Count; i++)
            {
                buffer[i] = players[i];
            }

            return buffer;
        }

        private static string DescribeNetwork(NetworkConditionProfile profile)
        {
            return $"{profile.BaseLatencyMs}ms/{profile.JitterMs}ms jitter";
        }
    }
}
