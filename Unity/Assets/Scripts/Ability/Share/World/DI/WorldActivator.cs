using System;
using System.Reflection;

namespace AbilityKit.Ability.World.DI
{
    internal static class WorldActivator
    {
        public static object Create(Type implType, IWorldServices resolver)
        {
            if (implType == null) throw new ArgumentNullException(nameof(implType));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));

            var ctors = implType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            if (ctors == null || ctors.Length == 0)
            {
                throw new InvalidOperationException($"No public constructor found for type: {implType.FullName}");
            }

            ConstructorInfo best = null;
            object[] bestArgs = null;
            var bestScore = -1;

            for (int i = 0; i < ctors.Length; i++)
            {
                var ctor = ctors[i];
                var ps = ctor.GetParameters();
                var args = new object[ps.Length];
                var ok = true;

                for (int p = 0; p < ps.Length; p++)
                {
                    if (resolver.TryResolve(ps[p].ParameterType, out var arg))
                    {
                        args[p] = arg;
                    }
                    else
                    {
                        ok = false;
                        break;
                    }
                }

                if (!ok) continue;

                if (ps.Length > bestScore)
                {
                    best = ctor;
                    bestArgs = args;
                    bestScore = ps.Length;
                }
            }

            if (best == null)
            {
                throw new InvalidOperationException($"No suitable constructor found for type: {implType.FullName}. Make sure dependencies are registered.");
            }

            return best.Invoke(bestArgs);
        }
    }
}
