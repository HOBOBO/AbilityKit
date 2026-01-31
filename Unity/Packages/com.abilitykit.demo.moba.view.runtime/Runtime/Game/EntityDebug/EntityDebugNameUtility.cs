using System;
using System.Reflection;
using AbilityKit.Ability.Share.Common.Log;
using UnityEngine;
using EC = AbilityKit.Ability.EC;

namespace AbilityKit.Game.EntityDebug
{
    public static class EntityDebugNameUtility
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static string GetEntityName(EC.EntityWorld world, EC.EntityId id)
        {
            if (world == null || !world.IsAlive(id)) return null;

            if (world.TryGetName(id, out var stored) && !string.IsNullOrEmpty(stored))
            {
                return stored;
            }

            string name = null;

            world.ForEachComponent(id, (_, obj) =>
            {
                if (name != null) return;
                if (obj == null) return;

                if (obj is IDebugNameProvider provider)
                {
                    name = provider.DebugName;
                    return;
                }

                var t = obj.GetType();
                name = TryGetStringMember(t, obj, "Name")
                    ?? TryGetStringMember(t, obj, "Value")
                    ?? TryGetStringMember(t, obj, "Id");
            });

            return string.IsNullOrEmpty(name) ? null : name;
        }

        private static string TryGetStringMember(Type type, object obj, string member)
        {
            try
            {
                var f = type.GetField(member, Flags);
                if (f != null && f.FieldType == typeof(string))
                {
                    return f.GetValue(obj) as string;
                }

                var p = type.GetProperty(member, Flags);
                if (p != null && p.PropertyType == typeof(string) && p.CanRead && p.GetIndexParameters().Length == 0)
                {
                    return p.GetValue(obj) as string;
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[EntityDebugNameUtility] TryGetStringMember failed");
            }

            return null;
        }
    }
}
