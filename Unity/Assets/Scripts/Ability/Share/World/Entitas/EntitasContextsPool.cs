using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.World.Entitas
{
    public static class EntitasContextsPool
    {
        private static readonly object Sync = new object();
        private static readonly Stack<global::Contexts> Pool = new Stack<global::Contexts>(4);

        public static int MaxSize { get; set; } = 4;

        public static global::Contexts Rent()
        {
            lock (Sync)
            {
                if (Pool.Count > 0)
                {
                    return Pool.Pop();
                }
            }

            return new global::Contexts();
        }

        public static void Return(global::Contexts contexts)
        {
            if (contexts == null) return;

            lock (Sync)
            {
                if (Pool.Count >= MaxSize) return;
                Pool.Push(contexts);
            }
        }

        public static void Clear(Action<global::Contexts> onRemove = null)
        {
            lock (Sync)
            {
                while (Pool.Count > 0)
                {
                    var c = Pool.Pop();
                    onRemove?.Invoke(c);
                }
            }
        }
    }
}
