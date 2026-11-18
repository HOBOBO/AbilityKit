using System;
using System.Reflection;

namespace AbilityKit.Core.Markers
{
    /// <summary>
    /// 兼容扫描器：按输入程序集及其返回类型的顺序调用直接声明的标记特性。
    /// 仅跳过空类型、抽象类型和接口，不自动登记、去重或创建实现对象。
    /// </summary>
    /// <typeparam name="TAttr">待读取的 MarkerAttribute 派生特性。</typeparam>
    public static class MarkerScanner<TAttr> where TAttr : MarkerAttribute
    {
        /// <summary>
        /// 取得当前应用域已加载的程序集数组后同步扫描，不主动加载所有可用程序集。
        /// 发现顺序由运行时决定，不提供裁剪或 AOT 可发现性保证。
        /// </summary>
        /// <param name="registry">目标注册表；为空时扫描无动作。</param>
        public static void ScanAll(IMarkerRegistry registry)
        {
            Scan(AppDomain.CurrentDomain.GetAssemblies(), registry);
        }

        /// <summary>
        /// 按数组顺序扫描，重复程序集会重复回调；注册表或程序集数组为空时无动作。
        /// 类型加载不完整时继续处理 ReflectionTypeLoadException.Types 中的非空类型。
        /// </summary>
        /// <param name="assemblies">程序集数组，跳过其中的空项。</param>
        /// <param name="registry">交给各特性回调的注册表。</param>
        /// <remarks>
        /// 不读取继承特性；每个直接匹配的特性均被调用，特性顺序沿用反射结果。
        /// 其他加载异常、特性构造异常及回调异常立即传播，已完成的副作用不回滚。
        /// </remarks>
        public static void Scan(Assembly[] assemblies, IMarkerRegistry registry)
        {
            if (registry == null) return;
            if (assemblies == null || assemblies.Length == 0) return;

            for (int a = 0; a < assemblies.Length; a++)
            {
                var asm = assemblies[a];
                if (asm == null) continue;

                Type?[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }

                if (types == null) continue;

                for (int i = 0; i < types.Length; i++)
                {
                    var t = types[i];
                    if (t == null) continue;
                    if (t.IsAbstract) continue;
                    if (t.IsInterface) continue;

                    var attrs = GetAttributes(t);
                    for (var attributeIndex = 0; attributeIndex < attrs.Length; attributeIndex++)
                    {
                        attrs[attributeIndex]?.OnScanned(t, registry);
                    }
                }
            }
        }

        private static TAttr[] GetAttributes(Type type)
        {
            var attrs = type.GetCustomAttributes(typeof(TAttr), false);
            if (attrs == null || attrs.Length == 0) return Array.Empty<TAttr>();

            var result = new TAttr[attrs.Length];
            for (var i = 0; i < attrs.Length; i++) result[i] = (TAttr)attrs[i];
            return result;
        }
    }
}
