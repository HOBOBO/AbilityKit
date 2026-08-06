using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.Battle.SkillLibrary;
using AbilityKit.Battle.SearchTarget;
using AbilityKit.Battle.SearchTarget.Rules;
using AbilityKit.Battle.SearchTarget.Scorers;
using AbilityKit.Battle.SearchTarget.Selectors;
using AbilityKit.Combat;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Dataflow;
using AbilityKit.Samples.Abstractions;
using SearchVec2 = AbilityKit.Battle.SearchTarget.Vec2;

namespace AbilityKit.Samples.Logic.Samples.Combat
{
    [Sample(700, "combat", "entity", "package-api", "web")]
    public sealed class CombatEntityKeyedIndex : SampleBase
    {
        public override string Title => "Combat Entity Keyed Index";
        public override string Description => "使用 BattleEntityManager 为战斗实体建立阵营、类型和标签索引";
        public override SampleCategory Category => SampleCategory.Combat;

        protected override void OnRun()
        {
            var world = SampleCombatWorld.CreateLane();

            Section("实体注册");
            KeyValue("Registry.Count", world.EntityManager.Registry.Count.ToString());
            KeyValue("CombatEntityIndex.RegistryCount", world.EntityManager.Registry.Count.ToString());
            foreach (var entity in world.Entities.OrderBy(x => x.Id))
            {
                KeyValue(entity.Id.ToString(), $"{entity.Name}, faction={entity.Faction}, kind={entity.Kind}, pos={CombatSampleFormatting.FormatVec3(entity.Position)}");
            }

            Divider();
            Section("KeyedEntityIndex");
            KeyValue("Heroes", string.Join(", ", world.ByFaction.Get(SampleFaction.Heroes)));
            KeyValue("CombatEntityIndex.Heroes", string.Join(", ", world.ByFaction.Get(SampleFaction.Heroes)));
            KeyValue("Monsters", string.Join(", ", world.ByFaction.Get(SampleFaction.Monsters)));
            KeyValue("Monster kind", string.Join(", ", world.ByKind.Get(SampleEntityKind.Monster)));

            Divider();
            Section("MultiKeyEntityIndex");
            KeyValue("tag:enemy", string.Join(", ", world.ByTag.Get("enemy")));
            KeyValue("tag:elite", string.Join(", ", world.ByTag.Get("elite")));

            world.EntityManager.Remove(2002);
            KeyValue("After Remove(2002), tag:enemy", string.Join(", ", world.ByTag.Get("enemy")));
            KeyValue("CombatEntityIndex.EnemiesAfterRemove", string.Join(", ", world.ByTag.Get("enemy")));
        }
    }

    [Sample(701, "combat", "skill-library", "package-api", "web")]
    public sealed class CombatSkillLibraryIndex : SampleBase
    {
        public override string Title => "Combat Skill Library Index";
        public override string Description => "使用 SkillLibrary 和派生索引管理技能配置";
        public override SampleCategory Category => SampleCategory.Combat;

        protected override void OnRun()
        {
            var library = new SkillLibrary<int, SampleSkillData>();
            var bySchool = library.CreateDerivedKeyedIndex<SampleSkillSchool>(skill => skill.School);
            var byTag = library.CreateDerivedMultiKeyIndex<string>(skill => skill.Tags, StringComparer.OrdinalIgnoreCase);

            library.Add(10001, new SampleSkillData("Fireball", SampleSkillSchool.Fire, 8000, "damage", "projectile"));
            library.Add(10002, new SampleSkillData("Holy Light", SampleSkillSchool.Holy, 12000, "heal", "target"));
            library.Add(10003, new SampleSkillData("Flame Zone", SampleSkillSchool.Fire, 15000, "damage", "area"));

            Section("技能目录");
            KeyValue("Count", library.Count.ToString());
            KeyValue("CombatSkillLibrary.Count", library.Count.ToString());
            foreach (var id in library.Keys.OrderBy(x => x))
            {
                var skill = library.Get(id);
                KeyValue(id.ToString(), $"{skill.Name}, school={skill.School}, cd={skill.CooldownMs}ms, tags={string.Join("/", skill.Tags)}");
            }

            Divider();
            Section("派生索引");
            KeyValue("Fire school", string.Join(", ", bySchool.Get(SampleSkillSchool.Fire)));
            KeyValue("CombatSkillLibrary.FireSchool", string.Join(", ", bySchool.Get(SampleSkillSchool.Fire)));
            KeyValue("tag:damage", string.Join(", ", byTag.Get("damage")));

            library.Update(10002, new SampleSkillData("Holy Light", SampleSkillSchool.Holy, 9000, "heal", "target", "support"), new SkillUpdate(1, "balance"));
            KeyValue("tag:support after update", string.Join(", ", byTag.Get("support")));
            KeyValue("CombatSkillLibrary.SupportAfterUpdate", string.Join(", ", byTag.Get("support")));
        }
    }

    [Sample(702, "combat", "targeting", "package-api", "web")]
    public sealed class CombatTargetingIndexProvider : SampleBase
    {
        public override string Title => "Combat Targeting Index Provider";
        public override string Description => "从实体索引提供候选，再通过 TargetSearchEngine 过滤、评分和选择";
        public override SampleCategory Category => SampleCategory.Combat;

        protected override void OnRun()
        {
            var world = SampleCombatWorld.CreateLane();
            var engine = new TargetSearchEngine();
            var context = new SearchContext
            {
                PositionProvider = world.Positions,
                EntityKeyProvider = world.Positions
            };

            var candidateIds = world.ByFaction.Get(SampleFaction.Monsters);
            var query = SearchPipelineBuilder.Create()
                .From(new EntityCollectionCandidateProvider(candidateIds))
                .Filter(new CircleShapeRule(new SearchVec2(0f, 0f), 7f))
                .ScoreBy(new DistanceToEntityScorer(new EntityId(1001)))
                .Select(new TopKByScoreSelector())
                .Take(2)
                .Build();

            var results = new List<EntityId>();
            engine.SearchIds(in query, context, results);

            Section("候选来源");
            KeyValue("ByFaction(Monsters)", string.Join(", ", candidateIds));
            KeyValue("CombatTargeting.Candidates", string.Join(", ", candidateIds));
            KeyValue("Shape", "Circle origin=(0,0), radius=7");
            KeyValue("Score", "nearest to actor 1001");

            Divider();
            Section("搜索结果");
            KeyValue("Result ids", CombatSampleFormatting.FormatIds(results));
            KeyValue("CombatTargeting.Results", CombatSampleFormatting.FormatIds(results));
            foreach (var id in results)
            {
                if (id.Value <= int.MaxValue &&
                    world.TryGet((int)id.Value, out var entity))
                {
                    KeyValue(entity.Label, CombatSampleFormatting.FormatVec3(entity.Position));
                }
            }
        }
    }

    [Sample(703, "combat", "damage", "dataflow", "package-api", "web")]
    public sealed class CombatDamagePipelineSample : SampleBase
    {
        public override string Title => "Combat Damage Pipeline";
        public override string Description => "使用 DamageCalculationPipeline 计算暴击、加成、魔抗、护盾和实际伤害";
        public override SampleCategory Category => SampleCategory.Combat;

        protected override void OnRun()
        {
            var world = SampleCombatWorld.CreateLane();
            var attacker = world.Entities.First(x => x.Id == 1001);
            var target = world.Entities.First(x => x.Id == 2001);

            var request = DamageRequest.Create(
                source: "Fireball",
                attacker: attacker.Label,
                target: target.Label,
                baseValue: 40f,
                damageType: DamageType.Magic,
                sourceType: DamageSourceType.Ability);

            var context = new DamageCalculationContext
            {
                Request = request,
                Result = DamageResult.Create(request),
                TargetArmor = target.Armor,
                TargetMagicResist = target.MagicResist,
                TargetCurrentHealth = target.Hp,
                TargetMaxHealth = target.Hp,
                AttackerMagicDamage = attacker.MagicPower,
                AttackerPhysicalDamage = attacker.PhysicalAttack
            };

            context.SetData(DamageSlots.CritChance, 0.5f);
            context.SetData(DamageSlots.CritRoll, 0.25f);
            context.SetData(DamageSlots.CritMultiplier, 1.5f);
            context.SetData(DamageSlots.DamageBonusPercent, 0.10f);
            context.SetData(DamageSlots.TargetShield, 12f);

            var pipeline = DamageCalculationPipeline.CreateDefault();
            var result = pipeline.Execute(request, context);
            var damage = result.Output;
            target.Hp -= damage.ActualDamage;

            Section("输入");
            KeyValue("Attacker", attacker.Label);
            KeyValue("Target", $"{target.Label}, MR={context.TargetMagicResist}, Shield=12");
            KeyValue("Request", "Magic base=40, critChance=50%, critRoll=25%, bonus=10%");

            Divider();
            Section("处理结果");
            KeyValue("ProcessedCount", result.ProcessedCount.ToString());
            KeyValue("CombatDamage.ProcessedCount", result.ProcessedCount.ToString());
            KeyValue("IsCritical", damage.IsCritical.ToString());
            KeyValue("RawDamage", damage.RawDamage.ToString("F1"));
            KeyValue("ResistReduction", damage.ResistReduction.ToString("F1"));
            KeyValue("FinalDamage", damage.FinalDamage.ToString("F1"));
            KeyValue("ShieldDamage", damage.ShieldDamage.ToString("F1"));
            KeyValue("ActualDamage", damage.ActualDamage.ToString("F1"));
            KeyValue("CombatDamage.ActualDamage", damage.ActualDamage.ToString("F1"));
            KeyValue("TargetHpAfterApply", target.Hp.ToString("F1"));
            KeyValue("CombatDamage.TargetHpAfterApply", target.Hp.ToString("F1"));
        }
    }

    [Sample(704, "combat", "collision", "package-api", "web")]
    public sealed class CombatCollisionRaycast : SampleBase
    {
        public override string Title => "Combat Collision Raycast";
        public override string Description => "使用 NaiveCollisionWorld 在纯逻辑环境中做 Raycast 命中查询";
        public override SampleCategory Category => SampleCategory.Combat;

        protected override void OnRun()
        {
            var world = SampleCombatWorld.CreateLane();
            var ray = new Ray3(new Vec3(0f, 0f, 0f), Vec3.Right);

            Section("碰撞世界");
            KeyValue("LayerMask", "MonsterLayer");
            KeyValue("Ray", "origin=(0,0,0), direction=Right, distance=20");

            Divider();
            var monsterFilter = new AbilityKit.Combat.Collision.LayerFilter(SampleCombatWorld.MonsterLayer);
            if (world.Collision.Raycast(ray, 20f, in monsterFilter, out var hit))
            {
                world.TryGetByCollider(hit.Collider, out var entity);
                KeyValue("HitCollider", hit.Collider.ToString());
                KeyValue("HitActor", entity?.Label ?? "unknown");
                KeyValue("CombatCollision.HitActor", entity?.Label ?? "unknown");
                KeyValue("HitDistance", hit.Distance.ToString("F2"));
                KeyValue("CombatCollision.HitDistance", hit.Distance.ToString("F2"));
                KeyValue("HitPoint", CombatSampleFormatting.FormatVec3(hit.Point));
            }
            else
            {
                Warn("No hit.");
            }
        }
    }

    [Sample(705, "combat", "area", "collision", "package-api", "web")]
    public sealed class CombatAreaEnterStayExit : SampleBase
    {
        public override string Title => "Combat Area Enter Stay Exit";
        public override string Description => "使用 AreaWorld 生成 Enter/Stay/Exit/Expire 事件";
        public override SampleCategory Category => SampleCategory.Combat;

        protected override void OnRun()
        {
            var world = SampleCombatWorld.CreateLane();
            var areas = new AreaWorld(world.Collision);
            var spawnEvents = new List<AreaSpawnEvent>();
            var enterEvents = new List<AreaEnterEvent>();
            var stayEvents = new List<AreaStayEvent>();
            var exitEvents = new List<AreaExitEvent>();
            var expireEvents = new List<AreaExpireEvent>();

            var area = areas.Spawn(
                new AreaSpawnParams(1001, new Vec3(5.5f, 0f, 0f), radius: 2.2f, lifetimeFrames: 3, collisionLayerMask: SampleCombatWorld.MonsterLayer, stayIntervalFrames: 1),
                frame: 0,
                spawnEvents);

            Section("Area spawn");
            KeyValue("AreaId", area.ToString());
            KeyValue("CombatArea.AreaId", area.ToString());
            KeyValue("Center", "(5.5, 0, 0)");
            KeyValue("Radius", "2.2");

            for (var frame = 1; frame <= 5; frame++)
            {
                if (frame == 3)
                {
                    world.Move(2003, 10f, 0f);
                    Info("Frame 3: Shaman moves out of area.");
                }

                areas.Tick(frame, enterEvents, stayEvents, exitEvents, expireEvents);
                DrainAreaEvents(world, frame, enterEvents, stayEvents, exitEvents, expireEvents);
            }
        }

        private void DrainAreaEvents(
            SampleCombatWorld world,
            int frame,
            List<AreaEnterEvent> enterEvents,
            List<AreaStayEvent> stayEvents,
            List<AreaExitEvent> exitEvents,
            List<AreaExpireEvent> expireEvents)
        {
            Section($"Frame {frame}");

            foreach (var e in enterEvents)
            {
                world.TryGetByCollider(e.Collider, out var actor);
                KeyValue("Enter", actor?.Label ?? e.Collider.ToString());
                KeyValue($"CombatArea.Frame[{frame}].Enter", actor?.Label ?? e.Collider.ToString());
            }
            foreach (var e in stayEvents)
            {
                world.TryGetByCollider(e.Collider, out var actor);
                KeyValue("Stay", actor?.Label ?? e.Collider.ToString());
                KeyValue($"CombatArea.Frame[{frame}].Stay", actor?.Label ?? e.Collider.ToString());
            }
            foreach (var e in exitEvents)
            {
                world.TryGetByCollider(e.Collider, out var actor);
                KeyValue("Exit", actor?.Label ?? e.Collider.ToString());
                KeyValue($"CombatArea.Frame[{frame}].Exit", actor?.Label ?? e.Collider.ToString());
            }
            foreach (var e in expireEvents)
            {
                KeyValue("Expire", e.Area.ToString());
            }

            if (enterEvents.Count == 0 && stayEvents.Count == 0 && exitEvents.Count == 0 && expireEvents.Count == 0)
            {
                Info("No area events.");
            }

            enterEvents.Clear();
            stayEvents.Clear();
            exitEvents.Clear();
            expireEvents.Clear();
        }
    }

    [Sample(706, "combat", "projectile", "collision", "package-api", "web")]
    public sealed class CombatProjectileBasicHit : SampleBase
    {
        public override string Title => "Combat Projectile Basic Hit";
        public override string Description => "使用 ProjectileWorld 发射命中即消失的单发投射物";
        public override SampleCategory Category => SampleCategory.Combat;

        protected override void OnRun()
        {
            var world = SampleCombatWorld.CreateLane();
            var projectiles = new ProjectileWorld(world.Collision);
            var hits = new List<ProjectileHitEvent>();
            var exits = new List<ProjectileExitEvent>();
            var ticks = new List<ProjectileTickEvent>();

            var spawn = CreateFireballSpawn(hitPolicyKind: ProjectileHitPolicyKind.ExitOnHit, hitPolicyParam: 0, hitsRemaining: 1);
            var projectile = projectiles.Spawn(in spawn);

            Section("Projectile spawn");
            KeyValue("ProjectileId", projectile.ToString());
            KeyValue("CombatProjectile.ProjectileId", projectile.ToString());
            KeyValue("Policy", "ExitOnHit");

            CombatProjectileSampleSupport.RunProjectileFrames(Section, KeyValue, Info, world, projectiles, hits, exits, ticks, applyDamage: false);
        }

        internal static ProjectileSpawnParams CreateFireballSpawn(ProjectileHitPolicyKind hitPolicyKind, int hitPolicyParam, int hitsRemaining)
        {
            return new ProjectileSpawnParams(
                ownerId: 1001,
                templateId: 3001,
                launcherActorId: 1001,
                rootActorId: 1001,
                spawnFrame: 0,
                position: new Vec3(0f, 0f, 0f),
                direction: Vec3.Right,
                speed: 10f,
                returnAfterFrames: 0,
                returnSpeed: 0f,
                returnStopDistance: 0f,
                lifetimeFrames: 10,
                maxDistance: 20f,
                collisionLayerMask: SampleCombatWorld.MonsterLayer,
                ignoreCollider: default,
                hitPolicy: null,
                hitsRemaining: hitsRemaining,
                hitPolicyKind: hitPolicyKind,
                hitPolicyParam: hitPolicyParam,
                tickIntervalFrames: 1,
                hitFilter: null,
                hitCooldownFrames: 0);
        }

    }

    [Sample(707, "combat", "projectile", "damage", "package-api", "web")]
    public sealed class CombatProjectileHitDamage : SampleBase
    {
        public override string Title => "Combat Projectile Hit Damage";
        public override string Description => "把 ProjectileHitEvent 接到 DamageCalculationPipeline 并落地扣血";
        public override SampleCategory Category => SampleCategory.Combat;

        protected override void OnRun()
        {
            var world = SampleCombatWorld.CreateLane();
            var projectiles = new ProjectileWorld(world.Collision);
            var hits = new List<ProjectileHitEvent>();
            var exits = new List<ProjectileExitEvent>();
            var ticks = new List<ProjectileTickEvent>();

            var spawn = CombatProjectileBasicHit.CreateFireballSpawn(ProjectileHitPolicyKind.ExitOnHit, hitPolicyParam: 0, hitsRemaining: 1);
            var projectile = projectiles.Spawn(in spawn);

            Section("Skill chain");
            KeyValue("Cast", "Knight casts Fireball");
            KeyValue("CombatProjectileDamage.Cast", "Knight casts Fireball");
            KeyValue("ProjectileId", projectile.ToString());
            KeyValue("OnHit", "DamagePipeline -> ApplyHp");

            CombatProjectileSampleSupport.RunProjectileFrames(Section, KeyValue, Info, world, projectiles, hits, exits, ticks, applyDamage: true);
        }
    }

    internal static class CombatProjectileSampleSupport
    {
        public static void RunProjectileFrames(
            Action<string> section,
            Action<string, string> keyValue,
            Action<string> info,
            SampleCombatWorld world,
            ProjectileWorld projectiles,
            List<ProjectileHitEvent> hits,
            List<ProjectileExitEvent> exits,
            List<ProjectileTickEvent> ticks,
            bool applyDamage)
        {
            for (var frame = 1; frame <= 8; frame++)
            {
                projectiles.Tick(frame, 0.1f, hits, exits, ticks);
                section($"Frame {frame}");

                foreach (var tick in ticks)
                {
                    keyValue("Tick", $"projectile={tick.Projectile}, pos={CombatSampleFormatting.FormatVec3(tick.Position)}");
                keyValue($"CombatProjectile.Frame[{frame}].Tick", $"projectile={tick.Projectile}, pos={CombatSampleFormatting.FormatVec3(tick.Position)}");
                }

                foreach (var hit in hits)
                {
                    world.TryGetByCollider(hit.HitCollider, out var actor);
                    keyValue("Hit", $"{actor?.Label ?? hit.HitCollider.ToString()} at {CombatSampleFormatting.FormatVec3(hit.Point)}, count={hit.HitCount}");
                    keyValue($"CombatProjectile.Frame[{frame}].Hit", $"{actor?.Label ?? hit.HitCollider.ToString()} at {CombatSampleFormatting.FormatVec3(hit.Point)}, count={hit.HitCount}");

                    if (applyDamage && actor != null && world.TryGet(hit.OwnerId, out var attacker))
                    {
                        var damage = CombatDamagePipelineSampleSupport.CalculateProjectileDamage(attacker, actor);
                        actor.Hp -= damage.ActualDamage;
                        keyValue("ApplyDamage", $"{damage.ActualDamage:F0}, {actor.Name}.Hp={actor.Hp:F0}");
                        keyValue($"CombatProjectileDamage.Frame[{frame}].ApplyDamage", $"{damage.ActualDamage:F0}, {actor.Name}.Hp={actor.Hp:F0}");
                    }
                }

                foreach (var exit in exits)
                {
                    keyValue("Exit", $"{exit.Reason} at frame {exit.Frame}");
                }

                if (ticks.Count == 0 && hits.Count == 0 && exits.Count == 0)
                {
                    info("No projectile events.");
                }

                ticks.Clear();
                hits.Clear();
                exits.Clear();

                if (projectiles.ActiveCount == 0)
                {
                    keyValue("ActiveCount", "0");
                    keyValue($"CombatProjectile.Frame[{frame}].ActiveCount", "0");
                    break;
                }
            }
        }
    }

    internal static class CombatDamagePipelineSampleSupport
    {
        public static DamageResult CalculateProjectileDamage(SampleCombatEntity attacker, SampleCombatEntity target)
        {
            var request = DamageRequest.Create(
                source: "Projectile.Fireball",
                attacker: attacker.Label,
                target: target.Label,
                baseValue: 35f,
                damageType: DamageType.Magic,
                sourceType: DamageSourceType.Ability);

            var context = new DamageCalculationContext
            {
                Request = request,
                Result = DamageResult.Create(request),
                TargetArmor = target.Armor,
                TargetMagicResist = target.MagicResist,
                TargetCurrentHealth = target.Hp,
                TargetMaxHealth = target.Hp,
                AttackerMagicDamage = attacker.MagicPower,
                AttackerPhysicalDamage = attacker.PhysicalAttack
            };

            context.SetData(DamageSlots.CritChance, 0f);
            context.SetData(DamageSlots.DamageBonusFlat, 5f);

            return DamageCalculationPipeline.CreateDefault().Execute(request, context).Output;
        }
    }
}
