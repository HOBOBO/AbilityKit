using AbilityKit.Attributes.Core;

namespace AbilityKit.Attributes
{
    /// <summary>
    /// 属性注册表静态门面。
    /// 提供便捷的静态方法访问默认注册表。
    /// 
    /// 设计说明：
    /// - 内部使用 AttributeRegistry.DefaultRegistry
    /// - 保持向后兼容
    /// - 业务层应考虑使用 IAttributeRegistry 接口进行依赖注入
    /// </summary>
    public static class AttrRegistry
    {
        /// <summary>
        /// 获取属性 ID
        /// </summary>
        public static AttributeId Attr(string name)
        {
            return AttributeRegistry.DefaultRegistry.Request(name);
        }

        /// <summary>
        /// 尝试获取属性 ID
        /// </summary>
        public static bool TryAttr(string name, out AttributeId id)
        {
            id = AttributeRegistry.DefaultRegistry.TryGet(name, out id) ? id : default;
            return id.IsValid;
        }

        /// <summary>
        /// 冻结注册表
        /// </summary>
        public static void Freeze()
        {
            AttributeRegistry.DefaultRegistry.Freeze();
        }

        /// <summary>
        /// 注册属性定义
        /// </summary>
        public static AttributeId Register(AttributeDef def)
        {
            return AttributeRegistry.DefaultRegistry.Register(def);
        }

        /// <summary>
        /// 获取默认注册表实例
        /// </summary>
        public static AttributeRegistry Instance => AttributeRegistry.DefaultRegistry;
    }
}
