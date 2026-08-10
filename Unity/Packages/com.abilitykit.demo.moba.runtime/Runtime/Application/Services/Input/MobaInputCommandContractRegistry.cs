using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.Snapshot;
using AbilityKit.Demo.Moba.Util.Converter;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Services
{
    public enum MobaInputCommandAuthority
    {
        Unspecified = 0,
        BattlePlayer = 1,
    }

    public enum MobaInputCommandFramePolicy
    {
        Unspecified = 0,
        ExactBatchFrame = 1,
    }

    public delegate bool MobaInputPayloadValidator(
        byte[] payload,
        out string error);

    public readonly struct MobaInputCommandContract
    {
        public MobaInputCommandContract(
            int opCode,
            Type handlerType,
            string name,
            bool required,
            MobaInputCommandAuthority authority,
            MobaInputCommandFramePolicy framePolicy,
            string payloadSchema,
            MobaInputPayloadValidator payloadValidator)
        {
            OpCode = opCode;
            HandlerType = handlerType;
            Name = string.IsNullOrEmpty(name) ? handlerType?.Name : name;
            Required = required;
            Authority = authority;
            FramePolicy = framePolicy;
            PayloadSchema = payloadSchema;
            PayloadValidator = payloadValidator;
        }

        public int OpCode { get; }
        public Type HandlerType { get; }
        public string Name { get; }
        public bool Required { get; }
        public MobaInputCommandAuthority Authority { get; }
        public MobaInputCommandFramePolicy FramePolicy { get; }
        public string PayloadSchema { get; }
        public MobaInputPayloadValidator PayloadValidator { get; }
    }

    public sealed class MobaInputCommandContractValidationResult
    {
        private readonly List<string> _errors = new List<string>(4);

        public IReadOnlyList<string> Errors => _errors;
        public bool Succeeded => _errors.Count == 0;

        public void AddError(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            _errors.Add(message);
        }
    }

    [WorldService(typeof(MobaInputCommandContractRegistry), WorldLifetime.Singleton)]
    public sealed class MobaInputCommandContractRegistry
    {
        private readonly Dictionary<int, MobaInputCommandContract> _contracts = new Dictionary<int, MobaInputCommandContract>();
        private readonly List<MobaInputCommandContract> _contractList = new List<MobaInputCommandContract>(4);

        public MobaInputCommandContractRegistry(MobaInputCommandHandlerRegistry handlers)
        {
            HandlerRegistry = handlers ?? throw new ArgumentNullException(nameof(handlers));
        }

        public MobaInputCommandHandlerRegistry HandlerRegistry { get; }
        public IReadOnlyList<MobaInputCommandContract> Contracts => _contractList;
        public int ContractCount => _contractList.Count;

        public static MobaInputCommandContractRegistry CreateDefault()
        {
            var handlerRegistry = MobaInputCommandHandlerRegistry.CreateEmpty();
            var registry = new MobaInputCommandContractRegistry(handlerRegistry);
            registry.Require(
                AbilityKit.Protocol.Moba.MobaOpCodes.Input.Move,
                typeof(MobaMoveInputCommandHandler),
                "Move",
                MobaInputCommandAuthority.BattlePlayer,
                MobaInputCommandFramePolicy.ExactBatchFrame,
                nameof(MobaMovePayload),
                ValidateMovePayload);
            registry.Require(
                AbilityKit.Protocol.Moba.MobaOpCodes.Input.SkillInput,
                typeof(MobaSkillInputCommandHandler),
                "SkillInput",
                MobaInputCommandAuthority.BattlePlayer,
                MobaInputCommandFramePolicy.ExactBatchFrame,
                nameof(SkillInputEvent),
                ValidateSkillInputPayload);
            registry.Require(
                AbilityKit.Protocol.Moba.MobaOpCodes.Input.DebugSpawnUnit,
                typeof(MobaDebugSpawnUnitInputCommandHandler),
                "DebugSpawnUnit",
                MobaInputCommandAuthority.BattlePlayer,
                MobaInputCommandFramePolicy.ExactBatchFrame,
                nameof(MobaDebugSpawnUnitPayload) + ":v1",
                ValidateDebugSpawnUnitPayload);
            registry.Require(
                AbilityKit.Protocol.Moba.MobaOpCodes.Input.DebugReplaceHero,
                typeof(MobaDebugReplaceHeroInputCommandHandler),
                "DebugReplaceHero",
                MobaInputCommandAuthority.BattlePlayer,
                MobaInputCommandFramePolicy.ExactBatchFrame,
                nameof(MobaDebugReplaceHeroPayload) + ":v1",
                ValidateDebugReplaceHeroPayload);
            return registry;
        }

        public void Require(int opCode, Type handlerType, string name = null)
        {
            Require(
                opCode,
                handlerType,
                name,
                MobaInputCommandAuthority.Unspecified,
                MobaInputCommandFramePolicy.Unspecified,
                null,
                null);
        }

        public void Require(
            int opCode,
            Type handlerType,
            string name,
            MobaInputCommandAuthority authority,
            MobaInputCommandFramePolicy framePolicy,
            string payloadSchema,
            MobaInputPayloadValidator payloadValidator)
        {
            Register(new MobaInputCommandContract(
                opCode,
                handlerType,
                name,
                required: true,
                authority,
                framePolicy,
                payloadSchema,
                payloadValidator));
        }

        public void Register(in MobaInputCommandContract contract)
        {
            if (contract.OpCode <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(contract.OpCode), contract.OpCode, "input command opCode must be positive.");
            }

            if (contract.HandlerType == null)
            {
                throw new ArgumentNullException(nameof(contract.HandlerType), "input command handler type is required.");
            }

            if (!typeof(IMobaInputCommandHandler).IsAssignableFrom(contract.HandlerType))
            {
                throw new ArgumentException($"input command handler type must implement {nameof(IMobaInputCommandHandler)}. type={contract.HandlerType.FullName}");
            }

            if (_contracts.ContainsKey(contract.OpCode))
            {
                throw new InvalidOperationException($"duplicate input command contract. opCode={contract.OpCode}, name={contract.Name}");
            }

            _contracts.Add(contract.OpCode, contract);
            _contractList.Add(contract);
            HandlerRegistry.Register(contract.OpCode, contract.HandlerType);
        }

        public bool TryGetContract(int opCode, out MobaInputCommandContract contract)
        {
            return _contracts.TryGetValue(opCode, out contract);
        }

        public MobaInputCommandContractValidationResult Validate()
        {
            var result = new MobaInputCommandContractValidationResult();
            if (_contractList.Count == 0)
            {
                result.AddError("input command contract registry has no declared contracts.");
                return result;
            }

            for (int i = 0; i < _contractList.Count; i++)
            {
                var contract = _contractList[i];
                if (!contract.Required) continue;

                if (contract.Authority == MobaInputCommandAuthority.Unspecified)
                {
                    result.AddError($"input command authority is unspecified. opCode={contract.OpCode}, name={contract.Name}");
                }

                if (contract.FramePolicy == MobaInputCommandFramePolicy.Unspecified)
                {
                    result.AddError($"input command frame policy is unspecified. opCode={contract.OpCode}, name={contract.Name}");
                }

                if (string.IsNullOrEmpty(contract.PayloadSchema))
                {
                    result.AddError($"input command payload schema is missing. opCode={contract.OpCode}, name={contract.Name}");
                }

                if (contract.PayloadValidator == null)
                {
                    result.AddError($"input command payload validator is missing. opCode={contract.OpCode}, name={contract.Name}");
                }

                if (!HandlerRegistry.TryGetHandlerDescriptor(contract.OpCode, out var descriptor))
                {
                    result.AddError($"missing input command handler. opCode={contract.OpCode}, name={contract.Name}, expected={contract.HandlerType.Name}");
                    continue;
                }

                if (descriptor.HandlerType == null || !contract.HandlerType.IsAssignableFrom(descriptor.HandlerType))
                {
                    var actual = descriptor.HandlerType == null ? "null" : descriptor.HandlerType.Name;
                    result.AddError($"input command handler type mismatch. opCode={contract.OpCode}, name={contract.Name}, expected={contract.HandlerType.Name}, actual={actual}");
                }
            }

            return result;
        }

        public bool TryValidateCommand(
            MobaInputCommandContext context,
            FrameIndex frame,
            PlayerInputCommand command,
            out MobaInputCommandResult result)
        {
            if (!_contracts.TryGetValue(command.OpCode, out var contract))
            {
                result = MobaInputCommandResult.Rejected(
                    command,
                    MobaInputCommandFailureCode.ContractMissing,
                    $"Input command contract is missing. opCode={command.OpCode}");
                return false;
            }

            if (contract.Authority != MobaInputCommandAuthority.BattlePlayer ||
                context?.PlayerActorMap == null ||
                !context.PlayerActorMap.TryGetActorId(command.Player, out _))
            {
                result = MobaInputCommandResult.Rejected(
                    command,
                    MobaInputCommandFailureCode.AuthorityRejected,
                    $"Input command authority rejected. authority={contract.Authority}, player={command.Player.Value}");
                return false;
            }

            if (contract.FramePolicy != MobaInputCommandFramePolicy.ExactBatchFrame ||
                command.Frame.Value != frame.Value)
            {
                result = MobaInputCommandResult.Rejected(
                    command,
                    MobaInputCommandFailureCode.FramePolicyRejected,
                    $"Input command frame policy rejected. policy={contract.FramePolicy}, batch={frame.Value}, command={command.Frame.Value}");
                return false;
            }

            var payloadError = contract.PayloadValidator == null
                ? "validator missing"
                : null;
            if (contract.PayloadValidator == null ||
                !contract.PayloadValidator(command.Payload, out payloadError))
            {
                result = MobaInputCommandResult.Rejected(
                    command,
                    MobaInputCommandFailureCode.PayloadInvalid,
                    $"Input command payload does not match schema. schema={contract.PayloadSchema}, error={payloadError}");
                return false;
            }

            result = default;
            return true;
        }

        private static bool ValidateMovePayload(byte[] payload, out string error)
        {
            return MobaMoveCodec.TryDeserialize(payload, out _, out _, out error);
        }

        private static bool ValidateSkillInputPayload(byte[] payload, out string error)
        {
            return SkillInputCodec.TryDeserialize(payload, out _, out error);
        }

        private static bool ValidateDebugSpawnUnitPayload(byte[] payload, out string error)
        {
            return MobaDebugSpawnUnitCodec.TryDeserialize(payload, out _, out error);
        }

        private static bool ValidateDebugReplaceHeroPayload(byte[] payload, out string error)
        {
            return MobaDebugReplaceHeroCodec.TryDeserialize(payload, out _, out error);
        }
    }

    [MobaInputCommandHandler(MobaOpCodes.Input.DebugSpawnUnit)]
    public sealed class MobaDebugSpawnUnitInputCommandHandler : IMobaInputCommandHandler
    {
        private const float SpawnForwardOffset = 2f;
        private const float SpawnSideOffset = 1.25f;

        public bool Handle(
            MobaInputCommandContext context,
            FrameIndex frame,
            PlayerInputCommand command,
            out MobaInputCommandResult result)
        {
            if (context == null)
            {
                result = MobaInputCommandResult.Rejected(command, MobaInputCommandFailureCode.ContextMissing);
                return false;
            }

            if (context.Phase == null || !context.Phase.InGame)
            {
                result = MobaInputCommandResult.Rejected(command, MobaInputCommandFailureCode.NotInGame);
                return false;
            }

            if (!MobaDebugSpawnUnitCodec.TryDeserialize(command.Payload, out var relation, out var payloadError))
            {
                result = MobaInputCommandResult.Rejected(
                    command,
                    MobaInputCommandFailureCode.PayloadInvalid,
                    $"PayloadInvalid(Player={command.Player.Value},Error={payloadError})");
                return false;
            }

            if (context.PlayerActorMap == null ||
                !context.PlayerActorMap.TryGetActorId(command.Player, out var sourceActorId))
            {
                result = MobaInputCommandResult.Rejected(command, MobaInputCommandFailureCode.ActorMapMissing);
                return false;
            }

            if (!context.TryGetEntity(sourceActorId, out var sourceActor) || sourceActor == null)
            {
                result = MobaInputCommandResult.Rejected(
                    command,
                    MobaInputCommandFailureCode.ActorEntityMissing,
                    sourceActorId);
                return false;
            }

            if (!sourceActor.hasTransform)
            {
                result = MobaInputCommandResult.Rejected(
                    command,
                    MobaInputCommandFailureCode.TransformMissing,
                    sourceActorId);
                return false;
            }

            if (!TryResolveDependencies(
                    context,
                    out var startSpecs,
                    out var actorSpawn,
                    out var initializer,
                    out var spawnSnapshots,
                    out var dependencyError))
            {
                result = MobaInputCommandResult.Rejected(
                    command,
                    MobaInputCommandFailureCode.HandlerRejected,
                    dependencyError,
                    sourceActorId);
                return false;
            }

            if (!startSpecs.TryGet(out var startSpec) ||
                !TryResolveTemplate(
                    startSpec.EnterReq.Players,
                    command.Player,
                    sourceActor,
                    relation,
                    out var template,
                    out var teamId))
            {
                result = MobaInputCommandResult.Rejected(
                    command,
                    MobaInputCommandFailureCode.HandlerRejected,
                    "spawn template is unavailable",
                    sourceActorId);
                return false;
            }

            var spawnPosition = ResolveSpawnPosition(sourceActor, relation);
            var loadout = CreateUnitLoadout(in template, command.Player, teamId, in spawnPosition);
            var spec = MobaConverter.ToActorBuildSpec(actorId: 0, in loadout);
            var request = MobaActorSpawnRequest.FromSpec(in spec);
            request.AllocateActorIdIfMissing = true;
            request.Initializer = (entity, _) => InitializeLoadoutOrThrow(initializer, entity, in loadout);

            if (!actorSpawn.TrySpawn(in request, out var spawnResult) || !spawnResult.Success)
            {
                result = MobaInputCommandResult.Rejected(
                    command,
                    MobaInputCommandFailureCode.HandlerRejected,
                    string.IsNullOrEmpty(spawnResult.Error) ? "actor spawn failed" : spawnResult.Error,
                    sourceActorId);
                return false;
            }

            spawnSnapshots.Enqueue(new MobaActorSpawnSnapshotEntry
            {
                NetId = spawnResult.ActorId,
                Kind = (int)SpawnEntityKind.Character,
                Code = loadout.HeroId,
                OwnerNetId = sourceActorId,
                X = spawnPosition.X,
                Y = spawnPosition.Y,
                Z = spawnPosition.Z,
            });

            result = MobaInputCommandResult.Accepted(
                command,
                $"Spawned(Relation={relation},Actor={spawnResult.ActorId},Frame={frame.Value})",
                spawnResult.ActorId);
            return true;
        }

        private static bool TryResolveDependencies(
            MobaInputCommandContext context,
            out MobaGameStartSpecService startSpecs,
            out IMobaActorSpawnService actorSpawn,
            out ActorEntityInitPipeline initializer,
            out MobaActorSpawnSnapshotService spawnSnapshots,
            out string error)
        {
            startSpecs = null;
            actorSpawn = null;
            initializer = null;
            spawnSnapshots = null;
            error = null;

            var services = context.Services;
            if (services == null)
            {
                error = "world services are unavailable";
                return false;
            }

            if (!services.TryResolve(out startSpecs) || startSpecs == null)
            {
                error = "game start spec service is unavailable";
                return false;
            }

            if (!services.TryResolve(out actorSpawn) || actorSpawn == null)
            {
                error = "actor spawn service is unavailable";
                return false;
            }

            if (!services.TryResolve(out initializer) || initializer == null)
            {
                error = "actor initializer is unavailable";
                return false;
            }

            if (!services.TryResolve(out spawnSnapshots) || spawnSnapshots == null)
            {
                error = "actor spawn snapshot service is unavailable";
                return false;
            }

            return true;
        }

        private static bool TryResolveTemplate(
            MobaPlayerLoadout[] players,
            PlayerId commandPlayer,
            global::ActorEntity sourceActor,
            MobaDebugSpawnUnitRelation relation,
            out MobaPlayerLoadout template,
            out int teamId)
        {
            template = default;
            teamId = sourceActor.hasTeam ? (int)sourceActor.team.Value : 0;
            if (players == null || players.Length == 0 || teamId <= 0) return false;

            var hasCommandTemplate = false;
            for (var i = 0; i < players.Length; i++)
            {
                var candidate = players[i];
                if (candidate.PlayerId.Equals(commandPlayer))
                {
                    template = candidate;
                    hasCommandTemplate = true;
                }

                var candidateIsEnemy = candidate.TeamId > 0 && candidate.TeamId != teamId;
                if (relation == MobaDebugSpawnUnitRelation.Enemy && candidateIsEnemy)
                {
                    template = candidate;
                    teamId = candidate.TeamId;
                    return true;
                }
            }

            if (!hasCommandTemplate) return false;
            if (relation == MobaDebugSpawnUnitRelation.Enemy)
            {
                teamId = teamId == (int)Team.Team1 ? (int)Team.Team2 : (int)Team.Team1;
            }

            return true;
        }

        private static Vec3 ResolveSpawnPosition(
            global::ActorEntity sourceActor,
            MobaDebugSpawnUnitRelation relation)
        {
            var basePosition = sourceActor.transform.Value.Position;
            var side = relation == MobaDebugSpawnUnitRelation.Enemy
                ? SpawnSideOffset
                : -SpawnSideOffset;
            return basePosition + new Vec3(side, 0f, SpawnForwardOffset);
        }

        private static MobaPlayerLoadout CreateUnitLoadout(
            in MobaPlayerLoadout template,
            PlayerId ownerPlayer,
            int teamId,
            in Vec3 spawnPosition)
        {
            return new MobaPlayerLoadout(
                ownerPlayer,
                teamId,
                template.HeroId,
                template.AttributeTemplateId,
                template.Level,
                template.BasicAttackSkillId,
                template.SkillIds,
                template.SpawnIndex,
                (int)UnitSubType.Minion,
                (int)EntityMainType.Unit,
                hasSpawnPosition: 1,
                spawnX: spawnPosition.X,
                spawnY: spawnPosition.Y,
                spawnZ: spawnPosition.Z);
        }

        private static void InitializeLoadoutOrThrow(
            ActorEntityInitPipeline initializer,
            global::ActorEntity entity,
            in MobaPlayerLoadout loadout)
        {
            if (!initializer.TryInitializeFromLoadout(entity, in loadout, out var error))
            {
                throw new InvalidOperationException(error ?? "actor loadout initialization failed");
            }
        }
    }

    public static class MobaHeroLoadoutResolver
    {
        public static bool TryResolve(
            MobaConfigDatabase config,
            PlayerId playerId,
            int teamId,
            int heroId,
            int level,
            int spawnIndex,
            in Vec3 spawnPosition,
            out MobaPlayerLoadout loadout,
            out string error)
        {
            loadout = default;
            error = null;
            if (config == null)
            {
                error = "config database is unavailable";
                return false;
            }

            if (!TryResolveHeroConfig(
                    config,
                    heroId,
                    out var character,
                    out var basicAttackSkillId,
                    out var skills,
                    out error))
            {
                return false;
            }

            loadout = new MobaPlayerLoadout(
                playerId,
                teamId,
                heroId,
                character.AttributeTemplateId,
                level > 0 ? level : 1,
                basicAttackSkillId,
                skills,
                spawnIndex,
                (int)UnitSubType.Hero,
                (int)EntityMainType.Unit,
                hasSpawnPosition: 1,
                spawnX: spawnPosition.X,
                spawnY: spawnPosition.Y,
                spawnZ: spawnPosition.Z);
            return true;
        }

        public static bool TryResolveHeroConfig(
            MobaConfigDatabase config,
            int heroId,
            out CharacterMO character,
            out int basicAttackSkillId,
            out int[] activeSkillIds,
            out string error)
        {
            if (!MobaResolvedHeroLoadoutResolver.TryResolve(
                    config,
                    heroId,
                    out var resolved,
                    out error))
            {
                character = null;
                basicAttackSkillId = 0;
                activeSkillIds = Array.Empty<int>();
                return false;
            }

            character = resolved.Character;
            basicAttackSkillId = resolved.BasicAttackSkillId;
            activeSkillIds = resolved.ActiveSkillIds;
            return true;
        }
    }

    [MobaSnapshotEmitter(25)]
    [WorldService(typeof(MobaPlayerHeroChangedSnapshotService))]
    public sealed class MobaPlayerHeroChangedSnapshotService :
        LogicWorldSnapshotBufferEmitterBase<MobaPlayerHeroChangedSnapshotService, MobaPlayerHeroChangedSnapshotEntry>
    {
        private readonly MobaLogicWorldRunGateService _phase;

        public MobaPlayerHeroChangedSnapshotService(MobaLogicWorldRunGateService phase) : base(4, 32)
        {
            _phase = phase ?? throw new ArgumentNullException(nameof(phase));
        }

        public void Enqueue(in MobaPlayerHeroChangedSnapshotEntry entry)
        {
            if (entry.ActorId <= 0 || string.IsNullOrEmpty(entry.PlayerId)) return;
            Add(entry);
        }

        protected override bool CanEmit(FrameIndex frame)
        {
            return _phase.InGame;
        }

        protected override WorldStateSnapshot CreateSnapshot(MobaPlayerHeroChangedSnapshotEntry[] entries)
        {
            return new WorldStateSnapshot(
                MobaOpCodes.Snapshot.PlayerHeroChanged,
                MobaPlayerHeroChangedSnapshotCodec.Serialize(entries));
        }
    }

    [MobaInputCommandHandler(MobaOpCodes.Input.DebugReplaceHero)]
    public sealed class MobaDebugReplaceHeroInputCommandHandler : IMobaInputCommandHandler
    {
        public bool Handle(
            MobaInputCommandContext context,
            FrameIndex frame,
            PlayerInputCommand command,
            out MobaInputCommandResult result)
        {
            Log.Info($"[MobaDebugReplaceHeroInputCommandHandler] Handling authoritative command. frame={frame.Value}, commandFrame={command.Frame.Value}, playerId={command.Player.Value}, payloadBytes={command.Payload?.Length ?? 0}");
            if (context == null)
            {
                result = MobaInputCommandResult.Rejected(command, MobaInputCommandFailureCode.ContextMissing);
                return false;
            }

            if (context.Phase == null || !context.Phase.InGame)
            {
                result = MobaInputCommandResult.Rejected(command, MobaInputCommandFailureCode.NotInGame);
                return false;
            }

            if (!MobaDebugReplaceHeroCodec.TryDeserialize(command.Payload, out var heroId, out var payloadError))
            {
                result = MobaInputCommandResult.Rejected(
                    command,
                    MobaInputCommandFailureCode.PayloadInvalid,
                    payloadError);
                return false;
            }

            if (context.PlayerActorMap == null ||
                !context.PlayerActorMap.TryGetActorId(command.Player, out var previousActorId))
            {
                result = MobaInputCommandResult.Rejected(command, MobaInputCommandFailureCode.ActorMapMissing);
                return false;
            }

            if (!context.TryGetEntity(previousActorId, out var previousActor) || previousActor == null)
            {
                result = MobaInputCommandResult.Rejected(
                    command,
                    MobaInputCommandFailureCode.ActorEntityMissing,
                    previousActorId);
                return false;
            }

            if (!previousActor.hasTransform || !previousActor.hasTeam)
            {
                result = MobaInputCommandResult.Rejected(
                    command,
                    MobaInputCommandFailureCode.TransformMissing,
                    previousActorId);
                return false;
            }

            var services = context.Services;
            if (services == null ||
                !services.TryResolve(out MobaConfigDatabase config) || config == null ||
                !services.TryResolve(out MobaGameStartSpecService startSpecs) || startSpecs == null ||
                !services.TryResolve(out IMobaHeroReplacementTransactionService replacement) || replacement == null)
            {
                result = MobaInputCommandResult.Rejected(
                    command,
                    MobaInputCommandFailureCode.HandlerRejected,
                    "hero replacement dependencies are unavailable",
                    previousActorId);
                return false;
            }

            var position = previousActor.transform.Value.Position;
            var level = ResolvePlayerLevel(startSpecs, command.Player);
            if (!MobaHeroLoadoutResolver.TryResolve(
                    config,
                    command.Player,
                    (int)previousActor.team.Value,
                    heroId,
                    level,
                    spawnIndex: 0,
                    in position,
                    out var loadout,
                    out var loadoutError))
            {
                result = MobaInputCommandResult.Rejected(
                    command,
                    MobaInputCommandFailureCode.HandlerRejected,
                    loadoutError,
                    previousActorId);
                return false;
            }

            var replacementRequest = new MobaHeroReplacementRequest(
                command.Player,
                frame,
                previousActorId,
                previousActor,
                in loadout);
            if (!replacement.TryReplace(in replacementRequest, out var replacementResult) ||
                !replacementResult.Success)
            {
                result = MobaInputCommandResult.Rejected(
                    command,
                    MobaInputCommandFailureCode.HandlerRejected,
                    string.IsNullOrEmpty(replacementResult.Error)
                        ? $"hero replacement failed at {replacementResult.FailureStage}"
                        : replacementResult.Error,
                    previousActorId);
                return false;
            }

            result = MobaInputCommandResult.Accepted(
                command,
                $"HeroReplaced(Hero={heroId},PreviousActor={previousActorId},Actor={replacementResult.ActorId})",
                replacementResult.ActorId);
            return true;
        }

        private static int ResolvePlayerLevel(MobaGameStartSpecService startSpecs, PlayerId playerId)
        {
            if (startSpecs == null || !startSpecs.TryGet(out var startSpec)) return 1;
            var players = startSpec.EnterReq.Players;
            if (players == null) return 1;

            for (var i = 0; i < players.Length; i++)
            {
                if (players[i].PlayerId.Equals(playerId))
                {
                    return players[i].Level > 0 ? players[i].Level : 1;
                }
            }

            return 1;
        }
    }
}
