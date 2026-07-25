using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.EntityConstruction;

namespace AbilityKit.Demo.Moba.Services.StateImport
{
    /// <summary>
    /// 待导入的单个 actor 状态（运行时中立结构，不依赖 wire 类型）。
    /// 由网关快照（GatewayStateSyncActorSnapshot）转换而来。
    /// </summary>
    public readonly struct MobaActorStateImport
    {
        public readonly int ActorId;
        public readonly float PosX, PosY, PosZ;
        public readonly float Yaw;
        public readonly float Hp, HpMax;
        public readonly int TeamId;
        public readonly int Kind;      // SpawnEntityKind: 1=Character, 2=Projectile
        public readonly int Code;      // 角色为 modelId/heroId；投射物为模板 id
        public readonly int OwnerNetId;

        public MobaActorStateImport(
            int actorId,
            float posX, float posY, float posZ,
            float yaw,
            float hp, float hpMax,
            int teamId,
            int kind, int code, int ownerNetId)
        {
            ActorId = actorId;
            PosX = posX; PosY = posY; PosZ = posZ;
            Yaw = yaw;
            Hp = hp; HpMax = hpMax;
            TeamId = teamId;
            Kind = kind; Code = code; OwnerNetId = ownerNetId;
        }
    }

    public struct MobaStateImportResult
    {
        public int Updated;
        public int Spawned;
        public int Despawned;
        public int Failed;

        public override string ToString() => $"updated={Updated} spawned={Spawned} despawned={Despawned} failed={Failed}";
    }

    /// <summary>
    /// FullSnapshot → 逻辑世界状态导入服务。
    ///
    /// 用途：断线重连后，预测世界（RemoteDriven）被销毁重建为空世界，
    /// 通过本服务把服务端 FullSnapshot 的 actor 状态导入逻辑世界：
    /// - 已存在 actor：覆写 transform（位置 + yaw→四元数）、HP/MaxHP、Team
    /// - 缺失 actor：按 Kind/Code/OwnerNetId 构建 BuildSpec 生成（投射物/英雄/小兵）
    /// - 多余 actor（仅全量快照）：RequestDespawn 移除
    ///
    /// 导入后世界状态与服务端快照对齐，哈希对账（InGame + Transform + HP）
    /// 可从该帧继续，预测驱动恢复执行。
    /// </summary>
    [WorldService(typeof(MobaLogicWorldStateImporter))]
    public sealed class MobaLogicWorldStateImporter : IService
    {
        private const int KindCharacter = 1;
        private const int KindProjectile = 2;

        private readonly MobaActorRegistry _registry;
        private readonly IMobaActorSpawnService _spawn;
        private readonly MobaConfigDatabase _config;
        private readonly ActorEntityInitPipeline _initPipeline;
        private readonly MobaAuthorityFrameService _authority;

        public MobaLogicWorldStateImporter(
            MobaActorRegistry registry,
            IMobaActorSpawnService spawn,
            MobaConfigDatabase config,
            ActorEntityInitPipeline initPipeline = null,
            MobaAuthorityFrameService authority = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _spawn = spawn; // 允许为 null：缺失 actor 生成退化为仅记录 Failed
            _config = config; // 允许为 null：角色 templateId 解析退化为 0
            _initPipeline = initPipeline; // 允许为 null：属性组初始化跳过
            _authority = authority; // 允许为 null：despawn 帧门槛直接用导入帧
        }

        public MobaStateImportResult Import(IReadOnlyList<MobaActorStateImport> actors, int frame, bool isFullSnapshot)
        {
            var result = new MobaStateImportResult();
            if (actors == null) return result;

            HashSet<int> seen = isFullSnapshot ? new HashSet<int>() : null;

            for (int i = 0; i < actors.Count; i++)
            {
                var a = actors[i];
                if (a.ActorId <= 0) continue;
                seen?.Add(a.ActorId);

                if (_registry.TryGet(a.ActorId, out var entity) && entity != null)
                {
                    ApplyToExisting(entity, in a);
                    result.Updated++;
                }
                else if (TrySpawnMissing(in a))
                {
                    result.Spawned++;
                }
                else
                {
                    result.Failed++;
                }
            }

            if (isFullSnapshot)
            {
                // 全量快照是权威全集：registry 中缺失的 actor 已在断线期间死亡。
                // despawn 门槛帧取 min(导入帧, 当前 confirmed)——快照对"现在"权威，
                // 不应因 confirmed 帧标度（本地世界未绑定 authority 时为 0）而永远挂起。
                var despawnFrame = ResolveDespawnFrame(frame);
                foreach (var kv in _registry.Entries)
                {
                    if (seen.Contains(kv.Key)) continue;
                    ActorLifecycleRequests.RequestDespawn(kv.Value, despawnFrame, ActorDespawnReason.Unknown);
                    result.Despawned++;
                }
            }

            Log.Info($"[MobaLogicWorldStateImporter] Import frame={frame} full={isFullSnapshot} {result}");
            return result;
        }

        private void ApplyToExisting(global::ActorEntity entity, in MobaActorStateImport a)
        {
            if (entity.hasTransform)
            {
                entity.ReplaceTransform(new Transform3(
                    new Vec3(a.PosX, a.PosY, a.PosZ),
                    Quat.FromAxisAngle(Vec3.Up, a.Yaw),
                    Vec3.One));
            }

            if (entity.hasAttributeGroup && entity.attributeGroup.Group != null)
            {
                var group = entity.attributeGroup.Group;
                if (a.HpMax > 0f) group.SetBase(MobaAttributeIds.MAX_HP, a.HpMax);
                group.SetBase(MobaAttributeIds.HP, a.Hp);
            }

            var team = (Team)a.TeamId;
            if (entity.hasTeam)
            {
                if (entity.team.Value != team) entity.ReplaceTeam(team);
            }
            else if (a.TeamId > 0)
            {
                entity.AddTeam(team);
            }
        }

        private bool TrySpawnMissing(in MobaActorStateImport a)
        {
            if (_spawn == null) return false;

            var transform = new Transform3(
                new Vec3(a.PosX, a.PosY, a.PosZ),
                Quat.FromAxisAngle(Vec3.Up, a.Yaw),
                Vec3.One);
            var team = (Team)a.TeamId;

            MobaActorBuildSpec spec;
            if (a.Kind == KindProjectile)
            {
                spec = new MobaActorBuildSpec(
                    new MobaEntityInfo(
                        a.ActorId,
                        MobaEntityKind.Projectile,
                        in transform,
                        team,
                        EntityMainType.Projectile,
                        UnitSubType.Bullet,
                        ownerPlayer: default,
                        templateId: a.Code),
                    MobaActorBuildSourceKind.Projectile,
                    a.Code,
                    a.OwnerNetId);
            }
            else
            {
                // Character：Code 即 heroId/modelId（demo 配置中两者同值），
                // 从角色配置解析属性模板；小兵/召唤物同样按 Unit 处理（P1 简化）。
                var templateId = 0;
                if (_config != null && a.Code > 0
                    && _config.TryGetCharacter(a.Code, out var character) && character != null)
                {
                    templateId = character.AttributeTemplateId;
                }

                spec = new MobaActorBuildSpec(
                    new MobaEntityInfo(
                        a.ActorId,
                        MobaEntityKind.Hero,
                        in transform,
                        team,
                        EntityMainType.Unit,
                        UnitSubType.Hero,
                        ownerPlayer: default,
                        templateId: templateId),
                    MobaActorBuildSourceKind.PlayerLoadout,
                    a.Code,
                    a.OwnerNetId);
            }

            var request = MobaActorSpawnRequest.FromSpec(in spec);
            request.AllocateActorIdIfMissing = false; // 必须使用快照中的 actorId 与服务端对齐
            if (a.Kind == KindProjectile)
            {
                request.PostSetup = new MobaActorSpawnPostSetup
                {
                    SetFlyingProjectileTag = true,
                    SetOwnerLink = a.OwnerNetId > 0,
                    OwnerActorId = a.OwnerNetId,
                    RootOwnerActorId = a.OwnerNetId,
                };
            }

            if (!_spawn.TrySpawn(in request, out var spawnResult) || !spawnResult.Success)
            {
                Log.Warning($"[MobaLogicWorldStateImporter] spawn failed. actorId={a.ActorId} kind={a.Kind} code={a.Code} error={spawnResult.Error}");
                return false;
            }

            // 生成后立刻用快照状态覆写（生成管线按 templateId 初始化 HP，快照值更权威）
            if (spawnResult.Entity != null)
            {
                // Character：生成管线不一定初始化属性组（取决于 SourceKind），
                // 而 Motion 等系统要求属性组存在——用属性模板补齐后再覆写快照值。
                if (a.Kind != KindProjectile
                    && !spawnResult.Entity.hasAttributeGroup
                    && _initPipeline != null)
                {
                    var templateId = spec.Info.TemplateId > 0 ? spec.Info.TemplateId : a.Code;
                    _initPipeline.InitializeFromAttributeTemplate(spawnResult.Entity, templateId);
                }

                ApplyToExisting(spawnResult.Entity, in a);
            }

            return true;
        }

        private int ResolveDespawnFrame(int importFrame)
        {
            if (_authority == null) return importFrame;

            var confirmed = _authority.ConfirmedFrame.Value;
            return confirmed < importFrame ? confirmed : importFrame;
        }

        public void Dispose()
        {
        }
    }
}
