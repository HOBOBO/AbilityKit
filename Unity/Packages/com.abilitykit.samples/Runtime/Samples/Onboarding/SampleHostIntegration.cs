#nullable enable

using System;
using System.Linq;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Onboarding
{
    /// <summary>
    /// 新手导览：模拟带界面的 sample 宿主。
    /// </summary>
    [Sample(4, "onboarding", "host", "ui")]
    public sealed class SampleHostIntegration : SampleBase
    {
        public override string Title => "UI Host Integration";
        public override string Description => "演示界面宿主如何列出示例，并在点击后用内存日志运行指定示例";
        public override SampleCategory Category => SampleCategory.Onboarding;

        protected override void OnRun()
        {
            var catalog = SampleCatalogProvider.CreateCatalog();
            var groups = catalog.GroupByCategory();

            Section("UI 可绑定的目录数据");
            KeyValue("SampleCount", catalog.Entries.Count.ToString());
            KeyValue("CategoryCount", groups.Count.ToString());

            foreach (var group in groups.Take(3))
            {
                KeyValue(group.Key.GetDisplayName(), $"{group.Value.Count} samples");
            }

            Divider();
            Section("模拟按钮点击");
            var selected = catalog.Entries.First(x => x.Id == "foundation/hello-world");
            KeyValue("Button.Title", selected.Title);
            KeyValue("Button.Id", selected.Id);
            KeyValue("Button.Category", selected.Category.GetDisplayName());

            var logger = new BufferedSampleLogger();
            var executor = new SampleExecutionService(catalog, _ => new InlineSampleEnvironment());
            var result = executor.Run(selected, logger, new SampleRunOptions
            {
                HostKind = SampleHostKind.Custom,
                ExecutionMode = ExecutionMode.Simulated
            });

            Divider();
            Section("点击运行结果");
            KeyValue("Succeeded", result.Succeeded.ToString());
            KeyValue("CapturedLogEntries", logger.Entries.Count.ToString());
            foreach (var entry in logger.Entries.Where(x => !string.IsNullOrWhiteSpace(x.Text)).Take(6))
            {
                KeyValue(entry.Kind.ToString(), entry.Text);
            }

            Divider();
            Bullet("界面层可以把 catalog.Entries 渲染成列表、树、标签页或搜索结果。");
            Bullet("点击某一项时，使用 entry.Id 或 entry.Index 调用 SampleExecutionService。");
            Bullet("BufferedSampleLogger 可直接转成 UI 日志条目，Unity/MonoGame 也可以实现自己的 ILogger。");
        }

        private sealed class InlineSampleEnvironment : ISampleEnvironment
        {
            public float Time { get; private set; }
            public float DeltaTime { get; private set; }
            public bool IsPaused { get; private set; }
            public event Action<float>? OnTick;

            public void Advance(float delta)
            {
                if (IsPaused) return;
                DeltaTime = delta;
                Time += delta;
                OnTick?.Invoke(delta);
            }

            public void Pause() => IsPaused = true;
            public void Resume() => IsPaused = false;
            public void Reset()
            {
                Time = 0;
                DeltaTime = 0;
                IsPaused = false;
            }

            public void AdvanceTo(float targetTime)
            {
                while (Time < targetTime)
                    Advance(Math.Min(0.016f, targetTime - Time));
            }

            public void Tick() => Advance(0.016f);
            public void ExecuteUntilComplete() => Tick();
        }
    }
}
