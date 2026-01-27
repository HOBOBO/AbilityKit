using Emilia.Kit;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 本地设置自定义处理器
    /// </summary>
    [EditorHandleGenerate]
    public abstract class GraphLocalSettingHandle
    {
        /// <summary>
        /// 读取“类型”设置
        /// </summary>
        public virtual void OnReadTypeSetting(GraphLocalSettingCache setting) { }

        /// <summary>
        /// 读取“资源”设置
        /// </summary>
        public virtual void OnReadAssetSetting(GraphLocalSettingCache setting) { }
    }
}