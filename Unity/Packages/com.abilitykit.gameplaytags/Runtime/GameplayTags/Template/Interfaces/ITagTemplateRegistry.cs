namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 标签模板注册表接口。
    /// </summary>
    public interface ITagTemplateRegistry
    {
        /// <summary>
        /// 尝试通过 ID 获取模板
        /// </summary>
        bool TryGet(int templateId, out TagTemplateRuntime template);

        /// <summary>
        /// 尝试通过名称获取模板
        /// </summary>
        bool TryGet(string name, out TagTemplateRuntime template);
    }
}
