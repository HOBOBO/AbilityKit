// ============================================================================
// Core Visualization Interfaces - 运行时可视化核心接口
// 与具体实现无关的抽象定义
// ============================================================================

using System;
using System.Collections.Generic;

namespace UnityHFSM.Visualization
{
    /// <summary>
    /// 状态节点信息
    /// </summary>
    public struct StateNodeInfo
    {
        /// <summary>
        /// 状态名称
        /// </summary>
        public string name;

        /// <summary>
        /// 完整路径
        /// </summary>
        public string path;

        /// <summary>
        /// 父状态路径（空表示根级别）
        /// </summary>
        public string parentPath;

        /// <summary>
        /// 是否为状态机（包含子状态）
        /// </summary>
        public bool isStateMachine;

        /// <summary>
        /// 是否激活
        /// </summary>
        public bool isActive;

        /// <summary>
        /// 是否正在进入
        /// </summary>
        public bool isEntering;

        /// <summary>
        /// 是否正在退出
        /// </summary>
        public bool isExiting;

        /// <summary>
        /// 激活持续时间
        /// </summary>
        public float activeDuration;

        /// <summary>
        /// 进入次数
        /// </summary>
        public int enterCount;

        /// <summary>
        /// 嵌套层级（用于布局）
        /// </summary>
        public int nestingLevel;

        /// <summary>
        /// 计算后的位置（由布局引擎设置）
        /// </summary>
        public float x, y;

        /// <summary>
        /// 节点尺寸
        /// </summary>
        public float width, height;
    }

    /// <summary>
    /// 转换/连线信息
    /// </summary>
    public struct TransitionInfo
    {
        /// <summary>
        /// 源状态路径
        /// </summary>
        public string fromPath;

        /// <summary>
        /// 目标状态路径
        /// </summary>
        public string toPath;

        /// <summary>
        /// 条件描述
        /// </summary>
        public string conditionDescription;

        /// <summary>
        /// 是否可以发生
        /// </summary>
        public bool canTransition;

        /// <summary>
        /// 最后转换时间
        /// </summary>
        public float lastTransitionTime;

        /// <summary>
        /// 计算后的起点位置
        /// </summary>
        public float fromX, fromY;

        /// <summary>
        /// 计算后的终点位置
        /// </summary>
        public float toX, toY;
    }

    /// <summary>
    /// 参数信息
    /// </summary>
    public struct ParameterInfo
    {
        /// <summary>
        /// 参数名称
        /// </summary>
        public string name;

        /// <summary>
        /// 参数类型
        /// </summary>
        public ParameterType type;

        /// <summary>
        /// 是否为触发器
        /// </summary>
        public bool isTrigger;

        /// <summary>
        /// 当前值（根据类型使用对应字段）
        /// </summary>
        public bool boolValue;
        public int intValue;
        public float floatValue;
    }

    /// <summary>
    /// 参数类型
    /// </summary>
    public enum ParameterType
    {
        Bool,
        Int,
        Float,
        Trigger
    }

    /// <summary>
    /// 状态转换历史记录
    /// </summary>
    public struct StateTransitionRecord
    {
        /// <summary>
        /// 时间戳
        /// </summary>
        public float timestamp;

        /// <summary>
        /// 源状态路径
        /// </summary>
        public string fromPath;

        /// <summary>
        /// 目标状态路径
        /// </summary>
        public string toPath;

        /// <summary>
        /// 触发条件
        /// </summary>
        public string trigger;

        /// <summary>
        /// 相对当前时间
        /// </summary>
        public float timeAgo;
    }

    /// <summary>
    /// FSM 快照 - 完整的状态机状态信息
    /// </summary>
    public class FsmSnapshot
    {
        /// <summary>
        /// 注册的名称
        /// </summary>
        public string name;

        /// <summary>
        /// FSM 类型名称
        /// </summary>
        public string typeName;

        /// <summary>
        /// 状态节点列表
        /// </summary>
        public List<StateNodeInfo> states;

        /// <summary>
        /// 转换列表
        /// </summary>
        public List<TransitionInfo> transitions;

        /// <summary>
        /// 参数列表
        /// </summary>
        public List<ParameterInfo> parameters;

        /// <summary>
        /// 当前激活的状态路径
        /// </summary>
        public List<string> activeStatePaths;

        /// <summary>
        /// 转换历史
        /// </summary>
        public List<StateTransitionRecord> history;

        /// <summary>
        /// 快照时间
        /// </summary>
        public float snapshotTime;

        public FsmSnapshot()
        {
            states = new List<StateNodeInfo>();
            transitions = new List<TransitionInfo>();
            parameters = new List<ParameterInfo>();
            activeStatePaths = new List<string>();
            history = new List<StateTransitionRecord>();
        }

        /// <summary>
        /// 根据路径查找状态
        /// </summary>
        public StateNodeInfo? FindState(string path)
        {
            for (int i = 0; i < states.Count; i++)
            {
                if (states[i].path == path)
                    return states[i];
            }
            return null;
        }

        /// <summary>
        /// 检查状态是否激活
        /// </summary>
        public bool IsStateActive(string path)
        {
            return activeStatePaths.Contains(path);
        }

        /// <summary>
        /// 获取根级别的状态
        /// </summary>
        public IEnumerable<StateNodeInfo> GetRootStates()
        {
            for (int i = 0; i < states.Count; i++)
            {
                if (string.IsNullOrEmpty(states[i].parentPath))
                    yield return states[i];
            }
        }
    }

    /// <summary>
    /// 可视化数据提供者接口
    /// FSM 实现此接口以提供可视化数据
    /// </summary>
    public interface IVisualizationProvider
    {
        /// <summary>
        /// 获取快照
        /// </summary>
        FsmSnapshot GetSnapshot();

        /// <summary>
        /// 获取当前激活状态
        /// </summary>
        IEnumerable<string> GetActiveStatePaths();

        /// <summary>
        /// 获取参数
        /// </summary>
        IEnumerable<ParameterInfo> GetParameters();

        /// <summary>
        /// 获取状态结构树
        /// </summary>
        IEnumerable<(string name, string parentPath, bool isStateMachine)> GetStateStructure();

        /// <summary>
        /// 获取转换列表
        /// </summary>
        IEnumerable<TransitionInfo> GetTransitions();

        /// <summary>
        /// 记录转换
        /// </summary>
        void RecordTransition(string fromPath, string toPath, string trigger);

        /// <summary>
        /// 获取转换历史
        /// </summary>
        IEnumerable<StateTransitionRecord> GetHistory(int maxCount = 50);
    }

    /// <summary>
    /// 布局计算器接口
    /// </summary>
    public interface ILayoutCalculator
    {
        /// <summary>
        /// 计算布局
        /// </summary>
        void CalculateLayout(FsmSnapshot snapshot, float canvasWidth, float canvasHeight);

        /// <summary>
        /// 更新单个节点位置
        /// </summary>
        void UpdateNodePosition(FsmSnapshot snapshot, string path, float x, float y);
    }
}
