using System;

namespace AbilityKit.Game.UI
{
    public sealed class UIContext
    {
        public UIManager Manager { get; }

        public UIContext(UIManager manager)
        {
            Manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }
    }
}
