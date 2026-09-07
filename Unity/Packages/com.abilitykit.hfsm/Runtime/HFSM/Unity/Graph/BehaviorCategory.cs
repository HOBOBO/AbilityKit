// ============================================================================
// BehaviorTypeRegistry - 行为类型注册表
// 支持包外扩展行为类型，无需修改枚举
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using IAction = AbilityKit.HFSM.Actions.IAction;
using ICompositeAction = AbilityKit.HFSM.Actions.ICompositeAction;
using IDecoratorAction = AbilityKit.HFSM.Actions.IDecoratorAction;


namespace AbilityKit.HFSM
{
    /// <summary>
    /// 行为分类
    /// </summary>
    public enum BehaviorCategory
    {
        /// <summary>原子行为（叶子节点）</summary>
        Primitive,

        /// <summary>复合行为（可以有多个子节点）</summary>
        Composite,

        /// <summary>修饰器行为（只有一个子节点）</summary>
        Decorator
    }
}
