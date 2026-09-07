using System;
using System.Collections.Generic;
using System.Text;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.DI;
using AbilityKit.Combat.Collision;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.EnvironmentModel;

namespace AbilityKit.Demo.Moba.Services.EnvironmentModel
{
    /// <summary>
    /// MOBA 的 <c>IEnvironmentProfileBinder&lt;int&gt;</c>：把解析后的环境原语翻译成 MOBA 实体生成，handle=actorId。
    /// 这是「业务实现放实现包」的落点——框架只给形状，这里负责把 <see cref="SpawnPrimitive"/> 变成真正的 <see cref="IMobaActorSpawnService"/> 调用。
    /// 支持 <see cref="SpawnPrimitive"/> 及其 <c>Components</c>（生成时覆盖数值，如 hp=500）；Obstacle/Tag/Modifier 待后续切片（MOBA 障碍是地图级几何，标签/修饰是组件操作）。
    /// </summary>
    public sealed class MobaEnvironmentProfileBinder : IEnvironmentProfileBinder<int>
    {
        private readonly IWorldResolver _services;

        /// <summary>构造一个 MOBA 环境 binder，用给定的服务解析器构造实体。</summary>
        public MobaEnvironmentProfileBinder(IWorldResolver services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        /// <inheritdoc/>
        public EnvironmentBindResult<int> Bind(in ResolvedEnvironmentProfile profile)
        {
            var handles = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var primitive in profile.Primitives)
            {
                switch (primitive)
                {
                    case SpawnPrimitive spawn:
                        SpawnActors(spawn, handles);
                        break;
                    case ObstaclePrimitive obstacle:
                        PlaceObstacle(obstacle);
                        break;
                }
            }

            return new EnvironmentBindResult<int>(handles);
        }

        /// <summary>放置一个静态障碍物（墙体/立柱）：把原语的 Shape/Position/Size 变成碰撞世界里的一个 collider（WorldId 层）。返回碰撞体 id。</summary>
        public ColliderId PlaceObstacle(ObstaclePrimitive obstacle)
        {
            if (obstacle == null) return default;
            var collision = _services.Resolve<ICollisionService>();
            var world = collision?.World;
            if (world == null) return default;

            var position = obstacle.Position;
            var size = obstacle.Size;
            var transform = new Transform3(new Vec3(position.X, position.Y, position.Z), Quat.Identity, Vec3.One);
            var shape = CreateObstacleShape(obstacle.Shape, size);

            return world.Add(in transform, in shape, MobaCollisionLayers.WorldId);
        }

        private static ColliderShape CreateObstacleShape(string shape, EnvironmentVector3 size)
        {
            switch (shape?.ToLowerInvariant())
            {
                case "sphere":
                    var radius = System.Math.Max(size.X, System.Math.Max(size.Y, size.Z)) * 0.5f;
                    return ColliderShape.CreateSphere(Vec3.Zero, radius);
                case "box":
                default:
                    return ColliderShape.CreateObb(
                        Vec3.Zero,
                        Quat.Identity,
                        new Vec3(size.X * 0.5f, size.Y * 0.5f, size.Z * 0.5f));
            }
        }

        private void SpawnActors(SpawnPrimitive spawn, Dictionary<string, int> handles)
        {
            var spawnService = _services.Resolve<IMobaActorSpawnService>();
            var kind = ResolveKind(spawn.EntityKind);
            var mainType = ResolveMainType(kind);
            var unitSubType = ResolveUnitSubType(kind);

            for (var i = 0; i < spawn.Count; i++)
            {
                var position = spawn.Position ?? EnvironmentVector3.Zero;
                var transform = new Transform3(
                    new Vec3(position.X, position.Y, position.Z),
                    Quat.Identity,
                    Vec3.One);

                var info = new MobaEntityInfo(
                    actorId: 0,
                    kind: kind,
                    transform: transform,
                    team: Team.Neutral,
                    mainType: mainType,
                    unitSubType: unitSubType,
                    ownerPlayer: default,
                    templateId: 0);

                var spec = new MobaActorBuildSpec(in info, MobaActorBuildSourceKind.Unknown, 0, 0);
                var request = MobaActorSpawnRequest.FromSpec(in spec);
                request.AllocateActorIdIfMissing = true;

                if (spawnService.TrySpawn(in request, out var result))
                {
                    ApplyComponents(result.Entity, spawn.Components);

                    // Count > 1 时给别名加下标：jungle_0 / jungle_1 / ...；Count == 1 保持原名。
                    var alias = spawn.Count > 1 ? $"{spawn.Alias}_{i}" : spawn.Alias;
                    if (!string.IsNullOrEmpty(alias))
                    {
                        handles[alias] = result.ActorId;
                    }
                }
            }
        }

        /// <summary>把 <see cref="SpawnPrimitive.Components"/>（key-value，如 hp=500 / moveSpeed=6）作为生成时的数值覆盖应用到实体。</summary>
        private void ApplyComponents(global::ActorEntity entity, IReadOnlyDictionary<string, string> components)
        {
            if (entity == null || components == null || components.Count == 0) return;

            // 确保属性容器存在（环境实体默认裸生成、无 AttributeGroup/ResourceContainer）。
            _services.Resolve<ActorEntityInitPipeline>().InitializeFromAttributeTemplate(entity, 0);
            var attrs = new MobaAttrs(entity);

            foreach (var pair in components)
            {
                ApplyComponent(attrs, pair.Key, pair.Value);
            }
        }

        private static void ApplyComponent(MobaAttrs attrs, string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number)) return;

            if (string.Equals(key, "hp", StringComparison.OrdinalIgnoreCase)) { attrs.Hp = number; return; }
            if (string.Equals(key, "mana", StringComparison.OrdinalIgnoreCase)) { attrs.Mana = number; return; }
            if (string.Equals(key, "rage", StringComparison.OrdinalIgnoreCase)) { attrs.Rage = number; return; }

            var normalized = ToUpperSnake(key);
            if (Enum.TryParse(normalized, ignoreCase: true, out BattleAttributeType type) && type != BattleAttributeType.None)
                attrs.SetBase(type, number);
        }

        /// <summary>把 camelCase / 带点横线的键转成 UPPER_SNAKE，匹配 <see cref="BattleAttributeType"/> 枚举名（moveSpeed → MOVE_SPEED）。</summary>
        private static string ToUpperSnake(string key)
        {
            var sb = new StringBuilder(key.Length + 4);
            for (var i = 0; i < key.Length; i++)
            {
                var c = key[i];
                if (char.IsLetterOrDigit(c))
                {
                    if (i > 0 && char.IsUpper(c) && (char.IsLower(key[i - 1]) || char.IsDigit(key[i - 1])))
                        sb.Append('_');
                    sb.Append(char.ToUpperInvariant(c));
                }
                else if (c != ' ')
                {
                    sb.Append('_');
                }
            }
            return sb.ToString();
        }

        private static MobaEntityKind ResolveKind(string entityKind)
        {
            if (!string.IsNullOrEmpty(entityKind)
                && Enum.TryParse(entityKind, ignoreCase: true, out MobaEntityKind parsed)
                && parsed != MobaEntityKind.Unknown)
            {
                return parsed;
            }

            return MobaEntityKind.Hero;
        }

        private static EntityMainType ResolveMainType(MobaEntityKind kind)
            => kind == MobaEntityKind.Summon ? EntityMainType.Summon : EntityMainType.Unit;

        private static UnitSubType ResolveUnitSubType(MobaEntityKind kind)
        {
            switch (kind)
            {
                case MobaEntityKind.Minion:
                    return UnitSubType.Minion;
                case MobaEntityKind.Monster:
                    return UnitSubType.Neutral;
                case MobaEntityKind.Summon:
                    return UnitSubType.None;
                default:
                    return UnitSubType.Hero;
            }
        }
    }
}
