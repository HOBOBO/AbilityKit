using System;
using System.Collections.Generic;
using AbilityKit.Attributes.Formula;
using AbilityKit.Attributes.Constraint;

namespace AbilityKit.Attributes.Core
{
    /// <summary>
    /// 属性定义。
    /// 定义属性的元数据，包括名称、组、默认值、公式、约束和依赖关系。
    /// 
    /// 设计说明：
    /// - AttributeId 现在持有名称
    /// - 支持通过 IAttributeDependencyProvider 接口声明依赖
    /// </summary>
    public sealed class AttributeDef
    {
        /// <summary>属性 ID（持有名称）</summary>
        public AttributeId Id;

        /// <summary>属性名称</summary>
        public string Name;

        /// <summary>所属组</summary>
        public string Group;

        /// <summary>默认基础值</summary>
        public float DefaultBaseValue;

        /// <summary>计算公式</summary>
        public IAttributeFormula Formula;

        /// <summary>值约束</summary>
        public IAttributeConstraint Constraint;

        /// <summary>直接依赖的其他属性</summary>
        public AttributeId[] DependsOn;

        public AttributeDef(string name, string group = null, float defaultBaseValue = 0f, IAttributeFormula formula = null, IAttributeConstraint constraint = null, params AttributeId[] dependsOn)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Group = group;
            DefaultBaseValue = defaultBaseValue;
            Formula = formula;
            Constraint = constraint;
            DependsOn = dependsOn;
        }

        /// <summary>
        /// 获取该属性依赖的其他属性
        /// </summary>
        public IEnumerable<AttributeId> GetDependencies()
        {
            if (DependsOn != null && DependsOn.Length > 0)
            {
                foreach (var dep in DependsOn)
                {
                    yield return dep;
                }
            }

            if (Formula is IAttributeDependencyProvider provider)
            {
                foreach (var dep in provider.GetDependencies())
                {
                    yield return dep;
                }
            }
        }
    }
}
