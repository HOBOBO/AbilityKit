using UnityEngine;

namespace AbilityKit.Game
{
    public sealed class GameEntryBootstrap : MonoBehaviour
    {
        private void Start()
        {
            if (!GameEntry.IsInitialized) return;

            var entry = GameEntry.Instance;

            if (!entry.TryGet(out GameManager gm))
            {
                gm = new GameManager();
                entry.Set(gm);
            }

            gm.EnterGame();

            const int SystemsNodeId = 1;
            var systems = EntityGenerator.CreateChild(entry.Root, SystemsNodeId, "SystemsNode");
            systems.AddComponent(new SystemsTag());
            systems.AddComponent(new SystemsInfo());
        }

        private sealed class SystemsTag
        {
        }
        private sealed class SystemsInfo
        {
        }
    }
}
