// ============================================================================
// AutoLayoutEngine - 自动布局引擎
// 基于状态机层级结构自动计算节点位置
// ============================================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityHFSM.Visualization;

namespace UnityHFSM.Editor.RuntimeMonitor
{
    /// <summary>
    /// 自动布局引擎
    /// 采用层级布局算法，根据状态机的嵌套结构自动排列节点
    /// </summary>
    public class AutoLayoutEngine
    {
        private readonly float _nodeWidth;
        private readonly float _nodeHeight;
        private readonly float _spacingX;
        private readonly float _spacingY;
        private readonly float _marginLeft;
        private readonly float _marginTop;

        public AutoLayoutEngine(
            float nodeWidth = 140f,
            float nodeHeight = 50f,
            float spacingX = 40f,
            float spacingY = 30f,
            float marginLeft = 50f,
            float marginTop = 30f)
        {
            _nodeWidth = nodeWidth;
            _nodeHeight = nodeHeight;
            _spacingX = spacingX;
            _spacingY = spacingY;
            _marginLeft = marginLeft;
            _marginTop = marginTop;
        }

        /// <summary>
        /// 计算布局
        /// </summary>
        public void CalculateLayout(FsmSnapshot snapshot, float canvasWidth, float canvasHeight)
        {
            if (snapshot == null || snapshot.states.Count == 0)
                return;

            // 1. 按层级分组
            var levels = GroupByLevel(snapshot);

            // 2. 计算每层的宽度和起始位置
            var levelData = new List<LevelData>();

            foreach (var kvp in levels)
            {
                var level = kvp.Key;
                var states = kvp.Value;

                // 计算该层所有节点的宽度
                float totalWidth = 0;
                for (int i = 0; i < states.Count; i++)
                {
                    totalWidth += _nodeWidth;
                    if (i < states.Count - 1)
                        totalWidth += _spacingX;
                }

                // 计算居中位置
                float startX = (canvasWidth - totalWidth) / 2;
                if (startX < _marginLeft)
                    startX = _marginLeft;

                levelData.Add(new LevelData
                {
                    Level = level,
                    States = states,
                    StartX = startX,
                    TotalWidth = totalWidth
                });
            }

            // 3. 分配位置
            foreach (var level in levelData)
            {
                float currentX = level.StartX;
                float y = _marginTop + level.Level * (_nodeHeight + _spacingY);

                foreach (var state in level.States)
                {
                    // 更新状态节点位置
                    for (int i = 0; i < snapshot.states.Count; i++)
                    {
                        if (snapshot.states[i].path == state.path)
                        {
                            var info = snapshot.states[i];
                            info.x = currentX;
                            info.y = y;
                            info.width = _nodeWidth;
                            info.height = _nodeHeight;
                            snapshot.states[i] = info;
                            break;
                        }
                    }

                    currentX += _nodeWidth + _spacingX;
                }
            }

            // 4. 调整连线位置
            UpdateTransitionPositions(snapshot);
        }

        /// <summary>
        /// 按层级分组
        /// </summary>
        private Dictionary<int, List<StateNodeInfo>> GroupByLevel(FsmSnapshot snapshot)
        {
            var levels = new Dictionary<int, List<StateNodeInfo>>();

            foreach (var state in snapshot.states)
            {
                int level = state.nestingLevel;

                if (!levels.ContainsKey(level))
                {
                    levels[level] = new List<StateNodeInfo>();
                }

                levels[level].Add(state);
            }

            return levels;
        }

        /// <summary>
        /// 更新连线位置
        /// </summary>
        private void UpdateTransitionPositions(FsmSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.transitions.Count; i++)
            {
                var transition = snapshot.transitions[i];
                var fromState = snapshot.FindState(transition.fromPath);
                var toState = snapshot.FindState(transition.toPath);

                if (fromState.HasValue && toState.HasValue)
                {
                    // 计算连线位置
                    transition.fromX = fromState.Value.x + _nodeWidth / 2;
                    transition.fromY = fromState.Value.y + _nodeHeight;
                    transition.toX = toState.Value.x + _nodeWidth / 2;
                    transition.toY = toState.Value.y;
                }
                snapshot.transitions[i] = transition;
            }
        }

        /// <summary>
        /// 层级数据
        /// </summary>
        private class LevelData
        {
            public int Level;
            public List<StateNodeInfo> States;
            public float StartX;
            public float TotalWidth;
        }
    }

    /// <summary>
    /// 贝塞尔曲线工具类
    /// </summary>
    public static class BezierUtility
    {
        /// <summary>
        /// 计算贝塞尔曲线上的点
        /// </summary>
        public static Vector3 BezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector3 p = uuu * p0;
            p += 3 * uu * t * p1;
            p += 3 * u * tt * p2;
            p += ttt * p3;

            return p;
        }
    }
}

#endif
