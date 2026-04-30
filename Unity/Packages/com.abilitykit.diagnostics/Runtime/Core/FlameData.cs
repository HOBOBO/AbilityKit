using System;
using System.Collections.Generic;
using System.Threading;

namespace AbilityKit.Diagnostics
{
    /// <summary>
    /// 火焰图节点
    /// </summary>
    public sealed class FlameNode
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public long TotalNanoseconds { get; set; }
        public long SelfNanoseconds { get; set; }
        public int HitCount { get; set; }
        public FlameNode Parent { get; set; }
        public Dictionary<string, FlameNode> Children { get; } = new();
        public long Depth => Parent == null ? 0 : Parent.Depth + 1;

        public FlameNode(string name, string? category = null, FlameNode? parent = null)
        {
            Name = name;
            Category = category;
            Parent = parent;
        }

        public FlameNode GetOrCreateChild(string name, string? category)
        {
            if (!Children.TryGetValue(name, out var child))
            {
                child = new FlameNode(name, category, this);
                Children[name] = child;
            }
            return child;
        }

        public void Merge(FlameNode other)
        {
            if (other == null) return;
            TotalNanoseconds += other.TotalNanoseconds;
            SelfNanoseconds += other.SelfNanoseconds;
            HitCount += other.HitCount;

            foreach (var kvp in other.Children)
            {
                var child = GetOrCreateChild(kvp.Key, kvp.Value.Category);
                child.Merge(kvp.Value);
            }
        }
    }

    /// <summary>
    /// 火焰图根节点
    /// </summary>
    public sealed class FlameRoot
    {
        public string SessionId { get; set; }
        public long StartTimestamp { get; set; }
        public long EndTimestamp { get; set; }
        public Dictionary<string, FlameNode> Roots { get; } = new();
        public Dictionary<string, CounterRecord> Counters { get; } = new();
        public Dictionary<string, GaugeRecord> Gauges { get; } = new();
        public Dictionary<string, List<double>> Samples { get; } = new();

        public FlameNode GetOrCreateRoot(string category)
        {
            if (!Roots.TryGetValue(category, out var root))
            {
                root = new FlameNode("ROOT", category);
                Roots[category] = root;
            }
            return root;
        }

        public FlameNode CurrentNode { get; set; }

        public void Push(string name, string category)
        {
            if (CurrentNode == null)
            {
                CurrentNode = GetOrCreateRoot(category);
            }

            CurrentNode = CurrentNode.GetOrCreateChild(name, category);
            CurrentNode.TotalNanoseconds = 0;
            CurrentNode.HitCount++;
        }

        public void Pop(long elapsedNanoseconds)
        {
            if (CurrentNode != null)
            {
                CurrentNode.TotalNanoseconds += elapsedNanoseconds;

                if (CurrentNode.Parent != null)
                {
                    CurrentNode.Parent.SelfNanoseconds -= elapsedNanoseconds;
                    CurrentNode = CurrentNode.Parent;
                }
            }
        }

        public void FinalizeSelfTime()
        {
            foreach (var root in Roots.Values)
            {
                CalculateSelfTime(root);
            }
        }

        private void CalculateSelfTime(FlameNode node)
        {
            long childrenTotal = 0;
            foreach (var child in node.Children.Values)
            {
                CalculateSelfTime(child);
                childrenTotal += child.TotalNanoseconds;
            }
            node.SelfNanoseconds = node.TotalNanoseconds - childrenTotal;
        }
    }

    /// <summary>
    /// 计数器记录
    /// </summary>
    public struct CounterRecord
    {
        public string Name;
        public long Value;
        public long Delta;
        public long MinValue;
        public long MaxValue;
        public double MeanValue;
    }

    /// <summary>
    /// Gauge 记录
    /// </summary>
    public struct GaugeRecord
    {
        public string Name;
        public long Value;
        public long Timestamp;
    }
}
