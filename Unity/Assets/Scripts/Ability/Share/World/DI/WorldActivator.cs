using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

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

            StringBuilder diag = null;

            for (int i = 0; i < ctors.Length; i++)
            {
                var ctor = ctors[i];
                var ps = ctor.GetParameters();
                var args = new object[ps.Length];
                var ok = true;
                List<string> missing = null;

                for (int p = 0; p < ps.Length; p++)
                {
                    if (resolver.TryResolve(ps[p].ParameterType, out var arg))
                    {
                        args[p] = arg;
                    }
                    else
                    {
                        ok = false;
                        missing ??= new List<string>(4);
                        missing.Add(ps[p].ParameterType.FullName ?? ps[p].ParameterType.Name);
                        break;
                    }
                }

                if (!ok)
                {
                    diag ??= new StringBuilder(256);
                    diag.Append("  ctor(");
                    for (int p = 0; p < ps.Length; p++)
                    {
                        if (p > 0) diag.Append(", ");
                        diag.Append(ps[p].ParameterType.Name);
                    }
                    diag.Append(") missing: ");
                    if (missing == null || missing.Count == 0)
                    {
                        diag.Append("unknown");
                    }
                    else
                    {
                        for (int m = 0; m < missing.Count; m++)
                        {
                            if (m > 0) diag.Append(", ");
                            diag.Append(missing[m]);
                        }
                    }
                    diag.AppendLine();
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
                var msg = $"No suitable constructor found for type: {implType.FullName}. Make sure dependencies are registered.";
                if (diag != null)
                {
                    msg += "\nMissing dependencies by constructor:\n" + diag.ToString();
                }
                throw new InvalidOperationException(msg);
            }

            return best.Invoke(bestArgs);
        }
    }
}
