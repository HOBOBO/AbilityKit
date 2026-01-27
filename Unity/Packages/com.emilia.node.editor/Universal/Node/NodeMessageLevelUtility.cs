using UnityEditor;
using UnityEngine;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 节点消息级别工具
    /// </summary>
    public static class NodeMessageLevelUtility
    {
        /// <summary>
        /// 根据节点消息级别获取图标
        /// </summary>
        public static Texture GetIcon(NodeMessageLevel level)
        {
            switch (level)
            {
                case NodeMessageLevel.Info:
                    return EditorGUIUtility.IconContent("console.infoicon").image;
                case NodeMessageLevel.Warning:
                    return EditorGUIUtility.IconContent("console.warnicon").image;
                case NodeMessageLevel.Error:
                    return EditorGUIUtility.IconContent("console.erroricon").image;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 根据节点消息级别获取颜色
        /// </summary>
        public static Color GetColor(NodeMessageLevel level)
        {
            switch (level)
            {
                case NodeMessageLevel.Info:
                    return Color.white;
                case NodeMessageLevel.Warning:
                    return Color.yellow;
                case NodeMessageLevel.Error:
                    return Color.red;
            }

            return Color.white;
        }
    }
}