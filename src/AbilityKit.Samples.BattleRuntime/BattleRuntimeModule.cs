using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Battle.SearchTarget;
using AbilityKit.Battle.SearchTarget.Rules;
using AbilityKit.Battle.SearchTarget.Scorers;
using AbilityKit.Battle.SearchTarget.Selectors;
using AbilityKit.Combat;
using AbilityKit.Combat.Collision;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Mathematics;
using AbilityKit.Dataflow;
using AbilityKit.Samples.Foundation;
using AbilityKit.Samples.SkillCore;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using SearchVec2 = AbilityKit.Battle.SearchTarget.Vec2;

namespace AbilityKit.Samples.BattleRuntime;

/// <summary>
/// BattleRuntime 业务模块：在 SkillCore 之上注册碰撞、投射物、目标查找与战斗驱动服务，
/// 形成「目标选择 → 投射物命中 → 伤害结算 → 事件/触发规则」的完整链路。
/// </summary>
public sealed class BattleRuntimeModule : IWorldModule, IWorldModuleInfo
{
    public string Id => "battle";

    public int Order => 10;

    public Type[] DependsOn => Array.Empty<Type>();

    public Type[] ConflictsWith => Array.Empty<Type>();

    public void Configure(WorldContainerBuilder builder)
    {
        builder.RegisterInstance(new EventBus());
        builder.RegisterInstance(new CollisionService());
        builder.Register<IProjectileService>(
            WorldLifetime.Singleton,
            r => new ProjectileService(r.Resolve<CollisionService>()));
        builder.Register<IBattleService>(
            WorldLifetime.Singleton,
            r => new BattleService(
                r.Resolve<EventBus>(),
                r.Resolve<CollisionService>(),
                r.Resolve<IProjectileService>()));
        builder.Register<IFoundationTickLoop>(
            WorldLifetime.Singleton,
            r => (IFoundationTickLoop)r.Resolve<IBattleService>());
    }
}

/// <summary>战斗单位（怪物）：位置、血量与碰撞体由业务层自管。</summary>
public sealed class Monster
{
    public int Id { get; }

    public string Name { get; }

    public float X { get; }

    public float Z { get; }

    public float Hp { get; set; }

    public float MaxHp { get; }

    public float MagicResist { get; }

    public ColliderId Collider { get; set; }

    public Monster(int id, string name, float x, float z, float hp, float magicResist)
    {
        Id = id;
        Name = name;
        X = x;
        Z = z;
        Hp = hp;
        MaxHp = hp;
        MagicResist = magicResist;
    }
}

/// <summary>targeting 的位置提供者：同一份 2D 位置同时服务查找引擎的过滤与评分。</summary>
public sealed class BattlePositionProvider : IPositionProvider, IEntityKeyProvider
{
    private readonly Dictionary<int, SearchVec2> _positions = new();

    public void Set(int actorId, float x, float z) => _positions[actorId] = new SearchVec2(x, z);

    public bool TryGetPosition(EntityId entity, out SearchVec2 position)
    {
        if (entity.Value <= int.MaxValue && _positions.TryGetValue((int)entity.Value, out var p))
        {
            position = p;
            return true;
        }

        position = SearchVec2.Zero;
        return false;
    }

    public ulong GetKey(EntityId id) => id.Value;
}

/// <summary>从怪物集合推送候选（targeting 的候选来源由接入方实现）。</summary>
public sealed class MonsterCandidateProvider : ICandidateProvider
{
    private readonly IReadOnlyCollection<int> _ids;

    public MonsterCandidateProvider(IReadOnlyCollection<int> ids)
    {
        _ids = ids ?? Array.Empty<int>();
    }

    public void ForEachCandidate<TConsumer>(in SearchQuery query, SearchContext context, ref TConsumer consumer)
        where TConsumer : struct, ICandidateConsumer
    {
        foreach (var id in _ids)
        {
            consumer.Consume(new EntityId(id));
        }
    }
}

public interface IBattleService : IService
{
    IReadOnlyList<Monster> Monsters { get; }

    void CastFireballVolley();
}

/// <summary>
/// 战斗驱动服务：施放时用 targeting 选目标、用 projectile 发射；
/// 每帧 Tick 驱动投射物，命中后用 damage 管线结算，并把伤害事件发回
/// SkillCore 同款 EventBus 供触发规则消费。
/// </summary>
public sealed class BattleService : IBattleService, IFoundationTickLoop
{
    private const int PlayerId = 1001;
    private const int MonsterLayerId = 1;
    private const int MonsterLayerMask = 1 << MonsterLayerId;

    private readonly EventBus _bus;
    private readonly CollisionService _collisions;
    private readonly IProjectileService _projectiles;
    private readonly TargetSearchEngine _search = new();
    private readonly BattlePositionProvider _positions = new();
    private readonly Dictionary<ColliderId, int> _colliderToMonster = new();
    private readonly List<Monster> _monsters = new();
    private readonly List<ProjectileHitEvent> _hitBuffer = new(8);
    private readonly List<ProjectileExitEvent> _exitBuffer = new(8);
    private TriggerRunner<TriggerContext>? _runner;
    private int _frame;

    public IReadOnlyList<Monster> Monsters => _monsters;

    public BattleService(EventBus bus, CollisionService collisions, IProjectileService projectiles)
    {
        _bus = bus;
        _collisions = collisions;
        _projectiles = projectiles;

        SpawnMonsters();
        SetupHeavyWoundRule();
    }

    /// <summary>施放「火球齐射」：玩家周围半径 8 内按距离取最近 2 个目标，各发射一枚投射物。</summary>
    public void CastFireballVolley()
    {
        var context = new SearchContext
        {
            PositionProvider = _positions,
            EntityKeyProvider = _positions
        };

        var ids = new List<int>(_monsters.Count);
        foreach (var m in _monsters)
        {
            ids.Add(m.Id);
        }

        var query = SearchPipelineBuilder.Create()
            .From(new MonsterCandidateProvider(ids))
            .Filter(new CircleShapeRule(new SearchVec2(0f, 0f), 8f))
            .ScoreBy(new DistanceToEntityScorer(new EntityId(PlayerId)))
            .Select(new TopKByScoreSelector())
            .Take(2)
            .Build();

        var results = new List<EntityId>();
        _search.SearchIds(in query, context, results);

        Log.Info($"[Targeting] 半径 8 内候选 [{string.Join(", ", ids)}]，按距离取最近 {results.Count} 个：{FormatIds(results)}");

        foreach (var id in results)
        {
            var target = FindMonster((int)id.Value);
            if (target == null)
            {
                continue;
            }

            var direction = DirectionTo(target);
            _projectiles.Spawn(new ProjectileSpawnParams(
                ownerId: PlayerId,
                templateId: 3001,
                launcherActorId: PlayerId,
                rootActorId: PlayerId,
                spawnFrame: _frame,
                position: new Vec3(0f, 0f, 0f),
                direction: direction,
                speed: 15f,
                returnAfterFrames: 0,
                returnSpeed: 0f,
                returnStopDistance: 0f,
                lifetimeFrames: 90,
                maxDistance: 25f,
                collisionLayerMask: MonsterLayerMask,
                ignoreCollider: default,
                hitFilter: null));
            Log.Info($"[Projectile] 向 {target.Name}({target.Id}) 发射火球，速度 15，ExitOnHit");
        }
    }

    public void Tick(float deltaTime)
    {
        _frame++;
        _projectiles.Tick(_frame, deltaTime);

        _hitBuffer.Clear();
        _projectiles.DrainHitEvents(_hitBuffer);
        foreach (var hit in _hitBuffer)
        {
            if (_colliderToMonster.TryGetValue(hit.HitCollider, out var monsterId))
            {
                ApplyDamage(FindMonster(monsterId));
            }
        }

        _exitBuffer.Clear();
        _projectiles.DrainExitEvents(_exitBuffer);
        foreach (var exit in _exitBuffer)
        {
            Log.Info($"[Projectile] 投射物退出：reason={exit.Reason}, frame={exit.Frame}");
        }

        _bus.Flush();
    }

    public void Dispose()
    {
    }

    private void SpawnMonsters()
    {
        AddMonster(new Monster(2001, "Goblin", 3f, 1f, hp: 120f, magicResist: 10f));
        AddMonster(new Monster(2002, "Orc", 6f, -2f, hp: 150f, magicResist: 20f));
        AddMonster(new Monster(2003, "Shaman", 9f, 0.5f, hp: 100f, magicResist: 0f));

        foreach (var m in _monsters)
        {
            Log.Info($"[Battlefield] {m.Name}({m.Id}) 位置 ({m.X:0.#}, {m.Z:0.#})，HP {m.Hp:0}，MR {m.MagicResist:0}");
        }
    }

    private void AddMonster(Monster monster)
    {
        _monsters.Add(monster);
        _positions.Set(monster.Id, monster.X, monster.Z);
        monster.Collider = _collisions.World.Add(
            new Transform3(new Vec3(monster.X, 0f, monster.Z), Quat.Identity, Vec3.One),
            ColliderShape.CreateSphere(new Sphere(Vec3.Zero, 0.6f)),
            MonsterLayerId);
        _colliderToMonster[monster.Collider] = monster.Id;
    }

    /// <summary>命中结算：damage 管线计算暴击/加成/魔抗/护盾后的实际伤害并扣血。</summary>
    private void ApplyDamage(Monster? target)
    {
        if (target == null)
        {
            return;
        }

        var request = DamageRequest.Create(
            source: "FireballVolley",
            attacker: "Player",
            target: target.Name,
            baseValue: 30f,
            damageType: DamageType.Magic,
            sourceType: DamageSourceType.Ability);

        var context = new DamageCalculationContext
        {
            Request = request,
            Result = DamageResult.Create(request),
            TargetArmor = 0f,
            TargetMagicResist = target.MagicResist,
            TargetCurrentHealth = target.Hp,
            TargetMaxHealth = target.MaxHp,
            AttackerMagicDamage = 20f,
            AttackerPhysicalDamage = 10f
        };
        context.SetData(DamageSlots.CritChance, 0.25f);
        context.SetData(DamageSlots.CritRoll, 0.1f);
        context.SetData(DamageSlots.CritMultiplier, 1.5f);
        context.SetData(DamageSlots.DamageBonusPercent, 0.1f);

        var output = DamageCalculationPipeline.CreateDefault().Execute(request, context).Output;
        target.Hp -= output.ActualDamage;

        Log.Info($"[Damage] {target.Name}：raw {output.RawDamage:0.#}，crit={output.IsCritical}，resist -{output.ResistReduction:0.#}，实际 {output.ActualDamage:0.#}，剩余 HP {Math.Max(target.Hp, 0f):0.#}");

        _bus.Publish(new EventKey<DamageEvent>(StableStringId.Get("event:damage")), new DamageEvent((int)output.ActualDamage));
    }

    /// <summary>注册「重伤告警」规则：单次伤害 ≥ 35 触发（复用 SkillCore 的事件与 payload 契约）。</summary>
    private void SetupHeavyWoundRule()
    {
        var payloads = new PayloadAccessorRegistry();
        payloads.RegisterIntAccessor(new DamageEventPayloadAccessor());

        _runner = new TriggerRunner<TriggerContext>(
            _bus,
            new FunctionRegistry(),
            new ActionRegistry(),
            contextSource: null,
            observer: null,
            blackboards: new DictionaryBlackboardResolver(),
            payloads: payloads);

        var expr = new RpnNumericExprRuntime(
            new RpnNumericExprPlan(RpnNumericExprParser.LangRpnV1, "payload:amount"));

        _runner.Register(
            new EventKey<DamageEvent>(StableStringId.Get("event:damage")),
            new DelegateTrigger<DamageEvent, TriggerContext>(
                predicate: (evt, ctx) => expr.Eval(in evt, in ctx) >= 35,
                actions: (evt, ctx) => Log.Info($"[Trigger] 重伤告警：单次伤害 {expr.Eval(in evt, in ctx):0.#} ≥ 35")),
            phase: 0,
            priority: 0);
    }

    private static Vec3 DirectionTo(Monster target)
    {
        float len = MathF.Sqrt(target.X * target.X + target.Z * target.Z);
        return new Vec3(target.X / len, 0f, target.Z / len);
    }

    private Monster? FindMonster(int id)
    {
        foreach (var m in _monsters)
        {
            if (m.Id == id)
            {
                return m;
            }
        }

        return null;
    }

    private static string FormatIds(List<EntityId> ids)
        => ids.Count == 0 ? "[]" : "[" + string.Join(", ", ids.ConvertAll(id => id.Value.ToString())) + "]";
}
