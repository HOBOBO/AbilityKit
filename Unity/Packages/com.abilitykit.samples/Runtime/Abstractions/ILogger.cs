#nullable enable

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// 日志器接口
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// 信息日志
        /// </summary>
        void Info(string message);

        /// <summary>
        /// 警告日志
        /// </summary>
        void Warn(string message);

        /// <summary>
        /// 错误日志
        /// </summary>
        void Error(string message);

        /// <summary>
        /// 分节标题
        /// </summary>
        void Section(string title);

        /// <summary>
        /// 空行
        /// </summary>
        void Line();

        /// <summary>
        /// 分隔线
        /// </summary>
        void Divider();

        /// <summary>
        /// 项目符号
        /// </summary>
        void Bullet(string text);

        /// <summary>
        /// 编号项
        /// </summary>
        void Numbered(int num, string text);

        /// <summary>
        /// 键值对
        /// </summary>
        void KeyValue(string key, string value);

        /// <summary>
        /// 刷新输出
        /// </summary>
        void Flush();
    }
}
