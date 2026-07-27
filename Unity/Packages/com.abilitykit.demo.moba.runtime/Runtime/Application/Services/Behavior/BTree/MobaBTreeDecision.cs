using System;
using AbilityKit.Ability.Behavior;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Mathematics;
using AbilityKit.Moba.Behavior;
using BTCore.Runtime;
using BTCore.Runtime.Blackboards;
using Newtonsoft.Json;

namespace AbilityKit.Demo.Moba.Services.Behavior.BTree
{
    /// <summary>
    /// BT 驱动决策：把一个 BTCore 行为树包装成 <see cref="IBehaviorDecision"/>。
    ///
    /// 每 tick 流程：
    /// 1. 同步黑板输入：owner 位置/速度、最近敌人（registry 扫描，队伍过滤）
    /// 2. <see cref="BTCore.Runtime.BTree.Update()"/> 推进树
    /// 3. 读取黑板输出 out.hasMove/out.moveX/out.moveZ 翻译成 Movement
    ///
    /// 树结束（Success/Failure）时不会终止大脑——由决策层保持 Running 继续下一评估周期，
    /// 并调用 <see cref="BTCore.Runtime.BTree.Restart()"/> 让树回到初始状态。
    /// </summary>
    internal sealed class MobaBTreeDecision : IBehaviorDecision
    {
        private readonly global::BTCore.Runtime.BTree _tree;
        private readonly MobaActorRegistry _registry;

        public string DecisionType => "MobaBTree";
        public string CurrentState { get; private set; } = "Running";

        private MobaBTreeDecision(global::BTCore.Runtime.BTree tree, MobaActorRegistry registry)
        {
            _tree = tree;
            _registry = registry;
        }

        public static MobaBTreeDecision Create(string json, MobaActorRegistry registry)
        {
            if (string.IsNullOrEmpty(json)) return null;

            // 编辑器导出的 JSON 用 BTEXT: 占位类型名（跨 Unity/src 程序集名不一致——
            // Unity 是 AbilityKit.Demo.Moba.Runtime，src 测试是 AbilityKit.Demo.Moba.Core），
            // 加载时替换为当前程序集的 AssemblyQualifiedName。
            json = json.Replace(
                "\"$type\": \"BTEXT:MobaChaseNearestEnemyAction\"",
                "\"$type\": \"" + typeof(MobaChaseNearestEnemyAction).AssemblyQualifiedName + "\"");

            // BTCore 的程序集名同样因宿主而异（Unity asmdef=BTRuntime，src=AbilityKit.BTCore），
            // 统一替换为当前加载的 BTree 所在程序集名。
            // 覆盖两种形态：顶层类型 ", BTRuntime" 与嵌套泛型 ", BTRuntime]]"。
            var btAssemblyName = typeof(global::BTCore.Runtime.BTree).Assembly.GetName().Name;
            json = json.Replace(", BTRuntime\"", ", " + btAssemblyName + "\"");
            json = json.Replace(", BTRuntime]]", ", " + btAssemblyName + "]]");

            global::BTCore.Runtime.BTree tree;
            try
            {
                tree = JsonConvert.DeserializeObject<global::BTCore.Runtime.BTree>(json, BTDef.SerializerSettingsAuto);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[MobaBTreeDecision] deserialize behavior tree failed");
                return null;
            }

            if (tree == null) return null;

            tree.RebuildTree();
            tree.Enable();
            return new MobaBTreeDecision(tree, registry);
        }

        public DecisionResult Decide(IBehaviorContext context, IWorldQuery world)
        {
            if (_tree == null || context == null || world == null) return DecisionResult.Continue(CurrentState);

            var bb = _tree.Blackboard;
            SyncBlackboard(bb, context, world);

            _tree.Update();

            if (_tree.TreeState == BTCore.Runtime.NodeState.Success
                || _tree.TreeState == BTCore.Runtime.NodeState.Failure)
            {
                _tree.Restart();
            }

            if (bb.GetValue<bool>("out.hasMove"))
            {
                var moveX = bb.GetValue<float>("out.moveX");
                var moveZ = bb.GetValue<float>("out.moveZ");
                var speed = world.GetMoveSpeed(context.OwnerId, 5f);
                CurrentState = "Chasing";
                return DecisionResult.Continue(CurrentState)
                    .WithMovement(new Vec3(moveX, 0f, moveZ), null, speed);
            }

            CurrentState = "Holding";
            return DecisionResult.Continue(CurrentState);
        }

        private void SyncBlackboard(Blackboard bb, IBehaviorContext context, IWorldQuery world)
        {
            var ownerPos = world.GetPosition(context.OwnerId);
            bb.SetValue("owner.x", ownerPos.X);
            bb.SetValue("owner.z", ownerPos.Z);
            bb.SetValue("owner.speed", world.GetMoveSpeed(context.OwnerId, 5f));

            var enemyId = FindNearestEnemy(world, context.OwnerId, ownerPos);
            if (enemyId > 0)
            {
                var enemyPos = world.GetPosition(new BehaviorEntityId(enemyId));
                bb.SetValue("enemy.valid", true);
                bb.SetValue("enemy.x", enemyPos.X);
                bb.SetValue("enemy.z", enemyPos.Z);
                bb.SetValue("enemy.dist", world.GetDistanceToPosition(context.OwnerId, enemyPos));
            }
            else
            {
                bb.SetValue("enemy.valid", false);
            }
        }

        private int FindNearestEnemy(IWorldQuery world, BehaviorEntityId ownerId, Vec3 ownerPos)
        {
            if (_registry == null) return 0;

            var ownerTeam = 0;
            if (world is AbilityKit.Moba.Behavior.MobaWorldQuery moba)
            {
                ownerTeam = moba.GetTeam(ownerId);
            }

            var bestId = 0;
            var bestDistSq = float.MaxValue;

            foreach (var kv in _registry.Entries)
            {
                var e = kv.Value;
                if (e == null || !e.isEnabled || !e.hasTransform) continue;
                if (kv.Key == ownerId.Value) continue;

                if (e.hasTeam)
                {
                    var team = (int)e.team.Value;
                    if (ownerTeam != 0 && team != 0 && team == ownerTeam) continue;
                }

                if (e.hasAttributeGroup && e.attributeGroup.Group != null
                    && e.attributeGroup.Group.GetValue(Attributes.MobaAttributeIds.HP) <= 0f)
                {
                    continue;
                }

                var delta = e.transform.Value.Position - ownerPos;
                var distSq = delta.SqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestId = kv.Key;
                }
            }

            return bestId;
        }
    }
}
