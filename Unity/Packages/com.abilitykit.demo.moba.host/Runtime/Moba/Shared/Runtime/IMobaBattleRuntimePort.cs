using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Services;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Ability.Host.Extensions.Moba.Runtime
{
    public enum MobaGameStartFailureCode
    {
        None = 0,
        AlreadyStarted = 1,
        InvalidProtocol = 2,
        MissingActorContext = 3,
        MissingActorEntityInitPipeline = 4,
        ActorBuildFailed = 5,
        InvalidActorBuildResult = 6,
        PublishEnterGameSnapshotFailed = 7,
        PublishSpawnSnapshotFailed = 8,
        MissingGameStartPort = 9,
        MissingGameplayService = 10,
        InvalidGameplayId = 11,
        GameplayStartFailed = 12,
    }

    public readonly struct MobaGameStartResult
    {
        public static readonly MobaGameStartResult Success = new MobaGameStartResult(true, MobaGameStartFailureCode.None, null);

        public readonly bool Succeeded;
        public readonly MobaGameStartFailureCode FailureCode;
        public readonly string Message;

        public MobaGameStartResult(bool succeeded, MobaGameStartFailureCode failureCode, string message)
        {
            Succeeded = succeeded;
            FailureCode = failureCode;
            Message = message;
        }

        public static MobaGameStartResult Fail(MobaGameStartFailureCode failureCode, string message)
        {
            return new MobaGameStartResult(false, failureCode, message);
        }

        public override string ToString()
        {
            return Succeeded ? "Success" : $"{FailureCode}: {Message}";
        }
    }

    public enum MobaInputSubmitFailureCode
    {
        None = 0,
        NotRunning = 1,
        MissingInputPort = 2,
        MissingInputCoordinator = 3,
        NullOrEmptyCommands = 4,
        InvalidFrame = 5,
        RejectedByInputCoordinator = 6,
        NoCommandHandled = 7,
        PartialCommandHandled = 8,
    }

    public readonly struct MobaInputSubmitResult
    {
        public static readonly MobaInputSubmitResult Success = new MobaInputSubmitResult(true, MobaInputSubmitFailureCode.None, null, 0);

        public readonly bool Succeeded;
        public readonly MobaInputSubmitFailureCode FailureCode;
        public readonly string Message;
        public readonly int CommandCount;

        public MobaInputSubmitResult(bool succeeded, MobaInputSubmitFailureCode failureCode, string message, int commandCount)
        {
            Succeeded = succeeded;
            FailureCode = failureCode;
            Message = message;
            CommandCount = commandCount;
        }

        public static MobaInputSubmitResult Accepted(int commandCount)
        {
            return Accepted(commandCount, null);
        }

        public static MobaInputSubmitResult Accepted(int commandCount, string message)
        {
            return new MobaInputSubmitResult(true, MobaInputSubmitFailureCode.None, message, commandCount);
        }

        public static MobaInputSubmitResult Fail(MobaInputSubmitFailureCode failureCode, string message)
        {
            return new MobaInputSubmitResult(false, failureCode, message, 0);
        }

        public override string ToString()
        {
            return Succeeded
                ? $"Success: Commands={CommandCount}, Message={Message}"
                : $"{FailureCode}: {Message}";
        }
    }

    public struct LogicWorldEntityState
    {
        public int EntityId;
        public float X;
        public float Y;
        public float Z;
        public float Rotation;
        public float VelocityX;
        public float VelocityZ;
        public float Hp;
        public float HpMax;
        public int TeamId;
        public bool IsDead;
        public bool HasAttributeGroup;
        public bool HasResourceContainer;
        public bool HasSkillLoadout;
        public int ActiveSkillCount;

        public LogicWorldEntityState(int entityId)
        {
            EntityId = entityId;
            X = Y = Z = 0f;
            Rotation = 0f;
            VelocityX = VelocityZ = 0f;
            Hp = HpMax = 0f;
            TeamId = 0;
            IsDead = true;
            HasAttributeGroup = false;
            HasResourceContainer = false;
            HasSkillLoadout = false;
            ActiveSkillCount = 0;
        }

        public static LogicWorldEntityState Empty(int entityId)
        {
            return new LogicWorldEntityState(entityId);
        }
    }

    public struct MobaDiagnosticEntityState
    {
        public int EntityId;
        public float X;
        public float Y;
        public float Z;
        public float Rotation;
        public float VelocityX;
        public float VelocityZ;
        public float Hp;
        public float HpMax;
        public int TeamId;
        public bool IsDead;
        public bool HasAttributeGroup;
        public bool HasResourceContainer;
        public bool HasSkillLoadout;
        public int ActiveSkillCount;

        public MobaDiagnosticEntityState(int entityId)
        {
            EntityId = entityId;
            X = Y = Z = 0f;
            Rotation = 0f;
            VelocityX = VelocityZ = 0f;
            Hp = HpMax = 0f;
            TeamId = 0;
            IsDead = true;
            HasAttributeGroup = false;
            HasResourceContainer = false;
            HasSkillLoadout = false;
            ActiveSkillCount = 0;
        }

        public static MobaDiagnosticEntityState FromLogicState(in LogicWorldEntityState state)
        {
            return new MobaDiagnosticEntityState(state.EntityId)
            {
                X = state.X,
                Y = state.Y,
                Z = state.Z,
                Rotation = state.Rotation,
                VelocityX = state.VelocityX,
                VelocityZ = state.VelocityZ,
                Hp = state.Hp,
                HpMax = state.HpMax,
                TeamId = state.TeamId,
                IsDead = state.IsDead,
                HasAttributeGroup = state.HasAttributeGroup,
                HasResourceContainer = state.HasResourceContainer,
                HasSkillLoadout = state.HasSkillLoadout,
                ActiveSkillCount = state.ActiveSkillCount,
            };
        }
    }

    [Flags]
    public enum MobaBattleRuntimeCapability
    {
        None = 0,
        GameStart = 1 << 0,
        Input = 1 << 1,
        SnapshotOutput = 1 << 2,
        StateReadModel = 1 << 3,
    }

    public readonly struct MobaBattleRuntimeStatus
    {
        public readonly MobaBattleRuntimeCapability Capabilities;
        public readonly string MissingServices;

        public MobaBattleRuntimeStatus(MobaBattleRuntimeCapability capabilities, string missingServices)
        {
            Capabilities = capabilities;
            MissingServices = missingServices;
        }

        public bool Has(MobaBattleRuntimeCapability capability)
        {
            return (Capabilities & capability) == capability;
        }

        public bool IsReadyForBattleLoop => Has(MobaBattleRuntimeCapability.Input | MobaBattleRuntimeCapability.SnapshotOutput);

        public bool IsReadyForGameStart => Has(MobaBattleRuntimeCapability.GameStart);

        public override string ToString()
        {
            return string.IsNullOrEmpty(MissingServices)
                ? $"Capabilities={Capabilities}"
                : $"Capabilities={Capabilities}, Missing={MissingServices}";
        }
    }

    public interface IMobaGameStartPort : IService
    {
        MobaGameStartResult TryStartGame(in MobaGameStartSpec spec);
    }

    public static class MobaBattleRuntimeFacadeContract
    {
        public const string FacadeService = nameof(IMobaBattleRuntimePort);

        public static readonly string[] AggregatedPorts =
        {
            nameof(IMobaGameStartPort),
            nameof(IMobaBattleRuntimePort) + ".Input",
            nameof(IMobaBattleRuntimePort) + ".SnapshotOutput",
            nameof(IMobaBattleRuntimePort) + ".StateReadModel",
        };

        public static readonly string[] ExternalConsumerServices =
        {
            "Session",
            "Server.Host.Extension",
            "View.Adapter",
            "Diagnostics",
        };
    }

    public interface IMobaBattleRuntimePort : IService
    {
        MobaBattleRuntimeStatus Status { get; }

        MobaGameStartResult TryStartGame(in MobaGameStartSpec spec);

        MobaInputSubmitResult Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs);

        /// <summary>
        /// 兼容单快照读取入口；生产同步循环应优先使用 <see cref="CollectSnapshots"/> 以完整收集同帧多路输出。
        /// </summary>
        bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot);

        /// <summary>
        /// 批量快照收集入口，供帧同步、服务器驱动和 View Adapter 高频路径复用外部缓冲区。
        /// </summary>
        int CollectSnapshots(FrameIndex frame, IList<WorldStateSnapshot> snapshots, int maxSnapshots = 32);

        /// <summary>
        /// 兼容数组读取入口；高频诊断采样应优先使用 <see cref="FillDiagnosticEntityStates"/>。
        /// </summary>
        MobaDiagnosticEntityState[] GetDiagnosticEntityStates();

        /// <summary>
        /// 填充调用方提供的缓冲区，避免诊断状态采样产生数组分配。
        /// </summary>
        int FillDiagnosticEntityStates(IList<MobaDiagnosticEntityState> buffer);

        /// <summary>
        /// 兼容数组读取入口；高频状态采样应优先使用 <see cref="FillAllEntityStates"/>。
        /// </summary>
        LogicWorldEntityState[] GetAllEntityStates();

        /// <summary>
        /// 填充调用方提供的缓冲区，作为逻辑世界状态读取的生产推荐路径。
        /// </summary>
        int FillAllEntityStates(IList<LogicWorldEntityState> buffer);
    }
}
