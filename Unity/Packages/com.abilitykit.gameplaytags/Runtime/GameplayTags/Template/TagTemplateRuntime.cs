namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 标签模板运行时数据。
    /// </summary>
    public sealed class TagTemplateRuntime
    {
        /// <summary>
        /// 模板 ID
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// 模板名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 标签需求条件
        /// </summary>
        public GameplayTagRequirements Requirements { get; }

        /// <summary>
        /// 授予的标签容器
        /// </summary>
        public GameplayTagContainer GrantTags { get; }

        /// <summary>
        /// 移除的标签容器
        /// </summary>
        public GameplayTagContainer RemoveTags { get; }

        public TagTemplateRuntime(int id, string name, GameplayTagRequirements requirements, GameplayTagContainer grantTags, GameplayTagContainer removeTags)
        {
            Id = id;
            Name = name;
            Requirements = requirements;
            GrantTags = grantTags ?? new GameplayTagContainer();
            RemoveTags = removeTags ?? new GameplayTagContainer();
        }

        /// <summary>
        /// 从 GameplayTagTemplate 创建运行时数据
        /// </summary>
        public static TagTemplateRuntime FromTemplate(int id, string name, GameplayTagTemplate template)
        {
            return new TagTemplateRuntime(
                id,
                name,
                template.Requirements,
                template.GetGrantContainer(),
                template.GetRemoveContainer()
            );
        }
    }
}
