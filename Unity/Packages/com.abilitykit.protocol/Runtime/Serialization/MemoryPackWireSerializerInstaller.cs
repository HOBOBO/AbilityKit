namespace AbilityKit.Protocol.Serialization
{
    /// <summary>
    /// 为调用方保留可选的 MemoryPack 反射安装入口；本包不负责加载或提供 MemoryPack。
    /// </summary>
    public static class MemoryPackWireSerializerInstaller
    {
        /// <summary>
        /// 注册反射序列化器。安装本身不验证后端是否可用，首次实际序列化或反序列化时才使用后端。
        /// </summary>
        /// <param name="replaceExisting">是否允许覆盖已经安装的二进制序列化器。</param>
        public static void InstallAsCurrent(bool replaceExisting = false)
        {
            WireSerializer.Install(new MemoryPackWireSerializer(), replaceExisting);
        }
    }
}
