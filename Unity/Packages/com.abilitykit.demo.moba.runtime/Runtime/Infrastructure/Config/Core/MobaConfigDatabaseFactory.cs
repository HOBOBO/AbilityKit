using AbilityKit.Ability.Config;

namespace AbilityKit.Demo.Moba.Config.Core
{
    /// <summary>
    /// 构造并加载 MOBA 表数据库；资源来源和加载策略由调用方提供。
    /// </summary>
    public static class MobaConfigDatabaseFactory
    {
        /// <summary>
        /// 通过指定配置加载策略构建数据库；未提供管线时直接调用策略的独立加载入口。
        /// 加载失败时不返回部分构建的数据库，异常由调用方处理。
        /// </summary>
        public static MobaConfigDatabase Create(
            ITextAssetLoader assets,
            IMobaConfigTableRegistry registry,
            IMobaConfigDtoDeserializer deserializer,
            IMobaConfigDtoBytesDeserializer bytesDeserializer,
            IMobaConfigLoadProfile profile,
            IMobaConfigLoadPipeline pipeline = null)
        {
            var database = new MobaConfigDatabase(registry, deserializer, bytesDeserializer, assets);
            if (pipeline == null)
                profile.Load(database);
            else
                profile.Load(database, pipeline);
            return database;
        }
    }
}
