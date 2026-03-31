namespace AbilityKit.Effects.Core.Model
{
    /// <summary>
    /// 效果属性项类型
    /// </summary>
    public enum EffectItemType : byte
    {
        /// <summary>属性效果</summary>
        Stat = 0,
    }

    /// <summary>
    /// EffectItemType 的扩展方法
    /// </summary>
    public static class EffectItemTypeExtensions
    {
        /// <summary>
        /// 从字符串解析类型
        /// </summary>
        public static bool TryParse(string value, out EffectItemType type)
        {
            type = default;
            if (string.IsNullOrEmpty(value)) return false;

            if (string.Equals(value, "Stat", System.StringComparison.OrdinalIgnoreCase))
            {
                type = EffectItemType.Stat;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取类型的中文名称
        /// </summary>
        public static string GetChineseName(this EffectItemType itemType) =>
            itemType switch
            {
                EffectItemType.Stat => "属性",
                _ => "未知"
            };
    }
}
