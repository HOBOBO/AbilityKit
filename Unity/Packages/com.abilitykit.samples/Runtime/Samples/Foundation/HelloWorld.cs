#nullable enable

using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Foundation
{
    /// <summary>
    /// 最小可运行示例。
    /// </summary>
    [Sample(10, "foundation", "hello-world")]
    public sealed class HelloWorld : SampleBase
    {
        public override string Title => "Hello World";
        public override string Description => "展示 SampleBase 的最小结构和基础输出能力";
        public override SampleCategory Category => SampleCategory.Foundation;

        protected override void OnRun()
        {
            Log("欢迎来到 AbilityKit.Samples。");
            Log("这个示例只依赖 SampleBase，不依赖具体游戏引擎。");

            Divider();
            Section("SampleBase 提供的常用能力");
            Bullet("Log / Warn / Error：统一输出，宿主决定写到哪里。");
            Bullet("Section / Bullet / KeyValue：让示例输出更容易阅读。");
            Bullet("Environment：由宿主推进的时间环境。");
            Bullet("Config / Resources：由宿主注入的配置和资源入口。");

            Divider();
            Section("最小运行时状态");
            KeyValue("HostKind", Context.HostKind.ToString());
            KeyValue("HelloWorld.HostKind", Context.HostKind.ToString());
            KeyValue("Time", Time.ToString("F3"));
            KeyValue("HelloWorld.TimeBeforeFrame", Time.ToString("F3"));
            AdvanceTime(0.016f);
            KeyValue("Time after one frame", Time.ToString("F3"));
            KeyValue("HelloWorld.TimeAfterFrame", Time.ToString("F3"));
        }
    }
}
