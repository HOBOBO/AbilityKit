#if UNITY_EDITOR
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Tests
{
    public sealed class TriggerAuthoringNodeClipboardTests
    {
        [Test]
        public void NodeClipboard_RoundTripsNestedSubtree()
        {
            var entry = new TriggerNodeClipboardEntry
            {
                Kind = TriggerNodeKind.Action,
                Node = new TriggerNodeData
                {
                    Kind = TriggerNodeKind.Action,
                    Type = "seq",
                    Children =
                    {
                        new TriggerNodeData
                        {
                            Kind = TriggerNodeKind.Action,
                            Type = "debug_log",
                            Arguments =
                            {
                                new TriggerArgumentData
                                {
                                    Name = "message",
                                    Value = new TriggerValueRefData
                                    {
                                        Source = TriggerValueSource.Constant,
                                        Type = TriggerValueType.String,
                                        StringValue = "copied"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var text = TriggerAuthoringNodeClipboard.Marker + TriggerAuthoringNodeClipboard.Serialize(entry);
            var restored = TriggerAuthoringNodeClipboard.TryDeserialize(text, out var parsed);

            Assert.That(restored, Is.True);
            Assert.That(parsed.Kind, Is.EqualTo(TriggerNodeKind.Action));
            Assert.That(parsed.Node.Type, Is.EqualTo("seq"));
            Assert.That(parsed.Node.Children.Count, Is.EqualTo(1));
            Assert.That(parsed.Node.Children[0].Arguments[0].Value.StringValue, Is.EqualTo("copied"));
        }

        [Test]
        public void NodeClipboard_RejectsForeignClipboardText()
        {
            Assert.That(TriggerAuthoringNodeClipboard.TryDeserialize("random text", out _), Is.False);
            Assert.That(TriggerAuthoringNodeClipboard.TryDeserialize(string.Empty, out _), Is.False);
            Assert.That(TriggerAuthoringNodeClipboard.TryDeserialize(null, out _), Is.False);
            Assert.That(TriggerAuthoringNodeClipboard.HasNode("not a node"), Is.False);
        }

        [Test]
        public void NodeClipboard_RejectsCorruptMarkerPayload()
        {
            Assert.That(
                TriggerAuthoringNodeClipboard.TryDeserialize(
                    TriggerAuthoringNodeClipboard.Marker + "{ not valid json",
                    out _),
                Is.False);
        }

        [Test]
        public void RecentUsage_OrdersMostRecentFirstWithoutDuplicates()
        {
            const TriggerNodeKind kind = TriggerNodeKind.Action;
            EditorPrefs.SetString("AbilityKit.TriggerAuthoring.RecentNodes." + kind, string.Empty);
            try
            {
                TriggerNodeRecentUsage.RecordUse(kind, "debug_log");
                TriggerNodeRecentUsage.RecordUse(kind, "seq");
                TriggerNodeRecentUsage.RecordUse(kind, "debug_log");

                var recent = TriggerNodeRecentUsage.GetRecent(kind);

                Assert.That(recent.Count, Is.EqualTo(2));
                Assert.That(recent[0], Is.EqualTo("debug_log"), "最近使用的应排最前且不重复");
                Assert.That(recent[1], Is.EqualTo("seq"));
            }
            finally
            {
                EditorPrefs.SetString("AbilityKit.TriggerAuthoring.RecentNodes." + kind, string.Empty);
            }
        }
    }
}
#endif
