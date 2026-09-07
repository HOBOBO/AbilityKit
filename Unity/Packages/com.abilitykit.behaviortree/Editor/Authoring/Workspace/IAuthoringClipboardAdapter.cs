namespace AbilityKit.BehaviorTree.Editor.Authoring.Workspace
{
    internal interface IAuthoringClipboardAdapter
    {
        bool IsAvailable { get; }
        string Status { get; }
    }
}
