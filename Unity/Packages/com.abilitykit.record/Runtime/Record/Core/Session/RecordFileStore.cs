using System.IO;

namespace AbilityKit.Core.Recording.Core
{
    /// <summary>
    /// 以二进制方式读写记录容器，路径基目录由宿主环境提供。
    /// </summary>
    public static class RecordFileStore
    {
        /// <summary>
        /// 写入容器字节并创建缺失的父目录；覆盖同名文件。
        /// </summary>
        public static void Save(string path, byte[] data)
        {
            var resolved = ResolvePath(path);
            var dir = Path.GetDirectoryName(resolved);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(resolved, data);
        }

        /// <summary>
        /// 读回完整容器字节；文件读取失败时向调用方传播异常。
        /// </summary>
        public static byte[] Load(string path)
        {
            var resolved = ResolvePath(path);
            return File.ReadAllBytes(resolved);
        }

        /// <summary>
        /// 将相对路径拼接到宿主基目录，原样返回已带根目录的路径和空值。
        /// </summary>
        public static string ResolvePath(string path) => RecordPathResolver.Resolve(path);
    }
}
