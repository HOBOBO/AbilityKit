// ============================================================================
// HFSM 示例代码
// 包含：分层状态机、切换条件、参数、行为、任意状态转换
// ============================================================================

using UnityEngine;
using UnityHFSM;
using UnityHFSM.Actions;

/// <summary>
/// 角色AI状态机示例 - 展示完整的分层状态机功能
/// 状态结构：
/// - Root: Idle | Patrol | Chase | Attack | Dead
///   - Patrol: PatrolIdle | Moving
///   - Chase: Approaching | Fighting
/// </summary>
public class CharacterAIExample : MonoBehaviour
{
    // 事件定义
    private enum Event
    {
        FoundTarget,
        LostTarget,
        InAttackRange,
        OutOfAttackRange,
        HealthLow,
        HealthRecovered,
        PatrolComplete,
        MoveComplete,
        Dead,
        Respawn
    }

    // 状态机
    private StateMachine<string, Event> _fsm;

    // 组件引用
    private UnityEngine.AI.NavMeshAgent _navAgent;
    private Transform _target;
    private float _health = 100f;
    private float _maxHealth = 100f;

    // 巡逻点
    private Vector3[] _patrolPoints;
    private int _currentPatrolIndex;

    // 参数存储
    private float _distanceToTarget;
    private float _timeInCurrentState;

    // 可视化注册（在编辑器中用于运行时监控）
    private void OnEnable()
    {
        UnityHFSM.Visualization.LiveRegistry.Register("CharacterAI", _fsm);
    }

    private void OnDisable()
    {
        UnityHFSM.Visualization.LiveRegistry.Unregister(_fsm);
    }

    private void Start()
    {
        _navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (_navAgent == null)
        {
            _navAgent = gameObject.AddComponent<UnityEngine.AI.NavMeshAgent>();
        }

        // 初始化巡逻点
        _patrolPoints = new Vector3[]
        {
            transform.position + new Vector3(5, 0, 0),
            transform.position + new Vector3(10, 0, 5),
            transform.position + new Vector3(5, 0, 10),
            transform.position + new Vector3(-5, 0, 5)
        };

        // 构建状态机
        BuildStateMachine();
    }

    private void BuildStateMachine()
    {
        _fsm = new StateMachine<string, Event>();

        // ==================== 根状态机 ====================

        // ---- Idle 状态 ----
        _fsm.AddState("Idle", new State<string, Event>(
            onEnter: state => Debug.Log("[Idle] 进入待机状态"),
            onLogic: state =>
            {
                _timeInCurrentState += Time.deltaTime;
                Debug.Log($"[Idle] 待机中... 已待机 {_timeInCurrentState:F1} 秒");
            },
            onExit: state => Debug.Log("[Idle] 退出待机状态")
        ));

        // ---- Patrol 状态（包含子状态机）----
        var patrolFsm = new StateMachine<string, Event>();

        // Patrol 子状态：PatrolIdle
        patrolFsm.AddState("PatrolIdle", new State<string, Event>(
            onEnter: state => Debug.Log("[Patrol/PatrolIdle] 进入巡逻等待"),
            onLogic: state =>
            {
                _timeInCurrentState += Time.deltaTime;
                // 等待1秒后开始移动
                if (_timeInCurrentState > 1f)
                {
                    _fsm.Trigger(Event.PatrolComplete);
                }
            },
            onExit: state => Debug.Log("[Patrol/PatrolIdle] 退出巡逻等待")
        ));

        // Patrol 子状态：Moving
        patrolFsm.AddState("Moving", new State<string, Event>(
            onEnter: state =>
            {
                Debug.Log($"[Patrol/Moving] 开始移动到巡逻点 {_currentPatrolIndex}");
                if (_navAgent != null && _patrolPoints != null)
                {
                    _navAgent.SetDestination(_patrolPoints[_currentPatrolIndex]);
                }
            },
            onLogic: state =>
            {
                if (_navAgent != null && _navAgent.hasPath)
                {
                    _distanceToTarget = _navAgent.remainingDistance;
                    if (_distanceToTarget < 0.5f)
                    {
                        _currentPatrolIndex = (_currentPatrolIndex + 1) % _patrolPoints.Length;
                        _fsm.Trigger(Event.MoveComplete);
                    }
                }
            },
            onExit: state => Debug.Log("[Patrol/Moving] 停止移动")
        ));

        // Patrol 内部转换
        patrolFsm.AddTransition(new Transition<string, Event>("PatrolIdle", "Moving",
            condition: t => _timeInCurrentState > 1f));
        patrolFsm.AddTransition(new Transition<string, Event>("Moving", "PatrolIdle",
            transition: t => _navAgent != null && _navAgent.remainingDistance < 0.5f));

        patrolFsm.SetStartState("PatrolIdle");

        _fsm.AddState("Patrol", patrolFsm);

        // ---- Chase 状态（包含子状态机）----
        var chaseFsm = new StateMachine<string, Event>();

        // Chase 子状态：Approaching
        chaseFsm.AddState("Approaching", new State<string, Event>(
            onEnter: state => Debug.Log("[Chase/Approaching] 开始追击目标"),
            onLogic: state =>
            {
                if (_target != null && _navAgent != null)
                {
                    _navAgent.SetDestination(_target.position);
                    _distanceToTarget = Vector3.Distance(transform.position, _target.position);

                    if (_distanceToTarget < 2f)
                    {
                        _fsm.Trigger(Event.InAttackRange);
                    }
                    else if (_distanceToTarget > 15f)
                    {
                        _fsm.Trigger(Event.LostTarget);
                    }
                }
            }
        ));

        // Chase 子状态：Fighting
        chaseFsm.AddState("Fighting", new State<string, Event>(
            onEnter: state => Debug.Log("[Chase/Fighting] 进入战斗距离，保持追击"),
            onLogic: state =>
            {
                if (_target != null)
                {
                    _distanceToTarget = Vector3.Distance(transform.position, _target.position);

                    if (_distanceToTarget > 3f)
                    {
                        _fsm.Trigger(Event.OutOfAttackRange);
                    }
                    else if (_distanceToTarget > 15f)
                    {
                        _fsm.Trigger(Event.LostTarget);
                    }
                }
            }
        ));

        chaseFsm.SetStartState("Approaching");

        _fsm.AddState("Chase", chaseFsm);

        // ---- Attack 状态 ----
        _fsm.AddState("Attack", new State<string, Event>(
            onEnter: state => Debug.Log("[Attack] 进入攻击状态"),
            onLogic: state =>
            {
                _timeInCurrentState += Time.deltaTime;
                Debug.Log("[Attack] 执行攻击！");

                // 模拟攻击
                if (_timeInCurrentState > 2f && _target != null)
                {
                    Debug.Log($"[Attack] 对 {_target.name} 造成 20 点伤害");
                    _timeInCurrentState = 0f;
                }
            },
            onExit: state => Debug.Log("[Attack] 退出攻击状态")
        ));

        // ---- Dead 状态 ----
        _fsm.AddState("Dead", new State<string, Event>(
            onEnter: state =>
            {
                Debug.Log("[Dead] 角色死亡");
                if (_navAgent != null) _navAgent.isStopped = true;
            },
            onLogic: state => Debug.Log("[Dead] 等待复活..."),
            needsExitTime: true // 需要退出时间，防止立即转换
        ));

        // ==================== 根状态转换 ====================

        // Patrol -> Idle
        _fsm.AddTransition(new Transition<string, Event>("Idle", "Patrol",
            condition: t => _timeInCurrentState > 5f));

        // Idle -> Chase
        _fsm.AddTransition(new Transition<string, Event>("Idle", "Chase",
            condition: t => _target != null));

        // Patrol -> Idle
        _fsm.AddTransition(new Transition<string, Event>("Patrol", "Idle",
            condition: t => _target != null,
            afterTransition: () => Debug.Log("Patrol -> Idle: 发现目标")));

        // Patrol 完成后循环
        _fsm.AddTransition(new Transition<string, Event>("Patrol", "Patrol",
            condition: t => false, // 由 PatrolComplete 触发
            afterTransition: () => Debug.Log("Patrol 完成，循环巡逻")));

        // Chase -> Attack
        _fsm.AddTransition(new Transition<string, Event>("Chase", "Attack",
            condition: t => _distanceToTarget < 2.5f));

        // Chase -> Idle (丢失目标)
        _fsm.AddTransition(new Transition<string, Event>("Chase", "Idle",
            condition: t => _target == null || _distanceToTarget > 15f,
            afterTransition: () => Debug.Log("Chase -> Idle: 丢失目标")));

        // Attack -> Chase
        _fsm.AddTransition(new Transition<string, Event>("Attack", "Chase",
            condition: t => _distanceToTarget > 3f,
            afterTransition: () => Debug.Log("Attack -> Chase: 目标离开攻击范围")));

        // Attack -> Dead
        _fsm.AddTransition(new Transition<string, Event>("Attack", "Dead",
            condition: t => _health <= 0,
            afterTransition: () => Debug.Log("Attack -> Dead: 生命值耗尽")));

        // Dead -> Idle (复活)
        _fsm.AddTransition(new Transition<string, Event>("Dead", "Idle",
            condition: t => _health > 0,
            forceInstantly: true,
            afterTransition: () =>
            {
                Debug.Log("Dead -> Idle: 复活！");
                _health = _maxHealth;
                _timeInCurrentState = 0f;
            }));

        // ==================== 触发器转换 ====================

        // 任意状态 -> Dead (通过事件)
        _fsm.AddTriggerTransition(Event.Dead, new Transition<string, Event>("", "Dead",
            isExitTransition: false));

        // ==================== 任意状态转换 ====================

        // 任意状态 -> Chase (发现目标时中断当前状态)
        _fsm.AddTransitionFromAny(new Transition<string, Event>("", "Chase",
            condition: t => _target != null && _fsm.ActiveStateName != "Dead" && _fsm.ActiveStateName != "Chase",
            forceInstantly: true,
            afterTransition: () => Debug.Log($"[任意转换] 从 {_fsm.ActiveStateName} 中断，转向目标")));

        // 任意状态 -> Dead (生命值低时)
        _fsm.AddTransitionFromAny(new Transition<string, Event>("", "Dead",
            condition: t => _health <= 0 && _fsm.ActiveStateName != "Dead",
            forceInstantly: true,
            afterTransition: () => Debug.Log($"[任意转换] 从 {_fsm.ActiveStateName} 中断，生命值耗尽")));

        // 设置初始状态
        _fsm.SetStartState("Idle");
        _fsm.Init();
    }

    private void Update()
    {
        // 更新状态机
        _fsm.OnLogic();
    }

    // ==================== 公共方法 ====================

    public void SetTarget(Transform target)
    {
        _target = target;
        Debug.Log($"[CharacterAI] 设置目标: {(target != null ? target.name : "null")}");
    }

    public void TakeDamage(float damage)
    {
        _health -= damage;
        _health = Mathf.Max(0, _health);
        Debug.Log($"[CharacterAI] 受到 {damage} 点伤害，剩余生命值: {_health:F1}");

        if (_health <= 30f)
        {
            _fsm.Trigger(Event.HealthLow);
        }
    }

    public void Heal(float amount)
    {
        _health += amount;
        _health = Mathf.Min(_maxHealth, _health);

        if (_health >= 50f && _health - amount < 50f)
        {
            _fsm.Trigger(Event.HealthRecovered);
        }
    }

    public string GetCurrentState()
    {
        return _fsm != null ? _fsm.ActiveStateName : "NotInitialized";
    }

    public string GetFullStatePath()
    {
        return _fsm != null ? _fsm.GetActiveHierarchyPath() : "NotInitialized";
    }
}
