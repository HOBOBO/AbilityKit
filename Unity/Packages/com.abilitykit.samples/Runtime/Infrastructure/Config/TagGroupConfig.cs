#nullable enable

namespace AbilityKit.Samples.Logic.Infrastructure.Config
{
    /// <summary>
    /// 鏍囩缁勯厤缃?
    /// </summary>
    public class TagGroupConfig
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string[] Tags { get; set; }
    }
}
