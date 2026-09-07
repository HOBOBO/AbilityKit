// ============================================================================
// Graph Descriptor Interfaces - 描述器接口层
// 核心抽象层，用于解耦数据存储与运行时/导出逻辑
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Graph.Descriptor
{

    /// <summary>
    /// 行为参数描述器接口
    /// </summary>
    public interface IBehaviorParameterDescriptor
    {
        string Name { get; }
        DescriptorBehaviorParameterType ValueType { get; }

        // 值访问
        float GetFloatValue();
        int GetIntValue();
        bool GetBoolValue();
        string GetStringValue();
        object GetObjectValue();
        UnityEngine.Vector2 GetVector2Value();
        UnityEngine.Vector3 GetVector3Value();
        UnityEngine.Color GetColorValue();
    }
}
