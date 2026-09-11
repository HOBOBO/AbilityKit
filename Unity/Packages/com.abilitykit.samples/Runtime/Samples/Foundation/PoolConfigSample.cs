#nullable enable

using System.Collections.Generic;
using AbilityKit.Core.Pooling;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Foundation
{
    /// <summary>
    /// 演示项目侧按需注册池参数，并通过核心池中心完成获取、使用、归还。
    /// </summary>
    [Sample]
    public sealed class PoolConfigSample : SampleBase
    {
        public override string Title => "Pool Config Registration";

        public override string Description => "Register typed pool configs outside core, then get and release pooled objects through PoolRegistry/Pools.";

        public override SampleCategory Category => SampleCategory.Foundation;

        protected override void OnRun()
        {
            var module = Pools.RegisterConfigModule(
                config => config
                    .Pool<List<int>>("Sample", defaultCapacity: 8, maxSize: 64, prewarmCount: 8, collectionCheck: true)
                    .Pool<Dictionary<string, int>>("Sample", defaultCapacity: 4, maxSize: 32, prewarmCount: 2, collectionCheck: true, neverTrim: true),
                defaultScopeName: "Sample",
                moduleName: "Sample.Foundation.Pools",
                source: "AbilityKit.Samples.Logic",
                priority: 10);

            var overrideModule = new PoolConfigBuilder(
                    defaultScopeName: "Sample",
                    moduleName: "Sample.Foundation.OverridePools",
                    source: "AbilityKit.Samples.Logic.Override",
                    priority: 20)
                .Pool<Dictionary<string, int>>("Sample", defaultCapacity: 16, maxSize: 128, prewarmCount: 4, collectionCheck: true, neverTrim: true)
                .Build();

            using (var overrideRegistration = Pools.RegisterConfigProvider(overrideModule))
            {
                var request = "Sample".For<Dictionary<string, int>>();
                if (Pools.TryGetConfigSnapshot(request, out var snapshot))
                {
                    KeyValue("Config Provider", snapshot.Provider.Name);
                    KeyValue("Config Source", snapshot.Provider.Source);
                    KeyValue("Config Priority", snapshot.Provider.Priority.ToString());
                }

                if (Pools.TryGetConfigReport(request, out var report))
                {
                    KeyValue("Config Candidate Count", report.Matches.Count.ToString());
                    KeyValue("Config Winner", report.Winner.Provider.Name);
                }

                KeyValue("Override Registration", overrideRegistration.Info.ToString());
            }

            var scope = PoolRegistry.GetOrCreateScope("Sample");

            var listPool = scope.GetPool<List<int>>(
                createFunc: () => new List<int>(8),
                onRelease: list => list.Clear());

            var list = listPool.Get();
            list.Add(1);
            list.Add(2);
            list.Add(3);
            KeyValue("List Count Before Release", list.Count.ToString());
            listPool.Release(list);
            KeyValue("List Pool Inactive", listPool.InactiveCount.ToString());

            var dictionaryPool = scope.GetPool<Dictionary<string, int>>(
                createFunc: () => new Dictionary<string, int>(4),
                onRelease: dictionary => dictionary.Clear());

            var dictionary = dictionaryPool.Get();
            dictionary["damage"] = 120;
            dictionary["heal"] = 35;
            KeyValue("Dictionary Count Before Release", dictionary.Count.ToString());
            dictionaryPool.Release(dictionary);
            KeyValue("Dictionary Never Trim", dictionaryPool.NeverTrim.ToString());

            Pools.UnregisterConfigProvider(module);
            PoolRegistry.DestroyScope("Sample");
        }
    }
}
