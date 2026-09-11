#nullable enable

namespace AbilityKit.Samples.Logic.Infrastructure.Config
{
    /// <summary>
    /// 鏍囩缁勯厤缃暟鎹ā鍨?
    /// </summary>
    public sealed class JsonTagGroup
    {
        /// <summary>
        /// 标签组唯一标识
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 鏍囩缁勫悕绉?
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 鏍囩缁勬弿杩?
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 标签列表
        /// </summary>
        public System.Collections.Generic.List<string> Tags { get; set; } = new();
    }
}
