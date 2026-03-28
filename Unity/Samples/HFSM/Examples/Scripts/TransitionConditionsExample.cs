// ============================================================================
// HFSM 转换条件示例 - 展示各种转换条件的用法
// ============================================================================

using UnityEngine;
using UnityHFSM;

/// <summary>
/// 转换条件示例 - 展示各种条件类型的转换
/// </summary>
public class TransitionConditionsExample : MonoBehaviour
{
    // 使用枚举作为状态ID（更类型安全）
    private enum StateID
    {
        Idle,
        Moving,
        Attacking,
        Defending,
        Dead
    }

    // 触发事件
    private enum Trigger
    {
        StartMove,
        StopMove,
        StartAttack,
        StopAttack,
        TakeDamage,
        HealthRestored,
        Die,
        Revive
    }

    // 模拟数据
    private float _health = 100f;
    private float _stamina = 100f;
    private bool _isAlert = false;
    private float _distanceToEnemy = 10f;

    private StateMachine<StateID, Trigger> _fsm;

    private void Start()
    {
        BuildStateMachine();
    }

    private void BuildStateMachine()
    {
        _fsm = new StateMachine<StateID, Trigger>();

        // ---- Idle 状态 ----
        _fsm.AddState(StateID.Idle, new State<StateID, Trigger>(
            onEnter: state => Debug.Log("[Idle] 进入待机"),
            onLogic: state =>
            {
                // 恢复体力
                _stamina = Mathf.Min(100f, _stamina + 5f * Time.deltaTime);
            }
        ));

        // ---- Moving 状态 ----
        _fsm.AddState(StateID.Moving, new State<StateID, Trigger>(
            onEnter: state => Debug.Log("[Moving] 开始移动"),
            onLogic: state =>
            {
                Debug.Log($"[Moving] 移动中... 体力: {_stamina:F1}");
                _stamina -= 10f * Time.deltaTime;
            }
        ));

        // ---- Attacking 状态 ----
        _fsm.AddState(StateID.Attacking, new State<StateID, Trigger>(
            onEnter: state => Debug.Log("[Attacking] 进入攻击"),
            onLogic: state =>
            {
                Debug.Log("[Attacking] 执行攻击！");
            }
        ));

        // ---- Defending 状态 ----
        _fsm.AddState(StateID.Defending, new State<StateID, Trigger>(
            onEnter: state => Debug.Log("[Defending] 进入防御"),
            onLogic: state =>
            {
                Debug.Log("[Defending] 防御中...");
            },
            needsExitTime: true // 需要等待防御动画完成
        ));

        // ---- Dead 状态 ----
        _fsm.AddState(StateID.Dead, new State<StateID, Trigger>(
            onEnter: state => Debug.Log("[Dead] 死亡"),
            onLogic: state => Debug.Log("[Dead] 等待复活...")
        ));

        // ==================== 普通转换（每帧检查条件） ====================

        // Idle -> Moving
        _fsm.AddTransition(new Transition<StateID, Trigger>(
            from: StateID.Idle,
            to: StateID.Moving,
            condition: t => _stamina > 20f && _distanceToEnemy > 3f
        ));

        // Moving -> Idle
        _fsm.AddTransition(new Transition<StateID, Trigger>(
            from: StateID.Moving,
            to: StateID.Idle,
            condition: t => _stamina < 10f || _distanceToEnemy <= 3f
        ));

        // Moving -> Attacking
        _fsm.AddTransition(new Transition<StateID, Trigger>(
            from: StateID.Moving,
            to: StateID.Attacking,
            condition: t => _distanceToEnemy <= 2f
        ));

        // Attacking -> Moving
        _fsm.AddTransition(new Transition<StateID, Trigger>(
            from: StateID.Attacking,
            to: StateID.Moving,
            condition: t => _distanceToEnemy > 3f
        ));

        // Attacking -> Defending (受到攻击时)
        _fsm.AddTransition(new Transition<StateID, Trigger>(
            from: StateID.Attacking,
            to: StateID.Defending,
            condition: t => _health < 30f,
            forceInstantly: true // 强制立即转换
        ));

        // ==================== 触发器转换（需要显式触发） ====================

        // 触发器：开始移动
        _fsm.AddTriggerTransition(Trigger.StartMove, new Transition<StateID, Trigger>(
            from: StateID.Idle,
            to: StateID.Moving,
            condition: t => _stamina > 20f
        ));

        // 触发器：停止移动
        _fsm.AddTriggerTransition(Trigger.StopMove, new Transition<StateID, Trigger>(
            from: StateID.Moving,
            to: StateID.Idle
        ));

        // 触发器：开始攻击
        _fsm.AddTriggerTransition(Trigger.StartAttack, new Transition<StateID, Trigger>(
            from: StateID.Moving,
            to: StateID.Attacking
        ));

        // 触发器：停止攻击
        _fsm.AddTriggerTransition(Trigger.StopAttack, new Transition<StateID, Trigger>(
            from: StateID.Attacking,
            to: StateID.Idle
        ));

        // 触发器：恢复生命
        _fsm.AddTriggerTransition(Trigger.HealthRestored, new Transition<StateID, Trigger>(
            from: StateID.Dead,
            to: StateID.Idle,
            forceInstantly: true
        ));

        // ==================== 任意状态转换 ====================

        // 任意状态 -> Dead (死亡事件)
        _fsm.AddTransitionFromAny(new Transition<StateID, Trigger>(
            from: StateID.Dead, // 任意状态转换的 from 可以是任意值
            to: StateID.Dead,
            condition: t => _health <= 0,
            forceInstantly: true
        ));

        // 任意状态 -> Moving (警报状态)
        _fsm.AddTransitionFromAny(new Transition<StateID, Trigger>(
            from: StateID.Dead,
            to: StateID.Moving,
            condition: t => _isAlert && _health > 0,
            forceInstantly: true
        ));

        // ==================== 双向转换 ====================

        // Idle <-> Defending 双向转换
        _fsm.AddTwoWayTransition(new Transition<StateID, Trigger>(
            from: StateID.Idle,
            to: StateID.Defending,
            condition: t => _health < 50f
        ));

        // ==================== 延迟转换 ====================

        // 使用 TransitionAfter 延迟转换
        _fsm.AddTransition(new TransitionAfter<StateID, Trigger>(
            from: StateID.Attacking,
            to: StateID.Idle,
            seconds: 3f
        ));

        // ==================== Exit 转换 ====================

        // 从任意状态退出到 Dead
        _fsm.AddExitTransition(new Transition<StateID, Trigger>(
            from: StateID.Idle,
            to: StateID.Dead,
            condition: t => _health <= 0
        ));

        _fsm.SetStartState(StateID.Idle);
        _fsm.Init();
    }

    private void Update()
    {
        _fsm.OnLogic();

        // 模拟数据更新
        SimulateData();

        // 测试触发器
        TestTriggers();
    }

    private void SimulateData()
    {
        // 模拟敌人距离变化
        _distanceToEnemy = 10f + Mathf.Sin(Time.time) * 5f;

        // 模拟体力消耗
        if (Input.GetKey(KeyCode.LeftShift))
        {
            _stamina -= 5f * Time.deltaTime;
        }
    }

    private void TestTriggers()
    {
        // 按 M 键开始移动
        if (Input.GetKeyDown(KeyCode.M))
        {
            _fsm.Trigger(Trigger.StartMove);
            Debug.Log("[Test] 触发 StartMove");
        }

        // 按 N 键停止移动
        if (Input.GetKeyDown(KeyCode.N))
        {
            _fsm.Trigger(Trigger.StopMove);
            Debug.Log("[Test] 触发 StopMove");
        }

        // 按 J 键开始攻击
        if (Input.GetKeyDown(KeyCode.J))
        {
            _fsm.Trigger(Trigger.StartAttack);
            Debug.Log("[Test] 触发 StartAttack");
        }

        // 按 K 键停止攻击
        if (Input.GetKeyDown(KeyCode.K))
        {
            _fsm.Trigger(Trigger.StopAttack);
            Debug.Log("[Test] 触发 StopAttack");
        }

        // 按 H 键恢复生命
        if (Input.GetKeyDown(KeyCode.H))
        {
            _health = 100f;
            _fsm.Trigger(Trigger.HealthRestored);
            Debug.Log("[Test] 触发 HealthRestored");
        }

        // 按 T 键受到伤害
        if (Input.GetKeyDown(KeyCode.T))
        {
            _health -= 20f;
            Debug.Log($"[Test] 受到 20 伤害，当前生命: {_health}");
        }

        // 按 L 键触发警报
        if (Input.GetKeyDown(KeyCode.L))
        {
            _isAlert = !_isAlert;
            Debug.Log($"[Test] 警报状态: {_isAlert}");
        }
    }

    public StateID GetCurrentState() => _fsm.ActiveStateName;
    public float GetHealth() => _health;
    public float GetStamina() => _stamina;
}
