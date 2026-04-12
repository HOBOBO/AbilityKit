using System.Collections.Generic;
using AbilityKit.Attributes.Constraint;
using AbilityKit.Attributes.Formula;

namespace AbilityKit.Attributes.Core
{
    /// <summary>
    /// 属性注册表接口。
    /// 定义属性系统的核心注册和管理功能。
    /// 
    /// 设计目标：
    /// - 解耦静态单例，允许业务层实现自定义注册表
    /// - 支持依赖注入，提高可测试性
    /// - 提供统一的属性元数据查询接口
    /// </summary>
    public interface IAttributeRegistry
    {
        /// <summary>注册表是否已冻结</summary>
        bool IsFrozen { get; }

        /// <summary>
        /// 注册属性定义
        /// </summary>
        /// <param name="def">属性定义</param>
        /// <returns>属性 ID</returns>
        AttributeId Register(AttributeDef def);

        /// <summary>
        /// 请求属性 ID（如果已注册则返回，否则创建新定义）
        /// </summary>
        /// <param name="name">属性名称</param>
        /// <returns>属性 ID</returns>
        AttributeId Request(string name);

        /// <summary>
        /// 尝试请求属性 ID
        /// </summary>
        AttributeId Request(string name, out bool created);

        /// <summary>
        /// 冻结注册表，冻结后不能注册新属性
        /// </summary>
        void Freeze();

        /// <summary>
        /// 获取属性名称
        /// </summary>
        string GetName(AttributeId id);

        /// <summary>
        /// 获取属性定义
        /// </summary>
        AttributeDef GetDef(AttributeId id);

        /// <summary>
        /// 获取属性组名
        /// </summary>
        string GetGroup(AttributeId id);

        /// <summary>
        /// 获取默认基础值
        /// </summary>
        float GetDefaultBaseValue(AttributeId id);

        /// <summary>
        /// 获取属性公式
        /// </summary>
        IAttributeFormula GetFormula(AttributeId id);

        /// <summary>
        /// 获取属性约束
        /// </summary>
        IAttributeConstraint GetConstraint(AttributeId id);

        /// <summary>
        /// 获取依赖该属性的属性列表
        /// </summary>
        IReadOnlyList<AttributeId> GetDependents(AttributeId id);

        /// <summary>
        /// 获取该属性依赖的其他属性
        /// </summary>
        IEnumerable<AttributeId> GetDependencies(AttributeId id);
    }
}
