using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services.Attributes;

/// <summary>
/// 文件名称: IMobaSnapshotEmitter.cs
/// 
/// 功能描述: 定义可扩展的 MOBA 快照输出接口。
/// 
/// 创建日期: 2026-05-27
/// 修改日期: 2026-05-27
/// </summary>
namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// MOBA 快照输出器接口，供快照路由按优先级统一调度。
    /// </summary>
    public interface IMobaSnapshotEmitter
    {
        /// <summary>
        /// 尝试生成当前帧快照。
        /// </summary>
        /// <param name="frame">当前帧</param>
        /// <param name="snapshot">输出快照</param>
        /// <returns>是否生成了快照</returns>
        bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot);
    }

    public readonly struct MobaSnapshotEmitterHealthEntry
    {
        public readonly Type EmitterType;
        public readonly string Name;

        public MobaSnapshotEmitterHealthEntry(Type emitterType)
        {
            EmitterType = emitterType;
            Name = emitterType != null ? emitterType.Name : string.Empty;
        }
    }

    public readonly struct MobaSnapshotRouterHealth
    {
        public readonly int EmitterCount;
        public readonly int RequiredEmitterCount;
        public readonly int MissingRequiredEmitterCount;
        public readonly long SingleRequests;
        public readonly long BatchRequests;
        public readonly long HitCount;
        public readonly long EmptyCount;
        public readonly int LastFrame;
        public readonly int LastSnapshotOpCode;
        public readonly int LastBatchSnapshotCount;
        public readonly bool UsedAttributeRegistry;
        public readonly IReadOnlyList<MobaSnapshotEmitterHealthEntry> Emitters;
        public readonly IReadOnlyList<string> MissingRequiredEmitters;

        public MobaSnapshotRouterHealth(int emitterCount, int requiredEmitterCount, int missingRequiredEmitterCount, long singleRequests, long batchRequests, long hitCount, long emptyCount, int lastFrame, int lastSnapshotOpCode, int lastBatchSnapshotCount, bool usedAttributeRegistry, IReadOnlyList<MobaSnapshotEmitterHealthEntry> emitters, IReadOnlyList<string> missingRequiredEmitters)
        {
            EmitterCount = emitterCount;
            RequiredEmitterCount = requiredEmitterCount;
            MissingRequiredEmitterCount = missingRequiredEmitterCount;
            SingleRequests = singleRequests;
            BatchRequests = batchRequests;
            HitCount = hitCount;
            EmptyCount = emptyCount;
            LastFrame = lastFrame;
            LastSnapshotOpCode = lastSnapshotOpCode;
            LastBatchSnapshotCount = lastBatchSnapshotCount;
            UsedAttributeRegistry = usedAttributeRegistry;
            Emitters = emitters;
            MissingRequiredEmitters = missingRequiredEmitters;
        }

        public bool HasEmitters => EmitterCount > 0;
        public bool IsOutputContractSatisfied => MissingRequiredEmitterCount == 0;

        public bool HasEmitter(Type emitterType)
        {
            if (emitterType == null || Emitters == null) return false;

            for (int i = 0; i < Emitters.Count; i++)
            {
                if (Emitters[i].EmitterType == emitterType) return true;
            }

            return false;
        }

        public override string ToString()
        {
            return $"emitters={EmitterCount}/{RequiredEmitterCount}, missingRequired={MissingRequiredEmitterCount}, contractReady={IsOutputContractSatisfied}, singleRequests={SingleRequests}, batchRequests={BatchRequests}, hits={HitCount}, empty={EmptyCount}, lastFrame={LastFrame}, lastOp={LastSnapshotOpCode}, lastBatch={LastBatchSnapshotCount}, attributeRegistry={UsedAttributeRegistry}";
        }
    }

    public readonly struct MobaRequiredSnapshotEmitterContract
    {
        public readonly int OpCode;
        public readonly Type EmitterType;
        public readonly string Name;

        public MobaRequiredSnapshotEmitterContract(int opCode, Type emitterType, string name)
        {
            if (emitterType == null) throw new ArgumentNullException(nameof(emitterType));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name is required.", nameof(name));

            OpCode = opCode;
            EmitterType = emitterType;
            Name = name;
        }
    }

    public sealed class MobaSnapshotOutputContractValidationResult
    {
        private readonly List<string> _errors = new List<string>(8);

        public IReadOnlyList<string> Errors => _errors;
        public bool Succeeded => _errors.Count == 0;

        public void AddError(string error)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                _errors.Add(error);
            }
        }
    }

    public sealed class MobaSnapshotOutputContract
    {
        private readonly List<MobaRequiredSnapshotEmitterContract> _requiredEmitters = new List<MobaRequiredSnapshotEmitterContract>(8);

        public IReadOnlyList<MobaRequiredSnapshotEmitterContract> RequiredEmitters => _requiredEmitters;

        public static MobaSnapshotOutputContract CreateDefault()
        {
            var contract = new MobaSnapshotOutputContract();
            contract.Require(AbilityKit.Protocol.Moba.MobaOpCodes.Snapshot.EnterGame, typeof(MobaEnterGameSnapshotService), "EnterGame");
            contract.Require(AbilityKit.Protocol.Moba.MobaOpCodes.Snapshot.ActorSpawn, typeof(MobaActorSpawnSnapshotService), "ActorSpawn");
            contract.Require(AbilityKit.Protocol.Moba.MobaOpCodes.Snapshot.ActorTransform, typeof(MobaActorTransformSnapshotService), "ActorTransform");
            contract.Require(AbilityKit.Protocol.Moba.MobaOpCodes.Snapshot.StateHash, typeof(MobaStateHashSnapshotService), "StateHash");
            contract.Require(AbilityKit.Protocol.Moba.MobaOpCodes.Snapshot.ProjectileEvent, typeof(MobaProjectileEventSnapshotService), "ProjectileEvent");
            contract.Require(AbilityKit.Protocol.Moba.MobaOpCodes.Snapshot.AreaEvent, typeof(MobaAreaEventSnapshotService), "AreaEvent");
            contract.Require(AbilityKit.Protocol.Moba.MobaOpCodes.Snapshot.DamageEvent, typeof(MobaDamageEventSnapshotService), "DamageEvent");
            contract.Require(AbilityKit.Protocol.Moba.MobaOpCodes.Snapshot.ActorDespawn, typeof(MobaActorDespawnSnapshotService), "ActorDespawn");
            contract.Require(AbilityKit.Protocol.Moba.MobaOpCodes.Snapshot.PlayerHeroChanged, typeof(MobaPlayerHeroChangedSnapshotService), "PlayerHeroChanged");
            contract.Require(AbilityKit.Protocol.Moba.MobaOpCodes.Snapshot.PresentationCue, typeof(MobaPresentationCueSnapshotService), "PresentationCue");
            contract.Require(AbilityKit.Protocol.Moba.MobaOpCodes.Snapshot.SkillState, typeof(MobaSkillStateSnapshotService), "SkillState");
            return contract;
        }

        public void Require(int opCode, Type emitterType, string name)
        {
            _requiredEmitters.Add(new MobaRequiredSnapshotEmitterContract(opCode, emitterType, name));
        }

        public MobaSnapshotOutputContractValidationResult Validate(in MobaSnapshotRouterHealth health)
        {
            var result = new MobaSnapshotOutputContractValidationResult();

            for (int i = 0; i < _requiredEmitters.Count; i++)
            {
                var required = _requiredEmitters[i];
                if (health.HasEmitter(required.EmitterType)) continue;

                result.AddError($"missing required snapshot emitter. opCode={required.OpCode}, name={required.Name}, expected={required.EmitterType.Name}. {health}");
            }

            return result;
        }
    }

    [WorldService(typeof(MobaSnapshotContractProfileRegistry), WorldLifetime.Singleton)]
    public sealed class MobaSnapshotContractProfileRegistry
    {
        private readonly Dictionary<int, MobaSnapshotOutputContract> _profiles =
            new Dictionary<int, MobaSnapshotOutputContract>();
        private MobaSnapshotOutputContract _defaultProfile;

        public static MobaSnapshotContractProfileRegistry CreateDefault()
        {
            var registry = new MobaSnapshotContractProfileRegistry();
            registry.RegisterDefault(MobaSnapshotOutputContract.CreateDefault());
            return registry;
        }

        public void RegisterDefault(MobaSnapshotOutputContract contract)
        {
            _defaultProfile = contract ?? throw new ArgumentNullException(nameof(contract));
        }

        public void Register(int gameplayId, MobaSnapshotOutputContract contract)
        {
            if (gameplayId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(gameplayId), gameplayId, "gameplay id must be positive.");
            }

            if (contract == null) throw new ArgumentNullException(nameof(contract));
            if (_profiles.ContainsKey(gameplayId))
            {
                throw new InvalidOperationException($"duplicate snapshot contract profile. gameplayId={gameplayId}");
            }

            _profiles.Add(gameplayId, contract);
        }

        public bool TryResolve(
            int gameplayId,
            out MobaSnapshotOutputContract contract,
            out string error)
        {
            contract = null;
            error = null;
            if (gameplayId <= 0)
            {
                error = $"snapshot contract gameplay id must be positive. gameplayId={gameplayId}";
                return false;
            }

            if (_profiles.TryGetValue(gameplayId, out contract) && contract != null)
            {
                return true;
            }

            if (_defaultProfile != null)
            {
                contract = _defaultProfile;
                return true;
            }

            error = $"snapshot contract profile is not declared. gameplayId={gameplayId}";
            return false;
        }
    }

    public interface IMobaSnapshotHealthProvider
    {
        MobaSnapshotRouterHealth GetHealth();
    }

    /// <summary>
    /// 为聚合多个输出器的路由器提供显式批量快照收集路径。
    /// </summary>
    public interface IMobaSnapshotBatchProvider
    {
        int CollectSnapshots(FrameIndex frame, IList<WorldStateSnapshot> snapshots, int maxSnapshots = 32);
    }
}