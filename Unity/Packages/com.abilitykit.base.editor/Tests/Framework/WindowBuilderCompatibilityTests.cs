#if UNITY_EDITOR
#pragma warning disable 0618
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Editor.Framework.Tests
{
    public sealed class WindowBuilderCompatibilityTests
    {
        [Test]
        public void FluentApi_PreservesBuilderAndWindowTypes()
        {
            var plugin = new RecordingPlugin();
            var builder = new WindowBuilder<string, TestConfig>();

            Assert.That(builder.Title("Legacy Window"), Is.SameAs(builder));
            Assert.That(builder.LoadData(_ => { }), Is.SameAs(builder));
            Assert.That(builder.DrawDetail(_ => { }), Is.SameAs(builder));
            Assert.That(builder.Filter((_, __) => true), Is.SameAs(builder));
            Assert.That(builder.Config(config => config.Configured = true), Is.SameAs(builder));
            Assert.That(builder.AddPlugin(plugin), Is.SameAs(builder));

            var window = builder.Build();
            try
            {
                Assert.That(window, Is.InstanceOf<PlugableWindow<string, TestConfig>>());
                Assert.That(window.Config, Is.Not.Null);
                Assert.That(window.Config.ValidateCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void Initialize_SortsPluginsAndKeepsConfigDataChainAvailable()
        {
            var later = new RecordingPlugin(20);
            var earlier = new RecordingPlugin(10);
            var window = ScriptableObject.CreateInstance<TestWindow>();
            try
            {
                window.Initialize(new[] { "ignored-by-legacy-contract" }, new[] { later, earlier });
                window.RefreshData();

                Assert.That(window.Config, Is.Not.Null);
                Assert.That(window.Config.ValidateCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(window.AllData, Is.EqualTo(new[] { "alpha", "beta" }));
                Assert.That(earlier.LoadSequence, Is.LessThan(later.LoadSequence));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private sealed class TestWindow : PlugableWindow<string, TestConfig>
        {
            protected override IEnumerable<string> LoadData()
            {
                return new[] { "alpha", "beta" };
            }
        }

        private sealed class TestConfig : IWindowConfig
        {
            public bool Configured { get; set; }
            public int ValidateCount { get; private set; }

            public void Validate()
            {
                ValidateCount++;
            }

            public string ToJson()
            {
                return Configured ? "true" : "false";
            }

            public void FromJson(string json)
            {
                Configured = string.Equals(json, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        private sealed class RecordingPlugin : BaseWindowPlugin<string>
        {
            private static int _sequence;

            public RecordingPlugin(int priority = 0)
            {
                PriorityValue = priority;
            }

            private int PriorityValue { get; }
            public int LoadSequence { get; private set; }
            public override int Priority => PriorityValue;

            public override void OnDataLoaded(IList<string> data)
            {
                LoadSequence = ++_sequence;
            }
        }
    }
}
#pragma warning restore 0618
#endif
