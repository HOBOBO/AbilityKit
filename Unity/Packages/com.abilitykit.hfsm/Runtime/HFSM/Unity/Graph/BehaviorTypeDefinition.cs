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
    /// 行为类型定义信息
    /// </summary>
    [Serializable]
    public class BehaviorTypeDefinition
    {
        /// <summary>类型唯一标识（用于序列化）</summary>
        public string typeName;

        /// <summary>显示名称</summary>
        public string displayName;

        /// <summary>分类</summary>
        public BehaviorCategory category;

        /// <summary>所属分类名称（编辑器中显示）</summary>
        public string categoryName;

        /// <summary>行为类类型</summary        [NonSerialized]
        public Type actionType;

        /// <summary>描述</summary>
        public string description;

        /// <summary>参数定义列表</summary>
        public List<BehaviorParameterDefinition> parameters = new List<BehaviorParameterDefinition>();
        public int minChildren;
        public int maxChildren;

        public BehaviorTypeDefinition() { }

        public BehaviorTypeDefinition(string typeName, string displayName, BehaviorCategory category, string categoryName = null, string description = null)
        {
            this.typeName = typeName;
            this.displayName = displayName;
            this.category = category;
            this.categoryName = categoryName ?? GetDefaultCategoryName(category);
            this.description = description ?? string.Empty;
            minChildren = category == BehaviorCategory.Primitive ? 0 : 1;
            maxChildren = category == BehaviorCategory.Composite ? -1 : 1;
        }

        private static string GetDefaultCategoryName(BehaviorCategory category)
        {
            return category switch
            {
                BehaviorCategory.Primitive => "基础行为",
                BehaviorCategory.Composite => "复合行为",
                BehaviorCategory.Decorator => "修饰器",
                _ => "其他"
            };
        }
    }
}
