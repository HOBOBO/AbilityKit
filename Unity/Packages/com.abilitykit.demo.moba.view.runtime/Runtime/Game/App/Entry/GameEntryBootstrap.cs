using System;
using AbilityKit.Core.Logging;
using AbilityKit.Game.EntityCreation;

namespace AbilityKit.Game
{
    public sealed class GameEntryBootstrap : IGameEntryModule
    {
        private GameManager _gameManager;

        public string Id => "game.entry.bootstrap";

        public void OnAttach(in GameEntryModuleContext ctx)
        {
            if (!ctx.Root.IsValid) return;

            TryInstallUnityLogSink();

            var root = ctx.Root;
            if (!root.TryGetRef(out GameManager gm))
            {
                gm = new GameManager();
                root.WithRef(gm);
            }

            _gameManager = gm;
            _gameManager.EnterGame();

            const int SystemsNodeId = 1;
            root.TryGetChildById(SystemsNodeId, out var systems);
            if (!systems.IsValid)
            {
                systems = EntityGenerator.CreateChild(root, SystemsNodeId, "SystemsNode");
            }

            systems.WithRef(new SystemsTag());
            systems.WithRef(new SystemsInfo());
        }

        public void OnDetach(in GameEntryModuleContext ctx)
        {
            _gameManager?.LeaveGame();
            _gameManager = null;
        }

        private static void TryInstallUnityLogSink()
        {
            try
            {
                var type = Type.GetType("AbilityKit.Examples.Common.Log.UnityLogSink, AbilityKit.Demo.Moba.View.Runtime");
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
