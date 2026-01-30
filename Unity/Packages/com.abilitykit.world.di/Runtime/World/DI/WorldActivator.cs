using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace AbilityKit.Ability.World.DI
{
    internal static class WorldActivator
    {
        private sealed class CtorPlan
        {
            public ConstructorInfo Ctor;
            public Type[] ParamTypes;
            public string Signature;
        }

        private sealed class TypePlan
        {
            public Type ImplType;
            public CtorPlan[] Ctors;
        }

        private static readonly ConcurrentDictionary<Type, TypePlan> s_planCache = new ConcurrentDictionary<Type, TypePlan>();

        public static object Create(Type implType, IWorldResolver resolver)
        {
            if (implType == null) throw new ArgumentNullException(nameof(implType));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));

            var plan = s_planCache.GetOrAdd(implType, BuildPlan);
            if (plan.Ctors == null || plan.Ctors.Length == 0)
            {
                throw new InvalidOperationException($"No public constructor found for type: {implType.FullName}");
            }

            CtorPlan best = null;
            object[] bestArgs = null;
            var bestScore = -1;

            StringBuilder diag = null;

            for (int i = 0; i < plan.Ctors.Length; i++)
            {
                var cp = plan.Ctors[i];
                var paramTypes = cp.ParamTypes;
                var args = new object[paramTypes.Length];

                var ok = true;
                int missingAt = -1;
                Type missingType = null;

                for (int p = 0; p < paramTypes.Length; p++)
                {
                    var pt = paramTypes[p];
                    if (resolver.TryResolve(pt, out var arg))
                    {
                        args[p] = arg;
                    }
                    else
                    {
                        ok = false;
                        missingAt = p;
                        missingType = pt;
                        break;
                    }
                }

                if (!ok)
                {
                    diag ??= new StringBuilder(256);
                    diag.Append("  ");
                    diag.Append(cp.Signature);
                    diag.Append(" missing: ");
                    diag.Append(missingType?.FullName ?? (missingType?.Name ?? "unknown"));
                    diag.Append(" @index=");
                    diag.Append(missingAt);
                    diag.AppendLine();
                    continue;
                }

                if (paramTypes.Length > bestScore)
                {
                    best = cp;
                    bestArgs = args;
                    bestScore = paramTypes.Length;
                }
            }

            if (best == null)
            {
                var msg = $"No suitable constructor found for type: {implType.FullName}. Make sure dependencies are registered.";
                if (diag != null)
                {
                    msg += "\nMissing dependencies by constructor:\n" + diag;
                }
                throw new InvalidOperationException(msg);
            }

            return best.Ctor.Invoke(bestArgs);
        }

        private static TypePlan BuildPlan(Type implType)
        {
            var ctors = implType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            if (ctors == null || ctors.Length == 0)
            {
                return new TypePlan { ImplType = implType, Ctors = Array.Empty<CtorPlan>() };
            }

            var plans = new List<CtorPlan>(ctors.Length);
            for (int i = 0; i < ctors.Length; i++)
            {
                var ctor = ctors[i];
                var ps = ctor.GetParameters();
                var paramTypes = new Type[ps.Length];
                for (int p = 0; p < ps.Length; p++)
                {
                    paramTypes[p] = ps[p].ParameterType;
                }

                var sb = new StringBuilder(64);
                sb.Append("ctor(");
                for (int p = 0; p < paramTypes.Length; p++)
                {
                    if (p > 0) sb.Append(", ");
                    sb.Append(paramTypes[p].Name);
                }
                sb.Append(")");

                plans.Add(new CtorPlan
                {
                    Ctor = ctor,
                    ParamTypes = paramTypes,
                    Signature = sb.ToString(),
                });
            }

            // Prefer more specific constructors first (more parameters).
            plans.Sort((a, b) => b.ParamTypes.Length.CompareTo(a.ParamTypes.Length));

            return new TypePlan
            {
                ImplType = implType,
                Ctors = plans.ToArray(),
            };
        }
    }
}
