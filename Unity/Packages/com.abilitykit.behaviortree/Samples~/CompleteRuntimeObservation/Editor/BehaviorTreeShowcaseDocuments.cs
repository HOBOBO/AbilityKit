using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Nodes;

namespace AbilityKit.BehaviorTree.Samples.CompleteRuntimeObservation.Editor
{
    internal static class BehaviorTreeShowcaseDocuments
    {
        internal static AuthoringSourceDocument BuildPatrol(AuthoringSourceDocument source)
        {
            var document = Clone(source);
            document.Tree.TreeId = "sample.patrol_loop";
            document.Metadata.Description = "巡逻循环：Sequence、RandomSelector、Wait 和黑板输出。";
            document.Tree.RootNodeId = "patrol";
            document.GetOrCreateNodeMetadata("patrol").Comment = "独立巡逻循环：随机路线与定时等待，完成后由宿主重新开始。";
            RetainReachable(document);
            Arrange(document);
            return document;
        }

        internal static AuthoringSourceDocument BuildChase(AuthoringSourceDocument source)
        {
            var document = Clone(source);
            document.Tree.TreeId = "sample.target_chase";
            document.Metadata.Description = "目标追踪：攻击优先、发现目标后追踪、无目标时等待；可观察 Selector 抢占。";
            var root = document.Tree.Nodes.Single(node => node.Id == "root");
            root.ChildIds.Clear();
            root.ChildIds.AddRange(new[] { "combat", "chase", "idle" });
            document.GetOrCreateNodeMetadata("root").Comment = "优先级：近距离攻击 > 目标追踪 > 空闲；高优先级条件可抢占。";

            Add(document, "chase", BuiltInNodeTypes.Sequence,
                "chaseHasTarget", "setChaseMode", "setChaseBusy", "chaseWait");
            var target = Add(document, "chaseHasTarget", BuiltInNodeTypes.BlackboardCompare);
            target.Properties.Set("leftKey", PropertyValue.Of("self.hasTarget"));
            target.Properties.Set("op", PropertyValue.Of(0L));
            target.Properties.Set("rightKind", PropertyValue.Of(0L));
            target.Properties.Set("rightBool", PropertyValue.Of(true));

            var chaseMode = Add(document, "setChaseMode", BuiltInNodeTypes.SetBlackboard);
            chaseMode.Properties.Set("key", PropertyValue.Of("out.mode"));
            chaseMode.Properties.Set("valueKind", PropertyValue.Of(0L));
            chaseMode.Properties.Set("constString", PropertyValue.Of("Chase"));
            AddClearBusy(document, "setChaseBusy");
            var chaseWait = Add(document, "chaseWait", BuiltInNodeTypes.Wait);
            chaseWait.Properties.Set("mode", PropertyValue.Of(1L));
            chaseWait.Properties.Set("durationFrames", PropertyValue.Of(45L));

            Add(document, "idle", BuiltInNodeTypes.Sequence, "setIdleMode", "setIdleBusy", "idleWait");
            var idleMode = Add(document, "setIdleMode", BuiltInNodeTypes.SetBlackboard);
            idleMode.Properties.Set("key", PropertyValue.Of("out.mode"));
            idleMode.Properties.Set("valueKind", PropertyValue.Of(0L));
            idleMode.Properties.Set("constString", PropertyValue.Of("Idle"));
            AddClearBusy(document, "setIdleBusy");
            var idleWait = Add(document, "idleWait", BuiltInNodeTypes.Wait);
            idleWait.Properties.Set("mode", PropertyValue.Of(1L));
            idleWait.Properties.Set("durationFrames", PropertyValue.Of(20L));

            RetainReachable(document);
            Arrange(document);
            return document;
        }

        private static AuthoringSourceDocument Clone(AuthoringSourceDocument document)
            => AuthoringJson.Load(AuthoringJson.Save(document));

        private static NodeDefinition Add(AuthoringSourceDocument document, string id, string type,
            params string[] children)
        {
            var node = new NodeDefinition { Id = id, Type = type };
            node.ChildIds.AddRange(children);
            document.Tree.Nodes.Add(node);
            document.NodeMetadata.Add(new AuthoringNodeMetadata { NodeId = id, DisplayName = id });
            return node;
        }

        private static void AddClearBusy(AuthoringSourceDocument document, string id)
        {
            var node = Add(document, id, BuiltInNodeTypes.SetBlackboard);
            node.Properties.Set("key", PropertyValue.Of("out.busy"));
            node.Properties.Set("valueKind", PropertyValue.Of(0L));
            node.Properties.Set("constBool", PropertyValue.Of(false));
        }

        private static void RetainReachable(AuthoringSourceDocument document)
        {
            var nodes = document.Tree.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
            var reachable = new HashSet<string>(StringComparer.Ordinal);
            void Visit(string id)
            {
                if (!reachable.Add(id)) return;
                if (!nodes.TryGetValue(id, out var node))
                    throw new InvalidOperationException("示例树缺失节点：" + id);
                foreach (var child in node.ChildIds) Visit(child);
            }
            Visit(document.Tree.RootNodeId);
            document.Tree.Nodes.RemoveAll(node => !reachable.Contains(node.Id));
            document.NodeMetadata.RemoveAll(meta => !reachable.Contains(meta.NodeId));
            document.Layout.RemoveAll(layout => !reachable.Contains(layout.NodeId));
            document.Groups.RemoveAll(group => group.NodeIds.Any(id => !reachable.Contains(id)));
            document.Notes.Clear();
        }

        private static void Arrange(AuthoringSourceDocument document)
        {
            document.Layout.Clear();
            document.Groups.Clear();
            var nodes = document.Tree.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
            var leaf = 0;
            float Place(string id, int depth)
            {
                var children = nodes[id].ChildIds;
                var first = 0f;
                var last = 0f;
                if (children.Count == 0) first = last = leaf++ * 270f;
                else
                {
                    first = Place(children[0], depth + 1);
                    last = first;
                    for (var index = 1; index < children.Count; index++)
                        last = Place(children[index], depth + 1);
                }
                var x = (first + last) / 2f;
                document.Layout.Add(new NodeLayoutData { NodeId = id, X = x, Y = depth * 185f });
                return x;
            }
            Place(document.Tree.RootNodeId, 0);
        }
    }
}
