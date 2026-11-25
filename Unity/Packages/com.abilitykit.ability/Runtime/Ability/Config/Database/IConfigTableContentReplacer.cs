namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// 供配置表工厂的外部实现参与原位增量替换，保持已取得的表引用有效。
    /// 数据库在提交前验证目标实现此接口且源与目标的运行时类型一致。
    /// </summary>
    public interface IConfigTableContentReplacer
    {
        /// <summary>
        /// 用已完整构建的同类型源表替换当前内容，不改变当前表身份。
        /// 实现应只复制表内容，不发布事件、不修改其他表，并保证合法源的替换不抛异常。
        /// 数据库不为自定义实现的提交异常提供跨表回滚。
        /// </summary>
        void ReplaceContentsFrom(object source);
    }
}
