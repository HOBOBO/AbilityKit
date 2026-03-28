// ============================================================================
// HFSM 示例 - AI角色控制器
// ============================================================================
//
// 功能展示:
// 1. 分层状态机 (Root -> Idle/Patrol/Combat/Dead)
// 2. 嵌套状态机 (Patrol 包含4个巡逻点, Combat 包含 Idle/Move/Attack)
// 3. 转换条件 (基于距离、健康值等)
// 4. 任意状态转换 (AnyState -> Dead)
// 5. 事件系统
// 6. 参数管理
//
// ============================================================================
//
// 状态机结构:
//
//                    Root (HybridStateMachine)
//                    ├── Idle (State) ←→ Patrol
//                    ├── Patrol (HybridSM)
//                    │   └── Patrol1 → Patrol2 → Patrol3 → Patrol4 → Patrol1
//                    ├── Combat (HybridSM)
//                    │   ├── CombatIdle → Attack
//                    │   └── CombatMove → Attack
//                    ├── Dead (State)
//                    └── AnyState ──────────► Dead
//
// ============================================================================

using System;
using UnityEngine;
using UnityHFSM;

namespace UnityHFSM.Examples
{
    // ============================================================================
    // 常量定义
    // ============================================================================
    public static class CharacterEvents
    {
        public const string SeeTarget = "SeeTarget";
        public const string LostTarget = "LostTarget";
        public const string HealthChanged = "HealthChanged";
        public const string AttackFinished = "AttackFinished";
        public const string PatrolPointReached = "PatrolPointReached";
    }

    // ============================================================================
    // 配置类 - 可序列化，方便在Inspector中调整
    // ============================================================================
    [Serializable]
    public class CharacterConfig
    {
        [Header("感知范围")]
        public float detectionRange = 10f;
        public float loseRange = 15f;
        public float attackRange = 2f;

        [Header("移动速度")]
        public float moveSpeed = 5f;
        public float chaseSpeed = 8f;
        public float patrolSpeed = 3f;

        [Header("战斗属性")]
        public float attackCooldown = 1.5f;
        public int maxHealth = 100;

        [Header("巡逻路径")]
        public Vector3[] patrolPoints = new Vector3[]
        {
            new Vector3(0, 0, 5),
            new Vector3(5, 0, 5),
            new Vector3(5, 0, 0),
            new Vector3(0, 0, 0)
        };
    }

    // ============================================================================
    // 参数类 - 管理状态机的运行时状态
    // ============================================================================
    [Serializable]
    public class CharacterParameters
    {
        public float health = 100f;
        public float distanceToTarget = float.MaxValue;
        public bool canSeeTarget = false;
        public bool isInCombat = false;
        public bool isDead = false;
        public float timeSinceLastAttack = 0f;
        public Vector3 targetPosition;
        public int currentPatrolIndex = 0;
    }

    // ============================================================================
    // 主状态机类 - CharacterFSM
    // ============================================================================
    public class CharacterFSM : HybridStateMachine<string, string, string>
    {
        private CharacterConfig config;
        private CharacterParameters parameters;
        private Transform characterTransform;
        private Transform targetTransform;

        public event Action<string> OnStateChanged;
        public event Action<string, string> OnTransition;

        public CharacterFSM(Transform character, Transform target = null, CharacterConfig config = null)
        {
            this.characterTransform = character;
            this.targetTransform = target;
            this.config = config ?? new CharacterConfig();
            this.parameters = new CharacterParameters();

            if (target != null)
            {
                parameters.targetPosition = target.position;
            }

            BuildStateMachine();
            SetStartState("Idle");
        }

        // ========================================================================
        // 状态机构建
        // ========================================================================
        private void BuildStateMachine()
        {
            // ================================================================
            // 1. Idle 状态 - 待机
            // ================================================================
            AddState("Idle", new State(
                onEnter: _ =>
                {
                    Debug.Log("[Idle] OnEnter - 开始待机");
                    parameters.isInCombat = false;
                },
                onLogic: _ =>
                {
                    if (CanDetectTarget())
                    {
                        RequestStateChange("Combat");
                    }
                }
            ));

            // ================================================================
            // 2. Patrol 嵌套状态机 - 巡逻
            // ================================================================
            var patrolSM = new HybridStateMachine<string, string, string>(
                beforeOnEnter: _ => Debug.Log("[Patrol] OnEnter - 开始巡逻"),
                afterOnExit: _ => Debug.Log("[Patrol] OnExit - 结束巡逻"),
                needsExitTime: true
            );

            // 添加4个巡逻点状态
            for (int i = 0; i < config.patrolPoints.Length; i++)
            {
                int nextIndex = (i + 1) % config.patrolPoints.Length;
                Vector3 targetPoint = config.patrolPoints[i];

                patrolSM.AddState($"Patrol{i + 1}", new State(
                    onEnter: _ => Debug.Log($"[Patrol{i + 1}] 移动到巡逻点 {i}"),
                    onLogic: _ =>
                    {
                        if (MoveToPoint(targetPoint))
                        {
                            parameters.currentPatrolIndex = nextIndex;
                            patrolSM.RequestStateChange($"Patrol{nextIndex + 1}");
                        }
                    }
                ));
            }

            // 巡逻点之间的转换
            for (int i = 0; i < config.patrolPoints.Length; i++)
            {
                int nextIndex = (i + 1) % config.patrolPoints.Length;
                patrolSM.AddTransition($"Patrol{i + 1}", $"Patrol{nextIndex + 1}", forceInstantly: true);
            }

            AddState("Patrol", patrolSM);

            // ================================================================
            // 3. Combat 嵌套状态机 - 战斗
            // ================================================================
            HybridStateMachine<string, string, string> combatSM = null;
            combatSM = new HybridStateMachine<string, string, string>(
                beforeOnEnter: _ =>
                {
                    Debug.Log("[Combat] OnEnter - 进入战斗模式");
                    parameters.isInCombat = true;
                },
                afterOnLogic: _ =>
                {
                    if (!CanSeeTarget())
                    {
                        combatSM.RequestExit(forceInstantly: true);
                    }
                },
                afterOnExit: _ =>
                {
                    Debug.Log("[Combat] OnExit - 退出战斗模式");
                    parameters.isInCombat = false;
                },
                needsExitTime: false
            );

            // 战斗待机
            combatSM.AddState("CombatIdle", new State(
                onEnter: _ => Debug.Log("[CombatIdle] 战斗待机"),
                onLogic: _ =>
                {
                    if (IsTargetInRange(config.attackRange))
                    {
                        combatSM.RequestStateChange("Attack");
                    }
                    else if (!IsTargetInRange(config.attackRange) && CanSeeTarget())
                    {
                        combatSM.RequestStateChange("CombatMove");
                    }
                }
            ));

            // 战斗移动
            combatSM.AddState("CombatMove", new State(
                onEnter: _ => Debug.Log("[CombatMove] 向目标移动"),
                onLogic: _ =>
                {
                    MoveTowardsTarget(config.chaseSpeed);
                    if (IsTargetInRange(config.attackRange))
                    {
                        combatSM.RequestStateChange("Attack");
                    }
                }
            ));

            // 攻击
            combatSM.AddState("Attack", new State(
                onEnter: _ =>
                {
                    Debug.Log("[Attack] 执行攻击!");
                    PerformAttack();
                },
                onLogic: _ =>
                {
                    parameters.timeSinceLastAttack += Time.deltaTime;
                    if (parameters.timeSinceLastAttack >= config.attackCooldown)
                    {
                        if (IsTargetInRange(config.attackRange))
                        {
                            PerformAttack();
                        }
                        else
                        {
                            combatSM.RequestStateChange("CombatMove");
                        }
                    }
                }
            ));

            // 战斗状态转换
            combatSM.AddTransition("CombatIdle", "CombatMove", forceInstantly: true);
            combatSM.AddTransition("CombatMove", "CombatIdle", forceInstantly: true);
            combatSM.AddTransition("CombatIdle", "Attack", forceInstantly: true);
            combatSM.AddTransition("CombatMove", "Attack", forceInstantly: true);
            combatSM.AddTransition("Attack", "CombatIdle", forceInstantly: true);
            combatSM.AddTransition("Attack", "CombatMove", forceInstantly: true);

            AddState("Combat", combatSM);

            // ================================================================
            // 4. Dead 状态 - 死亡
            // ================================================================
            AddState("Dead", new State(
                onEnter: _ =>
                {
                    Debug.Log("[Dead] 角色死亡!");
                    parameters.isDead = true;
                }
            ));

            // ================================================================
            // 5. 根状态转换
            // ================================================================
            this.AddTransition("Idle", "Patrol", t => UnityEngine.Random.value > 0.7f);
            this.AddTransition("Patrol", "Idle", t => CanDetectTarget());
            this.AddTransition("Idle", "Combat", t => CanDetectTarget());
            this.AddTransition("Patrol", "Combat", t => CanDetectTarget());

            // ================================================================
            // 6. 任意状态转换
            // ================================================================
            this.AddTransitionFromAny("Dead",
                t => parameters.health <= 0,
                forceInstantly: true
            );

            this.AddTransitionFromAny("Idle",
                t => !parameters.canSeeTarget && !parameters.isInCombat
            );
        }

        // ========================================================================
        // 辅助方法
        // ========================================================================
        private bool CanDetectTarget()
        {
            if (targetTransform == null)
            {
                parameters.canSeeTarget = false;
                return false;
            }

            parameters.distanceToTarget = Vector3.Distance(characterTransform.position, targetTransform.position);
            parameters.canSeeTarget = parameters.distanceToTarget <= config.detectionRange;
            return parameters.canSeeTarget;
        }

        private bool CanSeeTarget()
        {
            if (targetTransform == null)
            {
                parameters.canSeeTarget = false;
                return false;
            }

            parameters.distanceToTarget = Vector3.Distance(characterTransform.position, targetTransform.position);
            parameters.canSeeTarget = parameters.distanceToTarget <= config.loseRange;
            return parameters.canSeeTarget;
        }

        private bool IsTargetInRange(float range) => parameters.distanceToTarget <= range;

        private bool MoveToPoint(Vector3 point)
        {
            Vector3 dir = (point - characterTransform.position).normalized;
            characterTransform.position += dir * config.patrolSpeed * Time.deltaTime;
            return Vector3.Distance(characterTransform.position, point) < 0.5f;
        }

        private void MoveTowardsTarget(float speed)
        {
            if (targetTransform == null) return;
            parameters.targetPosition = targetTransform.position;
            Vector3 dir = (parameters.targetPosition - characterTransform.position).normalized;
            characterTransform.position += dir * speed * Time.deltaTime;
        }

        private void PerformAttack()
        {
            Debug.Log("[Attack] 对目标造成伤害!");
            parameters.timeSinceLastAttack = 0f;
            OnAction(CharacterEvents.AttackFinished);
        }

        // ========================================================================
        // 公共方法
        // ========================================================================
        public void TakeDamage(float damage)
        {
            parameters.health -= damage;
            Debug.Log($"[Character] 受到 {damage} 点伤害，剩余生命值: {parameters.health}");
            OnAction(CharacterEvents.HealthChanged);
        }

        public void SetTarget(Transform target)
        {
            targetTransform = target;
            if (target != null)
            {
                parameters.targetPosition = target.position;
                OnAction(CharacterEvents.SeeTarget);
            }
        }

        public string GetCurrentStateName() => ActiveStateName?.ToString() ?? "None";
        public string GetFullStatePath() => GetType().Name;
        public CharacterParameters GetParameters() => parameters;
        public CharacterConfig GetConfig() => config;

        /// <summary>
        /// 获取可视化的状态路径
        /// </summary>
        public string GetVisualizationPath()
        {
            var parts = new System.Collections.Generic.List<string>();
            StateBase<string> current = ActiveState;

            while (current != null)
            {
                parts.Insert(0, current.name ?? "?");
                if (current is HybridStateMachine<string, string, string> hsm)
                {
                    current = hsm.ActiveState;
                }
                else
                {
                    break;
                }
            }

            return string.Join(" > ", parts);
        }
    }

    // ============================================================================
    // 参数驱动状态机示例
    // ============================================================================
    public class ParameterBasedFSM : HybridStateMachine<string, string, string>
    {
        [Serializable]
        public class Params
        {
            public float health = 100f;
            public float stamina = 100f;
            public float speed = 5f;
        }

        public Params parameters = new Params();

        public ParameterBasedFSM()
        {
            AddState("Normal", new State(
                onEnter: _ => Debug.Log("[Normal] 进入正常状态"),
                onLogic: _ =>
                {
                    if (parameters.health < 30f) RequestStateChange("Weak");
                    if (parameters.health <= 0f) RequestStateChange("Dead");
                    if (parameters.stamina <= 0f) RequestStateChange("Rest");
                }
            ));

            AddState("Weak", new State(
                onEnter: _ => { Debug.Log("[Weak] 进入虚弱状态"); parameters.speed = 2f; },
                onLogic: _ => { if (parameters.health > 50f) RequestStateChange("Normal"); },
                onExit: _ => { parameters.speed = 5f; }
            ));

            AddState("Rest", new State(
                onEnter: _ => Debug.Log("[Rest] 开始休息"),
                onLogic: _ =>
                {
                    parameters.stamina += Time.deltaTime * 20f;
                    if (parameters.stamina >= 100f)
                    {
                        parameters.stamina = 100f;
                        RequestStateChange("Normal");
                    }
                }
            ));

            AddState("Dead", new State(
                onEnter: _ => Debug.Log("[Dead] 角色死亡")
            ));

            this.AddTransition("Normal", "Weak", t => parameters.health < 30f);
            this.AddTransition("Weak", "Normal", t => parameters.health > 50f);
            this.AddTransition("Normal", "Rest", t => parameters.stamina <= 0f);
            this.AddTransition("Rest", "Normal", t => parameters.stamina >= 100f);
            this.AddTransitionFromAny("Dead", t => parameters.health <= 0f, forceInstantly: true);

            SetStartState("Normal");
        }
    }

    // ============================================================================
    // 触发器驱动状态机示例
    // ============================================================================
    public class TriggerBasedFSM : HybridStateMachine<string, string, string>
    {
        public static class Triggers
        {
            public const string Jump = "Jump";
            public const string Land = "Land";
            public const string Attack = "Attack";
            public const string Dodge = "Dodge";
        }

        public TriggerBasedFSM()
        {
            AddState("Grounded", new State(
                onEnter: _ => Debug.Log("[Grounded] 着地状态")
            ));

            AddState("Jumping", new State(
                onEnter: _ => Debug.Log("[Jumping] 跳跃中...")
            ));

            AddState("Attacking", new State(
                onEnter: _ => Debug.Log("[Attacking] 攻击中!"),
                onExit: _ => Debug.Log("[Attacking] 攻击结束")
            ));

            AddState("Dodging", new State(
                onEnter: _ => Debug.Log("[Dodging] 闪避中...")
            ));

            this.AddTriggerTransition(Triggers.Jump, "Grounded", "Jumping", forceInstantly: true);
            this.AddTriggerTransition(Triggers.Land, "Jumping", "Grounded", forceInstantly: true);
            this.AddTriggerTransition(Triggers.Attack, "Grounded", "Attacking", forceInstantly: true);
            this.AddTriggerTransitionFromAny(Triggers.Dodge, "Dodging", forceInstantly: true);

            SetStartState("Grounded");
        }

        public void TriggerJump() => Trigger(Triggers.Jump);
        public void TriggerAttack() => Trigger(Triggers.Attack);
        public void TriggerDodge() => Trigger(Triggers.Dodge);
    }
}
