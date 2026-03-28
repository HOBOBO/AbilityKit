// ============================================================================
// HFSM 基础示例 - 最简单的状态机用法
// ============================================================================

using UnityEngine;
using UnityHFSM;

/// <summary>
/// 基础状态机示例 - 展示状态机的核心用法
/// </summary>
public class BasicStateMachineExample : MonoBehaviour
{
    // 状态ID使用字符串
    private StateMachine _fsm;

    private void Start()
    {
        BuildSimpleFSM();
    }

    private void BuildSimpleFSM()
    {
        _fsm = new StateMachine();

        // 添加状态：使用 lambda 表达式定义行为
        _fsm.AddState("Idle", new State(
            onEnter: state => Debug.Log("[Idle] 进入空闲状态"),
            onLogic: state => Debug.Log("[Idle] 执行空闲逻辑..."),
            onExit: state => Debug.Log("[Idle] 退出空闲状态")
        ));

        _fsm.AddState("Walk", new State(
            onEnter: state => Debug.Log("[Walk] 开始行走"),
            onLogic: state =>
            {
                transform.position += Vector3.forward * Time.deltaTime;
            },
            onExit: state => Debug.Log("[Walk] 停止行走")
        ));

        _fsm.AddState("Run", new State(
            onEnter: state => Debug.Log("[Run] 开始奔跑"),
            onLogic: state =>
            {
                transform.position += Vector3.forward * Time.deltaTime * 2f;
            },
            onExit: state => Debug.Log("[Run] 停止奔跑")
        ));

        _fsm.AddState("Jump", new State(
            onEnter: state => Debug.Log("[Jump] 跳跃！"),
            needsExitTime: true, // 等待跳跃完成后再退出
            canExit: state => Time.time - state.timer.ElapsedTime > 0.5f
        ));

        // 添加转换：条件转换
        _fsm.AddTransition(new Transition("Idle", "Walk", t => Input.GetKey(KeyCode.W)));
        _fsm.AddTransition(new Transition("Walk", "Idle", t => !Input.GetKey(KeyCode.W)));
        _fsm.AddTransition(new Transition("Walk", "Run", t => Input.GetKey(KeyCode.LeftShift)));
        _fsm.AddTransition(new Transition("Run", "Walk", t => !Input.GetKey(KeyCode.LeftShift)));

        // 带退出时间的转换
        _fsm.AddTransition(new Transition("Idle", "Jump", t => Input.GetKeyDown(KeyCode.Space)));
        _fsm.AddTransition(new Transition("Walk", "Jump", t => Input.GetKeyDown(KeyCode.Space)));
        _fsm.AddTransition(new Transition("Run", "Jump", t => Input.GetKeyDown(KeyCode.Space)));

        // 跳跃完成后返回 Idle
        _fsm.AddTransition(new Transition("Jump", "Idle",
            t => Time.time - (_fsm.GetState("Jump") as State)?.timer.ElapsedTime > 0.5f));

        // 设置初始状态
        _fsm.SetStartState("Idle");
        _fsm.Init();
    }

    private void Update()
    {
        _fsm.OnLogic();
    }
}
