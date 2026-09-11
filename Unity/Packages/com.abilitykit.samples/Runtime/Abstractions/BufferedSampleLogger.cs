#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// 示例宿主和导出器共享的稳定输出协议元数据。
    /// </summary>
    public static class SampleOutputContract
    {
        /// <summary>当前结构化输出协议版本。</summary>
        public const string SchemaVersion = "sample-output.v1";
    }

    /// <summary>
    /// 结构化日志记录类型。
    /// </summary>
    public enum SampleLogKind
    {
        /// <summary>普通信息文本。</summary>
        Info,
        /// <summary>警告文本。</summary>
        Warn,
        /// <summary>错误文本。</summary>
        Error,
        /// <summary>分节标题。</summary>
        Section,
        /// <summary>空行。</summary>
        Line,
        /// <summary>可视分隔线。</summary>
        Divider,
        /// <summary>项目符号项。</summary>
        Bullet,
        /// <summary>编号项。</summary>
        Numbered,
        /// <summary>键值对。</summary>
        KeyValue
    }

    /// <summary>
    /// 由 <see cref="BufferedSampleLogger"/> 捕获的结构化日志项。
    /// </summary>
    public readonly struct SampleLogEntry
    {
        /// <summary>
        /// 创建结构化日志项。
        /// </summary>
        public SampleLogEntry(SampleLogKind kind, string text, string? key = null, int? number = null, int sequence = 0)
        {
            Kind = kind;
            Text = text ?? string.Empty;
            Key = key ?? string.Empty;
            Number = number;
            Sequence = sequence;
        }

        /// <summary>记录类型。</summary>
        public SampleLogKind Kind { get; }
        /// <summary>主体文本。</summary>
        public string Text { get; }
        /// <summary>键值记录的可选键。</summary>
        public string Key { get; }
        /// <summary>编号记录的可选序号。</summary>
        public int? Number { get; }
        /// <summary>单次示例运行内从零开始的稳定顺序。</summary>
        public int Sequence { get; }
    }

    /// <summary>
    /// 为 UI 宿主存储结构化记录的日志器。
    /// </summary>
    public sealed class BufferedSampleLogger : ILogger
    {
        private readonly List<SampleLogEntry> _entries = new();
        private int _nextSequence;

        /// <summary>
        /// 已捕获的记录。
        /// </summary>
        public IReadOnlyList<SampleLogEntry> Entries => _entries;

        /// <inheritdoc />
        public void Info(string message) => Add(SampleLogKind.Info, message);
        /// <inheritdoc />
        public void Warn(string message) => Add(SampleLogKind.Warn, message);
        /// <inheritdoc />
        public void Error(string message) => Add(SampleLogKind.Error, message);
        /// <inheritdoc />
        public void Section(string title) => Add(SampleLogKind.Section, title);
        /// <inheritdoc />
        public void Line() => Add(SampleLogKind.Line, string.Empty);
        /// <inheritdoc />
        public void Divider() => Add(SampleLogKind.Divider, string.Empty);
        /// <inheritdoc />
        public void Bullet(string text) => Add(SampleLogKind.Bullet, text);
        /// <inheritdoc />
        public void Numbered(int num, string text) => Add(SampleLogKind.Numbered, text, number: num);
        /// <inheritdoc />
        public void KeyValue(string key, string value) => Add(SampleLogKind.KeyValue, value, key);
        /// <inheritdoc />
        public void Flush() { }

        /// <summary>
        /// 清理所有已捕获记录。
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
            _nextSequence = 0;
        }

        private void Add(SampleLogKind kind, string text, string? key = null, int? number = null)
        {
            _entries.Add(new SampleLogEntry(kind, text, key, number, _nextSequence++));
        }
    }
}
