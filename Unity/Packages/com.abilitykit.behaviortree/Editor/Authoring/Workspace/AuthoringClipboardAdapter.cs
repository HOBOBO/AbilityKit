#nullable enable

namespace AbilityKit.BehaviorTree.Editor.Authoring.Workspace
{
    internal sealed class AuthoringClipboardAdapter : IAuthoringClipboardAdapter
    {
        public static readonly IAuthoringClipboardAdapter Unavailable =
            new AuthoringClipboardAdapter(false, "共享复制粘贴能力尚未接入。");

        public AuthoringClipboardAdapter(bool isAvailable, string status)
        {
            IsAvailable = isAvailable;
            Status = status ?? string.Empty;
        }

        public bool IsAvailable { get; }
        public string Status { get; }
    }
}
