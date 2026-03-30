using System;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using AbilityKit.Ability.World.DI;
using UnityEngine;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterConfig(WorldContainerBuilder builder)
        {
            Debug.Log("[RegisterConfig] Entered");
            builder.TryRegister<IMobaConfigDtoDeserializer>(WorldLifetime.Singleton, _ => JsonNetMobaConfigDtoDeserializer.Instance);
            builder.TryRegister<IMobaConfigDtoBytesDeserializer>(WorldLifetime.Singleton, _ => new LubanMobaConfigDtoBytesDeserializer());

            builder.TryRegister<MobaConfigDatabase>(WorldLifetime.Singleton, _ =>
            {
                Debug.Log("[MobaConfigDatabase Factory] invoked");
                _.TryResolve<IMobaConfigTableRegistry>(out var registry);
                _.TryResolve<IMobaConfigDtoDeserializer>(out var deserializer);
                _.TryResolve<IMobaConfigDtoBytesDeserializer>(out var bytesDeserializer);
                Debug.Log($"[MobaConfigDatabase Factory] registry={(registry != null ? registry.GetType().Name : "null")}, deserializer={(deserializer != null ? "set" : "null")}, bytesDeserializer={(bytesDeserializer != null ? "set" : "null")}");
                var db = new MobaConfigDatabase(registry, deserializer, bytesDeserializer);
                Debug.Log($"[MobaConfigDatabase Factory] after ctor: _tables.Count={CountTables(db)}, dbHash={db.GetHashCode()}");

                try
                {
                    Debug.Log("[MobaConfigDatabase Factory] Loading from Resources");
                    db.LoadFromResources(MobaConfigPaths.DefaultResourcesDir, strict: false);
                    Debug.Log($"[MobaConfigDatabase Factory] completed: CountTables={CountTables(db)}, dbHash={db.GetHashCode()}");
                    return db;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    throw;
                }
            });

            Debug.Log("[RegisterConfig] MobaConfigDatabase registered");
        }

        private static int CountTables(MobaConfigDatabase db)
        {
            var field = typeof(MobaConfigDatabase).GetField("_tables",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field?.GetValue(db) is System.Collections.Generic.Dictionary<Type, object> tables)
                return tables.Count;
            return -1;
        }
    }
}