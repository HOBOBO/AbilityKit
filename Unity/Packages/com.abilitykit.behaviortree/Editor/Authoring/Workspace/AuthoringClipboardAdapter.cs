#nullable enable

namespace AbilityKit.BehaviorTree.Editor.Authoring.Workspace
{
    internal sealed class AuthoringClipboardAdapter : IAuthoringClipboardAdapter
    {
        public static readonly IAuthoringClipboardAdapter Unavailable =
            new AuthoringClipboardAdapter(false, "Pending shared copy-paste worker API.");

        public AuthoringClipboardAdapter(bool isAvailable, string status)
        {
            IsAvailable = isAvailable;
            Status = status ?? string.Empty;
        }

        public bool IsAvailable { get; }
        public string Status { get; }
    }
}
