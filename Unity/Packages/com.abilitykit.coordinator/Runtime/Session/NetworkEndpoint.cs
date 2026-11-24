using System;

namespace AbilityKit.Coordinator
{
    /// <summary>
    /// 远程连接使用的网络端点。
    /// </summary>
    public readonly struct NetworkEndpoint : IEquatable<NetworkEndpoint>
    {
        /// <summary>
        /// 获取配置中的主机文本；默认端点未配置。
        /// </summary>
        public string Host { get; }
        /// <summary>
        /// 获取配置中的端口；默认端点为零。
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// 保存主机文本与端口，不执行解析、规范化或拨号。
        /// </summary>
        public NetworkEndpoint(string host, int port)
        {
            Host = host;
            Port = port;
        }

        /// <summary>
        /// 判断是否填写了主机且端口为正；不代表传输可达或通过具体协议校验。
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(Host) && Port > 0;

        /// <summary>
        /// 返回用于显示的主机和端口，不保证是可解析的网络地址。
        /// </summary>
        public override string ToString() => $"{Host}:{Port}";

        /// <summary>
        /// 按原始主机字符串和端口比较。
        /// </summary>
        public bool Equals(NetworkEndpoint other) => Host == other.Host && Port == other.Port;
        /// <summary>
        /// 判断对象是否是值相等的端点。
        /// </summary>
        public override bool Equals(object obj) => obj is NetworkEndpoint other && Equals(other);
        /// <summary>
        /// 返回与端点值比较一致的散列。
        /// </summary>
        public override int GetHashCode() => HashCode.Combine(Host, Port);

        /// <summary>
        /// 判断两个端点是否相等。
        /// </summary>
        public static bool operator ==(NetworkEndpoint left, NetworkEndpoint right) => left.Equals(right);
        /// <summary>
        /// 判断两个端点是否不同。
        /// </summary>
        public static bool operator !=(NetworkEndpoint left, NetworkEndpoint right) => !left.Equals(right);

        /// <summary>
        /// 获取未配置的默认端点。
        /// </summary>
        public static NetworkEndpoint None => default;
    }
}
