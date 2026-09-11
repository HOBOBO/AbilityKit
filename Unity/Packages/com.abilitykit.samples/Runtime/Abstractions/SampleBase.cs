#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// 示例基类 - 提供便捷的环境访问
    /// </summary>
    public abstract class SampleBase : ISample
    {
        /// <inheritdoc />
        public abstract string Title { get; }

        /// <inheritdoc />
        public virtual string Description => string.Empty;

        /// <inheritdoc />
        public abstract SampleCategory Category { get; }

        /// <summary>
        /// 日志输出器（运行时注入）
        /// </summary>
        protected ILogger Output => _logger ?? throw new InvalidOperationException("Logger not initialized");

        /// <summary>
        /// 运行环境（运行时注入）
        /// </summary>
        protected ISampleEnvironment Environment => _environment ?? throw new InvalidOperationException("Environment not initialized");

        /// <summary>
        /// 配置提供器
        /// </summary>
        protected IConfigProvider? Config { get; private set; }

        /// <summary>
        /// 当前宿主提供的资源提供器。
        /// </summary>
        protected IResourceProvider? Resources { get; private set; }

        /// <summary>
        /// 当前运行时上下文。
        /// </summary>
        protected SampleRuntimeContext Context => _context ?? throw new InvalidOperationException("Runtime context not initialized");

        /// <summary>
        /// 所有宿主输入值。
        /// </summary>
        protected IReadOnlyDictionary<string, string> Inputs => Context.Inputs;

        /// <summary>
        /// 当前时间
        /// </summary>
        protected float Time => Environment.Time;

        private ILogger? _logger;
        private ISampleEnvironment? _environment;
        private SampleRuntimeContext? _context;

        /// <summary>
        /// 初始化（由运行器调用）
        /// </summary>
        public void Initialize(SampleRuntimeContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = context.Output;
            _environment = context.Environment;
            Config = context.Config;
            Resources = context.Resources;
        }

        /// <summary>
        /// 使用显式输出和环境服务初始化示例。
        /// </summary>
        public void Initialize(ILogger logger, ISampleEnvironment environment)
        {
            Initialize(new SampleRuntimeContext(logger, environment));
        }

        /// <inheritdoc />
        public virtual void Run()
        {
            Section(Title);
            if (!string.IsNullOrEmpty(Description))
            {
                Info(Description);
            }
            Line();

            try
            {
                OnRun();
            }
            catch (Exception ex)
            {
                Error($"Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// 子类实现的运行逻辑
        /// </summary>
        protected abstract void OnRun();

        /// <summary>
        /// 记录日志（Info级别）
        /// </summary>
        protected void Log(string message) => Output.Info(message);

        /// <summary>
        /// 信息日志
        /// </summary>
        protected void Info(string message) => Output.Info(message);

        /// <summary>
        /// 显示分节标题
        /// </summary>
        protected void Section(string title) => Output.Section(title);

        /// <summary>
        /// 显示分隔线
        /// </summary>
        protected void Divider() => Output.Divider();

        /// <summary>
        /// 显示空行
        /// </summary>
        protected void Line() => Output.Line();

        /// <summary>
        /// 记录警告
        /// </summary>
        protected void Warn(string message) => Output.Warn(message);

        /// <summary>
        /// 记录错误
        /// </summary>
        protected void Error(string message) => Output.Error(message);

        /// <summary>
        /// 显示项目符号
        /// </summary>
        protected void Bullet(string text) => Output.Bullet(text);

        /// <summary>
        /// 显示编号项
        /// </summary>
        protected void Numbered(int num, string text) => Output.Numbered(num, text);

        /// <summary>
        /// 显示键值对
        /// </summary>
        protected void KeyValue(string key, string value) => Output.KeyValue(key, value);

        /// <summary>
        /// 获取字符串输入。
        /// </summary>
        protected string Input(string key, string defaultValue = "")
        {
            if (string.IsNullOrWhiteSpace(key))
                return defaultValue;

            return Inputs.TryGetValue(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// 获取整数输入。
        /// </summary>
        protected int InputInt(string key, int defaultValue)
        {
            return int.TryParse(Input(key), out var value) ? value : defaultValue;
        }

        /// <summary>
        /// 刷新输出
        /// </summary>
        protected void Flush() => Output.Flush();

        /// <summary>
        /// 推进时间
        /// </summary>
        protected void AdvanceTime(float delta)
        {
            Environment.Advance(delta);
        }

        /// <summary>
        /// 模拟多帧
        /// </summary>
        protected void SimulateFrames(int frames, float deltaPerFrame = 0.016f)
        {
            for (int i = 0; i < frames; i++)
            {
                Environment.Advance(deltaPerFrame);
            }
        }

        /// <summary>
        /// 执行到完成
        /// </summary>
        protected void ExecuteUntilComplete()
        {
            Environment.ExecuteUntilComplete();
        }

        /// <summary>
        /// 推进到指定时间
        /// </summary>
        protected void AdvanceTo(float targetTime)
        {
            Environment.AdvanceTo(targetTime);
        }

        /// <summary>
        /// 暂停
        /// </summary>
        protected void Pause() => Environment.Pause();

        /// <summary>
        /// 继续
        /// </summary>
        protected void Resume() => Environment.Resume();

        /// <summary>
        /// 重置时间
        /// </summary>
        protected void ResetTime() => Environment.Reset();
    }
}
