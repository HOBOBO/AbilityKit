// ============================================================================
// HFSM Action 示例 - 展示带行为树执行的状态机
// ============================================================================

using UnityEngine;
using UnityHFSM;
using UnityHFSM.Actions;
using UnityHFSM.Actions.Runtime;

/// <summary>
/// 行为树执行示例 - 使用 ActionStateMachine 和行为树节点
/// </summary>
public class ActionStateMachineExample : MonoBehaviour
{
    // 事件定义
    private enum Event
    {
        Complete,
        Fail,
        CanAttack,
        TargetLost
    }

    private ActionStateMachine<string, Event> _fsm;
    private Transform _target;
    private float _health = 100f;
    private float _attackRange = 2f;
    private float _chaseRange = 10f;
    private float _timeInState;

    private void Start()
    {
        BuildStateMachine();
    }

    private void BuildStateMachine()
    {
        _fsm = new ActionStateMachine<string, Event>();

        // ==================== 根状态机 ====================

        // ---- Wander 状态 ----
        var wanderState = new State<string, Event>(
            onEnter: state => Debug.Log("[Wander] 开始漫游"),
            onLogic: state =>
            {
                // 随机移动
                if (Random.Range(0f, 1f) < 0.01f)
                {
                    Vector3 randomDir = Random.insideUnitSphere * 5f;
                    randomDir.y = 0;
                    transform.position += randomDir;
                }
            }
        );
        _fsm.AddState("Wander", wanderState);

        // ---- Chase 状态 ----
        var chaseState = new State<string, Event>(
            onEnter: state => Debug.Log("[Chase] 开始追击"),
            onLogic: state =>
            {
                if (_target != null)
                {
                    float distance = Vector3.Distance(transform.position, _target.position);

                    if (distance < _attackRange)
                    {
                        Debug.Log("[Chase] 进入攻击范围");
                        _fsm.Trigger(Event.CanAttack);
                    }
                    else if (distance > _chaseRange)
                    {
                        Debug.Log("[Chase] 目标逃离追击范围");
                        _fsm.Trigger(Event.TargetLost);
                    }
                    else
                    {
                        // 接近目标
                        Vector3 dir = (_target.position - transform.position).normalized;
                        transform.position += dir * 3f * Time.deltaTime;
                    }
                }
            }
        );
        _fsm.AddState("Chase", chaseState);

        // ---- Attack 状态 ----
        var attackState = new State<string, Event>(
            onEnter: state => Debug.Log("[Attack] 开始攻击"),
            onLogic: state =>
            {
                if (_target != null)
                {
                    float distance = Vector3.Distance(transform.position, _target.position);

                    if (distance > _attackRange * 1.5f)
                    {
                        Debug.Log("[Attack] 目标离开攻击范围");
                        _fsm.Trigger(Event.TargetLost);
                    }
                    else if (_health < 30f)
                    {
                        Debug.Log("[Attack] 生命值过低，撤退");
                        _fsm.Trigger(Event.Fail);
                    }
                    else
                    {
                        // 模拟攻击
                        Debug.Log("[Attack] 执行攻击！");
                    }
                }
            }
        );
        _fsm.AddState("Attack", attackState);

        // ---- Retreat 状态 ----
        var retreatState = new State<string, Event>(
            onEnter: state => Debug.Log("[Retreat] 开始撤退"),
            onLogic: state =>
            {
                _timeInState += Time.deltaTime;

                // 向安全方向移动
                Vector3 safeDir = (transform.position - (_target?.position ?? transform.position)).normalized;
                transform.position += safeDir * 2f * Time.deltaTime;

                if (_timeInState > 3f)
                {
                    Debug.Log("[Retreat] 撤退完成，恢复巡逻");
                    _fsm.Trigger(Event.Complete);
                }
            }
        );
        _fsm.AddState("Retreat", retreatState);

        // ==================== 转换 ====================

        // Wander -> Chase
        _fsm.AddTransition(new Transition<string, Event>("Wander", "Chase",
            condition: t => _target != null));

        // Chase -> Attack
        _fsm.AddTransition(new Transition<string, Event>("Chase", "Attack",
            condition: t => _target != null && Vector3.Distance(transform.position, _target.position) < _attackRange));

        // Chase -> Wander
        _fsm.AddTransition(new Transition<string, Event>("Chase", "Wander",
            condition: t => _target == null));

        // Attack -> Chase
        _fsm.AddTransition(new Transition<string, Event>("Attack", "Chase",
            condition: t => Vector3.Distance(transform.position, _target.position) > _attackRange * 1.5f));

        // Attack -> Retreat
        _fsm.AddTransition(new Transition<string, Event>("Attack", "Retreat",
            condition: t => _health < 30f));

        // Retreat -> Wander
        _fsm.AddTransition(new Transition<string, Event>("Retreat", "Wander",
            condition: t => _timeInState > 3f));

        // ==================== 触发器转换 ====================

        // 被攻击时中断并进入 Chase
        _fsm.AddTriggerTransition(Event.Fail, new Transition<string, Event>("", "Chase",
            forceInstantly: true));

        _fsm.SetStartState("Wander");
        _fsm.Init();

        // 订阅行为完成事件
        _fsm.OnBehaviorCompleted += (behaviorId, stateId, status) =>
        {
            Debug.Log($"[ActionFSM] 行为完成 - State: {stateId}, Behavior: {behaviorId}, Status: {status}");
        };

        _fsm.OnBehaviorFailed += (behaviorId, stateId) =>
        {
            Debug.Log($"[ActionFSM] 行为失败 - State: {stateId}, Behavior: {behaviorId}");
        };
    }

    private void Update()
    {
        _fsm.OnLogic();
    }

    public void SetTarget(Transform target)
    {
        _target = target;
    }

    public void TakeDamage(float damage)
    {
        _health -= damage;
        Debug.Log($"[ActionFSM] 受到 {damage} 伤害，剩余 {_health:F1}");
    }

    public string GetCurrentState() => _fsm?.ActiveStateName ?? "None";
}
