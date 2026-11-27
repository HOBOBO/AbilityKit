#nullable enable annotations

namespace AbilityKit.Core.Recording.Core
{
    /// <summary>
    /// 使用默认轨道工厂和 JSON 容器序列化器创建、加载及保存录制会话。
    /// </summary>
    public static class RecordSessionFactory
    {
        /// <summary>
        /// 按指定配置创建会话；未指定配置时使用默认轨道配置。
        /// </summary>
        public static RecordSession CreateDefault(RecordProfile? profile = null)
        {
            var effectiveProfile = profile ?? new RecordProfile();
            var container = new RecordContainerBuilder(effectiveProfile).Build();
            var serializer = new JsonRecordContainerSerializer();
            var factory = new DefaultRecordTrackFactory();
            return new RecordSession(effectiveProfile, container, serializer, factory, factory);
        }

        /// <summary>
        /// 用容器字节加载新会话；没有解析出容器时返回初始会话，解析异常向外传播。
        /// </summary>
        public static RecordSession FromBytes(byte[] data, RecordProfile? profile = null)
        {
            var session = CreateDefault(profile);
            session.TryLoad(data);
            return session;
        }

        /// <summary>
        /// 读取指定文件的容器字节并加载新会话；文件读取失败时传播异常。
        /// </summary>
        public static RecordSession LoadFromFile(string path, RecordProfile? profile = null)
        {
            var data = RecordFileStore.Load(path);
            return FromBytes(data, profile);
        }

        /// <summary>
        /// 将非空会话序列化为容器文件并覆盖同名文件；会话为空时不写入。
        /// </summary>
        public static void SaveToFile(IRecordSession? session, string path)
        {
            if (session == null) return;
            var data = session.Serialize();
            RecordFileStore.Save(path, data);
        }
    }
}
