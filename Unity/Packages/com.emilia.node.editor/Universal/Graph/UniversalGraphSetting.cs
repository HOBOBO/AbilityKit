using System;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// UniversalGraph设置属性
    /// </summary>
    public class UniversalGraphSettingAttribute : Attribute
    {
        public UniversalGraphSetting settingStruct = new() {
            forceUseBuiltInInspector = false,
            disabledTransmitNode = false,
            disabledNodeInsert = false,
        };

        /// <summary>
        /// 强制使用内置Inspector
        /// </summary>
        public bool forceUseBuiltInInspector
        {
            get => settingStruct.forceUseBuiltInInspector;
            set => settingStruct.forceUseBuiltInInspector = value;
        }

        /// <summary>
        /// 禁用传输节点
        /// </summary>
        public bool disabledTransmitNode
        {
            get => settingStruct.disabledTransmitNode;
            set => settingStruct.disabledTransmitNode = value;
        }

        /// <summary>
        /// 禁用节点插入
        /// </summary>
        public bool disabledNodeInsert
        {
            get => settingStruct.disabledNodeInsert;
            set => settingStruct.disabledNodeInsert = value;
        }
    }

    /// <summary>
    /// 通用Graph设置
    /// </summary>
    [Serializable]
    public struct UniversalGraphSetting
    {
        /// <summary>
        /// 强制使用内置Inspector
        /// </summary>
        public bool forceUseBuiltInInspector;

        /// <summary>
        /// 禁用传送节点
        /// </summary>
        public bool disabledTransmitNode;

        /// <summary>
        /// 禁用节点插入
        /// </summary>
        public bool disabledNodeInsert;
    }
}