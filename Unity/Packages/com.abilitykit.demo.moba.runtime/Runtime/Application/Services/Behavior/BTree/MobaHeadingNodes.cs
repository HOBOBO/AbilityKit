using System;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.Deterministic;
using ExecutionContext = AbilityKit.BehaviorTree.Execution.ExecutionContext;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;

namespace AbilityKit.Demo.Moba.Services.Behavior.BTree
{
    [NodeType(MobaBTreeNodeTypes.MoveAlongLaunchHeading, "沿生成方向移动", "MOBA", NodeKind.Action)]
    public sealed class MobaMoveAlongLaunchHeadingNode : ActionNodeBase, NodeDescriptorProvider
    {
        private long _priority;
        public NodeDescriptor BuildDescriptor(NodeTypeAttribute attribute) => new(
            attribute.NodeTypeId, attribute.DisplayName, attribute.Category, NodeKind.Action, 0, 0,
            () => new MobaMoveAlongLaunchHeadingNode(),
            new[] { new PropertyField("priority", ValueType.Int64, PropertyValue.Of(50L), "移动意图优先级") });
        public override void OnInit(in NodeInitContext context) => _priority = context.Properties.GetInt64("priority", 50);
        public override NodeState OnTick(ExecutionContext context)
        {
            var bb = context.Blackboard;
            if (!bb.GetBool(MobaBTreeKeys.OwnerCanMove) || !bb.GetBool(MobaBTreeKeys.LaunchHeadingValid)) return NodeState.Success;
            bb.SetBool(MobaBTreeKeys.MoveRequestIsDirection, true);
            bb.SetFixed64(MobaBTreeKeys.MoveRequestX, bb.GetFixed64(MobaBTreeKeys.LaunchHeadingX));
            bb.SetFixed64(MobaBTreeKeys.MoveRequestY, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.MoveRequestZ, bb.GetFixed64(MobaBTreeKeys.LaunchHeadingZ));
            bb.SetFixed64(MobaBTreeKeys.MoveRequestStopRange, Fixed64.Zero);
            bb.SetInt64(MobaBTreeKeys.MoveRequestPriority, _priority);
            bb.SetBool(MobaBTreeKeys.MoveRequestValid, true);
            return NodeState.Success;
        }
    }

    [NodeType(MobaBTreeNodeTypes.ResolveHeadingAim, "按方向解析瞄准", "MOBA", NodeKind.Action)]
    public sealed class MobaResolveHeadingAimNode : ActionNodeBase, NodeDescriptorProvider
    {
        private Fixed64 _distance;
        private string _source;
        public NodeDescriptor BuildDescriptor(NodeTypeAttribute attribute) => new(
            attribute.NodeTypeId, attribute.DisplayName, attribute.Category, NodeKind.Action, 0, 0,
            () => new MobaResolveHeadingAimNode(), new[] {
                new PropertyField("distance", ValueType.Fixed64, PropertyValue.Of(Fixed64.One), "瞄准距离"),
                new PropertyField("directionSource", ValueType.String, PropertyValue.Of("LaunchHeading"), "LaunchHeading 或 CurrentForward") });
        public override void OnInit(in NodeInitContext context)
        {
            _distance = context.Properties.GetFixed64("distance", Fixed64.One);
            _source = context.Properties.GetString("directionSource", "LaunchHeading");
            if (_distance <= Fixed64.Zero || (_source != "LaunchHeading" && _source != "CurrentForward"))
                throw new InvalidOperationException("Heading aim requires a positive distance and a declared direction source.");
        }
        public override NodeState OnTick(ExecutionContext context)
        {
            var bb = context.Blackboard;
            if (_source == "LaunchHeading" && !bb.GetBool(MobaBTreeKeys.LaunchHeadingValid)) return NodeState.Failure;
            var x = bb.GetFixed64(_source == "LaunchHeading" ? MobaBTreeKeys.LaunchHeadingX : MobaBTreeKeys.OwnerForwardX);
            var z = bb.GetFixed64(_source == "LaunchHeading" ? MobaBTreeKeys.LaunchHeadingZ : MobaBTreeKeys.OwnerForwardZ);
            var length = DeterministicMath.Sqrt(x * x + z * z);
            if (length <= Fixed64.Zero) return NodeState.Failure;
            x /= length; z /= length;
            // Scan targets do not become cast locks; directional targeting consumes this same resolved ray.
            bb.SetInt64(MobaBTreeKeys.AimTargetActorId, 0);
            bb.SetFixed64(MobaBTreeKeys.AimX, bb.GetFixed64(MobaBTreeKeys.OwnerX) + x * _distance);
            bb.SetFixed64(MobaBTreeKeys.AimY, bb.GetFixed64(MobaBTreeKeys.OwnerY));
            bb.SetFixed64(MobaBTreeKeys.AimZ, bb.GetFixed64(MobaBTreeKeys.OwnerZ) + z * _distance);
            bb.SetFixed64(MobaBTreeKeys.AimDirectionX, x);
            bb.SetFixed64(MobaBTreeKeys.AimDirectionY, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.AimDirectionZ, z);
            bb.SetBool(MobaBTreeKeys.AimValid, true);
            return NodeState.Success;
        }
    }
}
