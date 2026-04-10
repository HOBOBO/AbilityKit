using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Share.Impl.Moba.Serialization;
using AbilityKit.Core.Common.Config;
using AbilityKit.Ability.Share.Impl.Moba.EntitasAdapters;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Services;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Battle.Requests;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private const string ProtocolWireSerializerModuleKey = "protocol.wire_serializer";
        private const string FeatureConfigFileName = "abilitykit.features.json";

        private static void DestroyEntityTree(AbilityKit.World.ECS.IEntity root)
        {
            if (!root.IsValid) return;

            var list = new List<AbilityKit.World.ECS.IEntity>(16);
            var stack = new Stack<AbilityKit.World.ECS.IEntity>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var e = stack.Pop();
                if (!e.IsValid) continue;
                list.Add(e);

                var count = e.ChildCount;
                for (int i = 0; i < count; i++)
                {
                    stack.Push(e.GetChild(i));
                }
            }

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var e = list[i];
                if (e.IsValid) e.Destroy();
            }
        }

        private void CreateWorld()
        {
            if (_session == null) return;

            TrySetupProtocolWireSerializerInstaller();

            var builder = WorldServiceContainerFactory.CreateWithAttributes(
                AbilityKit.Ability.World.Services.Attributes.WorldServiceProfile.All,
                new[]
                {
                    typeof(WorldServiceContainerFactory).Assembly,
                    typeof(AbilityKit.Ability.Share.Impl.Moba.Systems.MobaWorldBootstrapModule).Assembly,
                    typeof(BattleSessionFeature).Assembly
                },
                new[] { "AbilityKit" }
            );

            builder.AddModule(new MobaConfigWorldModule());

            var options = new WorldCreateOptions(new WorldId(_plan.WorldId), _plan.WorldType)
            {
                ServiceBuilder = builder,
            };
            options.SetEntitasContextsFactory(new MobaEntitasContextsFactory());

            var req = new CreateWorldRequest(options, _plan.CreateWorldOpCode, _plan.CreateWorldPayload);
            _session.CreateWorld(req);
        }

        private void TrySetupProtocolWireSerializerInstaller()
        {
            var path = ResolveConfigPath(FeatureConfigFileName);
            var cfg = PersistentJsonConfigLoader.LoadOrDefault<ModuleInstallerConfigSet>(path, JsonUtility.FromJson<ModuleInstallerConfigSet>);
            var module = cfg != null ? cfg.FindModule(ProtocolWireSerializerModuleKey) : null;
            if (module == null || !module.IsValid) return;

            DemoWireSerializerBootstrap.SetProtocolWireSerializerInstaller(module);
        }

        private static string ResolveConfigPath(string fileName)
        {
            var baseDir = Application.persistentDataPath;
            if (string.IsNullOrEmpty(baseDir)) baseDir = Application.dataPath;
            if (string.IsNullOrEmpty(baseDir)) return fileName;
            return Path.Combine(baseDir, fileName);
        }
    }
}
