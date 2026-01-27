using System;
using UnityEngine;

namespace Emilia.Node.Attributes
{
    /// <summary>
    /// Graph设置属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class GraphSettingAttribute : Attribute
    {
        public GraphSettingStruct settingStruct = GraphSettingStruct.Default;

        /// <summary>
        /// 最大加载时间
        /// </summary>
        public float maxLoadTimeMs
        {
            get => settingStruct.maxLoadTimeMs;
            set => settingStruct.maxLoadTimeMs = value;
        }

        /// <summary>
        /// 是否启用快速撤销
        /// </summary>
        public bool fastUndo
        {
            get => settingStruct.fastUndo;
            set => settingStruct.fastUndo = value;
        }

        /// <summary>
        /// 实时保存
        /// </summary>
        public bool immediatelySave
        {
            get => settingStruct.immediatelySave;
            set => settingStruct.immediatelySave = value;
        }

        /// <summary>
        /// Zoom的最小和最大值
        /// </summary>
        public Vector2 zoomSize
        {
            get => settingStruct.zoomSize;
            set => settingStruct.zoomSize = value;
        }

        /// <summary>
        /// 禁用边绘制优化
        /// </summary>
        public bool disabledEdgeDrawOptimization
        {
            get => settingStruct.disabledEdgeDrawOptimization;
            set => settingStruct.disabledEdgeDrawOptimization = value;
        }
    }

    /// <summary>
    /// Graph设置
    /// </summary>
    [Serializable]
    public struct GraphSettingStruct
    {
        public float maxLoadTimeMs;
        public bool fastUndo;
        public bool immediatelySave;
        public Vector2 zoomSize;
        public bool disabledEdgeDrawOptimization;

        public static readonly GraphSettingStruct Default = new() {
            maxLoadTimeMs = 0.0416f,
            fastUndo = true,
            immediatelySave = true,
            zoomSize = new Vector2(0.15f, 3f),
            disabledEdgeDrawOptimization = false
        };
    }
}