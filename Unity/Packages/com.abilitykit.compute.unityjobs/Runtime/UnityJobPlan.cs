#nullable enable

using System;
using System.Collections.Generic;
using Unity.Jobs;

namespace AbilityKit.Compute.UnityJobs
{
    public readonly struct UnityJobNodeId : IEquatable<UnityJobNodeId>
    {
        internal UnityJobNodeId(int value)
        {
            Value = value;
        }

        internal int Value { get; }

        public bool Equals(UnityJobNodeId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is UnityJobNodeId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => "JobNode(" + Value + ")";
    }

    /// <summary>
    /// Reusable dependency tracker for a static Job DAG. Business code schedules concrete
    /// Job structs directly, then records each handle so Jobs/Burst AOT can discover the
    /// concrete Schedule call site in the business assembly.
    /// </summary>
    public sealed class UnityJobPlan
    {
        private readonly UnityJobPlanNode[] _nodes;
        private readonly JobHandle[] _nodeHandles;
        private readonly bool[] _recordedNodes;
        private JobHandle _inputDependency;
        private JobHandle _completion;
        private bool _isScheduling;
        private bool _inFlight;

        internal UnityJobPlan(UnityJobPlanNode[] nodes)
        {
            _nodes = nodes;
            _nodeHandles = new JobHandle[nodes.Length];
            _recordedNodes = new bool[nodes.Length];
        }

        public int NodeCount => _nodes.Length;
        public bool IsScheduling => _isScheduling;
        public bool IsInFlight => _inFlight;
        public bool IsCompleted => !_isScheduling && (!_inFlight || _completion.IsCompleted);

        public void BeginSchedule(JobHandle inputDependency = default)
        {
            if (_isScheduling)
            {
                throw new InvalidOperationException("The current job plan schedule has not ended.");
            }

            if (_inFlight)
            {
                if (!_completion.IsCompleted)
                {
                    throw new InvalidOperationException(
                        "Complete the current job plan execution before scheduling it again.");
                }

                Complete();
            }

            Array.Clear(_recordedNodes, 0, _recordedNodes.Length);
            _inputDependency = inputDependency;
            _completion = inputDependency;
            _isScheduling = true;
        }

        public JobHandle GetDependency(UnityJobNodeId node)
        {
            EnsureScheduling();
            var nodeIndex = ValidateNode(node);
            if (_recordedNodes[nodeIndex])
            {
                throw new InvalidOperationException(
                    "A handle has already been recorded for job node '" + _nodes[nodeIndex].Name + "'.");
            }

            var dependency = _inputDependency;
            var dependencies = _nodes[nodeIndex].Dependencies;
            for (var index = 0; index < dependencies.Length; index++)
            {
                var dependencyIndex = dependencies[index];
                if (!_recordedNodes[dependencyIndex])
                {
                    throw new InvalidOperationException(
                        "Record dependency node '" + _nodes[dependencyIndex].Name +
                        "' before scheduling job node '" + _nodes[nodeIndex].Name + "'.");
                }

                dependency = JobHandle.CombineDependencies(
                    dependency,
                    _nodeHandles[dependencyIndex]);
            }

            return dependency;
        }

        public void Record(UnityJobNodeId node, JobHandle handle)
        {
            EnsureScheduling();
            var nodeIndex = ValidateNode(node);
            if (_recordedNodes[nodeIndex])
            {
                throw new InvalidOperationException(
                    "A handle has already been recorded for job node '" + _nodes[nodeIndex].Name + "'.");
            }

            var dependencies = _nodes[nodeIndex].Dependencies;
            for (var index = 0; index < dependencies.Length; index++)
            {
                if (!_recordedNodes[dependencies[index]])
                {
                    throw new InvalidOperationException(
                        "Record every dependency before recording job node '" +
                        _nodes[nodeIndex].Name + "'.");
                }
            }

            _nodeHandles[nodeIndex] = handle;
            _recordedNodes[nodeIndex] = true;
            _completion = JobHandle.CombineDependencies(_completion, handle);
            _inFlight = true;
        }

        public JobHandle EndSchedule()
        {
            EnsureScheduling();
            for (var index = 0; index < _recordedNodes.Length; index++)
            {
                if (!_recordedNodes[index])
                {
                    throw new InvalidOperationException(
                        "No handle was recorded for job node '" + _nodes[index].Name + "'.");
                }
            }

            _isScheduling = false;
            return _completion;
        }

        public void Complete()
        {
            if (!_isScheduling && !_inFlight)
            {
                return;
            }

            _completion.Complete();
            _inputDependency = default;
            _completion = default;
            _isScheduling = false;
            _inFlight = false;
        }

        private void EnsureScheduling()
        {
            if (!_isScheduling)
            {
                throw new InvalidOperationException("Call BeginSchedule before using job plan nodes.");
            }
        }

        private int ValidateNode(UnityJobNodeId node)
        {
            if (node.Value < 0 || node.Value >= _nodes.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(node));
            }

            return node.Value;
        }
    }

    public sealed class UnityJobPlanBuilder
    {
        private readonly List<UnityJobPlanNode> _nodes = new List<UnityJobPlanNode>();
        private bool _built;

        public UnityJobNodeId Add(string name, params UnityJobNodeId[] dependencies)
        {
            if (_built)
            {
                throw new InvalidOperationException("A job plan builder cannot be changed after Build.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A job node name is required.", nameof(name));
            }

            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }

            var dependencyIndices = new int[dependencies.Length];
            for (var index = 0; index < dependencies.Length; index++)
            {
                var dependencyIndex = dependencies[index].Value;
                if (dependencyIndex < 0 || dependencyIndex >= _nodes.Count)
                {
                    throw new ArgumentException(
                        "Job dependencies must refer to nodes already added to this builder.",
                        nameof(dependencies));
                }

                dependencyIndices[index] = dependencyIndex;
            }

            var id = new UnityJobNodeId(_nodes.Count);
            _nodes.Add(new UnityJobPlanNode(name, dependencyIndices));
            return id;
        }

        public UnityJobPlan Build()
        {
            if (_built)
            {
                throw new InvalidOperationException("Build can only be called once.");
            }

            _built = true;
            return new UnityJobPlan(_nodes.ToArray());
        }
    }

    internal sealed class UnityJobPlanNode
    {
        public UnityJobPlanNode(string name, int[] dependencies)
        {
            Name = name;
            Dependencies = dependencies;
        }

        public string Name { get; }
        public int[] Dependencies { get; }
    }
}
