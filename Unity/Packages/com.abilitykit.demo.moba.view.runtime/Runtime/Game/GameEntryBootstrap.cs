using System;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Game.EntityCreation;
using UnityEngine;

namespace AbilityKit.Game
{
    public sealed class GameEntryBootstrap : MonoBehaviour
    {
        private void Start()
        {
            if (!GameEntry.IsInitialized) return;

            TryInstallUnityLogSink();

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

        private static void TryInstallUnityLogSink()
        {
            try
            {
                var type = Type.GetType("AbilityKit.Ability.Impl.Common.Log.UnityLogSink, AbilityKit.Ability.Unity");
                if (type == null) return;
                if (!typeof(ILogSink).IsAssignableFrom(type)) return;

                var sink = Activator.CreateInstance(type) as ILogSink;
                if (sink == null) return;
                Log.SetSink(sink);
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }

        private sealed class SystemsTag
        {
        }
        private sealed class SystemsInfo
        {
        }
    }
}
