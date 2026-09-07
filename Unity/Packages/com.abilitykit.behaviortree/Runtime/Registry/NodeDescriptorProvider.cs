namespace AbilityKit.BehaviorTree.Registry
{
    public interface NodeDescriptorProvider
    {
        NodeDescriptor BuildDescriptor(NodeTypeAttribute attribute);
    }
}
