// ============================================================================
// HFSM 运行时测试器 - 在游戏运行时测试状态机行为
// ============================================================================

using UnityEngine;
using UnityHFSM;

/// <summary>
/// 运行时测试器 - 用于在编辑器或运行时测试状态机
/// 展示如何在运行时动态修改状态机
/// </summary>
public class HfsmRuntimeTester : MonoBehaviour
{
    // 状态机类型
    public enum FsmType
    {
        Basic,
        CharacterAI,
        TransitionConditions,
        ActionStateMachine
    }

    [Header("Settings")]
    public FsmType fsmType = FsmType.Basic;
    public bool autoTest = false;
    public float autoTestInterval = 2f;

    [Header("Components")]
    public CharacterAIExample characterAI;
    public BasicStateMachineExample basicFSM;
    public TransitionConditionsExample transitionFSM;

    // 内部状态
    private float _autoTestTimer;
    private int _testStep;
    private StateMachine _basicFsm;

    private void Start()
    {
        if (autoTest)
        {
            StartAutoTest();
        }
    }

    private void Update()
    {
        if (autoTest)
        {
            _autoTestTimer += Time.deltaTime;
            if (_autoTestTimer >= autoTestInterval)
            {
                _autoTestTimer = 0f;
                RunAutoTestStep();
            }
        }

        DrawDebugInfo();
    }

    private void DrawDebugInfo()
    {
        string currentState = GetCurrentStateName();
        string fsmTypeStr = fsmType.ToString();

        GUILayout.BeginArea(new Rect(10, 10, 400, 300));
        GUILayout.BeginVertical("box");

        GUILayout.Label($"=== HFSM Runtime Tester ===", GUI.skin.box);
        GUILayout.Space(5);

        GUILayout.Label($"FSM Type: {fsmTypeStr}");
        GUILayout.Label($"Current State: {currentState}");
        GUILayout.Label($"Time: {Time.time:F1}s");

        GUILayout.Space(10);
        GUILayout.Label("--- Controls ---");
        GUILayout.Label("1-4: Switch FSM Type");
        GUILayout.Label("A: Auto Test On/Off");
        GUILayout.Label("Space: Test Trigger");

        GUILayout.Space(10);
        GUILayout.Label("--- Test Results ---");
        GUILayout.Label($"Test Step: {_testStep}");

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private string GetCurrentStateName()
    {
        switch (fsmType)
        {
            case FsmType.CharacterAI:
                return characterAI != null ? characterAI.GetCurrentState() : "No CharacterAI";
            case FsmType.Basic:
                return "See Console";
            case FsmType.TransitionConditions:
                return transitionFSM != null ? transitionFSM.GetCurrentState().ToString() : "No TransitionFSM";
            default:
                return "Unknown";
        }
    }

    private void OnGUI()
    {
        // 键盘控制
        Event e = Event.current;
        if (e.type == EventType.KeyDown)
        {
            switch (e.keyCode)
            {
                case KeyCode.Alpha1:
                    fsmType = FsmType.Basic;
                    break;
                case KeyCode.Alpha2:
                    fsmType = FsmType.CharacterAI;
                    break;
                case KeyCode.Alpha3:
                    fsmType = FsmType.TransitionConditions;
                    break;
                case KeyCode.Alpha4:
                    fsmType = FsmType.ActionStateMachine;
                    break;
                case KeyCode.A:
                    autoTest = !autoTest;
                    _testStep = 0;
                    if (autoTest) StartAutoTest();
                    Debug.Log($"[Tester] Auto Test: {autoTest}");
                    break;
            }
        }

        DrawDebugInfo();
    }

    private void StartAutoTest()
    {
        _testStep = 0;
        _autoTestTimer = 0f;
        Debug.Log("=== HFSM Auto Test Started ===");
    }

    private void RunAutoTestStep()
    {
        _testStep++;
        Debug.Log($"[Tester] Auto Test Step {_testStep}");

        switch (fsmType)
        {
            case FsmType.CharacterAI:
                RunCharacterAITest();
                break;
            case FsmType.TransitionConditions:
                RunTransitionTest();
                break;
        }
    }

    private void RunCharacterAITest()
    {
        if (characterAI == null) return;

        switch (_testStep % 6)
        {
            case 0:
                characterAI.SetTarget(null);
                Debug.Log("[Tester] 清除目标");
                break;
            case 1:
                characterAI.SetTarget(transform);
                Debug.Log("[Tester] 设置目标为自身");
                break;
            case 2:
                characterAI.TakeDamage(10f);
                break;
            case 3:
                characterAI.TakeDamage(50f);
                break;
            case 4:
                characterAI.Heal(30f);
                break;
            case 5:
                characterAI.Heal(100f);
                break;
        }
    }

    private void RunTransitionTest()
    {
        if (transitionFSM == null) return;

        switch (_testStep % 5)
        {
            case 0:
                transitionFSM.TakeDamage(20f);
                break;
            case 1:
                transitionFSM.TakeDamage(50f);
                break;
            case 2:
                transitionFSM.TakeDamage(100f);
                break;
            case 3:
                Debug.Log("[Tester] 生命值应该已满");
                break;
            case 4:
                Debug.Log("[Tester] 状态机正常运行");
                break;
        }
    }
}
