#nullable enable

namespace AbilityKit.Samples.Logic.Infrastructure.Config
{
    /// <summary>
    /// 配置文件路径常量
    /// </summary>
    public static class ConfigPaths
    {
        /// <summary>
        /// 管线配置文件
        /// </summary>
        public const string PipelineConfig = "Configs/PipelineConfig.json";

        /// <summary>
        /// 示例配置
        /// </summary>
        public const string SampleConfigs = "Configs/SampleConfigs.json";

        /// <summary>
        /// 标签配置文件
        /// </summary>
        public const string TagsConfig = "Configs/TagsConfig.json";
    }

    /// <summary>
    /// JSON 閰嶇疆鑺傚悕绉板父閲?    /// </summary>
    public static class ConfigSections
    {
        /// <summary>
        /// 绠＄嚎閰嶇疆鑺?        /// </summary>
        public const string PipelineConfigs = "pipelineConfigs";

        /// <summary>
        /// 瑙掕壊鏍囩閰嶇疆鑺?        /// </summary>
        public const string CharacterTags = "characterTags";

        /// <summary>
        /// 标签组配置节
        /// </summary>
        public const string TagGroups = "tagGroups";
    }
}
