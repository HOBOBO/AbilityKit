using System;
using System.Reflection;

namespace AbilityKit.Core.Reflection
{
    /// <summary>
    /// 保留至下一主版本移除窗口的广泛类型查找与静态调用兼容入口。
    /// </summary>
    /// <remarks>
    /// 新代码应使用所属领域的窄接口或显式注册；本类型不管理程序集加载、裁剪保留或 AOT 注册。
    /// 同步使用当前运行时的反射规则，不缓存结果，不切换线程，也不等待方法返回的异步任务。
    /// </remarks>
    [Obsolete("Broad reflection invocation no longer belongs in Core. Use a narrow owner-specific bootstrap contract before the next major version.")]
    public static class ReflectionInvokeUtils
    {
        /// <summary>
        /// 先按运行时规则解析类型名，再按当前已加载程序集的枚举顺序查找首个匹配类型。
        /// </summary>
        /// <remarks>
        /// 空引用或空字符串返回空值；名称区分大小写，本工具不额外裁剪或规范化。
        /// 首次 Type.GetType 位于异常隔离之外，解析或程序集解析回调异常可能传播。
        /// 后续程序集遍历整体隔离异常，遇到异常即结束查找，不保证继续检查其他程序集。
        /// 程序集枚举没有额外排序，不承诺同名类型跨运行时的稳定胜出顺序。
        /// </remarks>
        /// <param name="typeNameOrAssemblyQualified">完整类型名或程序集限定名；运行时解析也可能触发程序集加载。</param>
        /// <returns>首个解析结果，或未找到及受隔离的遍历失败时的空值。</returns>
        public static Type? FindType(string typeNameOrAssemblyQualified)
        {
            if (string.IsNullOrEmpty(typeNameOrAssemblyQualified)) return null;

            var t = Type.GetType(typeNameOrAssemblyQualified, throwOnError: false);
            if (t != null) return t;

            try
            {
                var asms = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < asms.Length; i++)
                {
                    var tt = asms[i].GetType(typeNameOrAssemblyQualified, throwOnError: false);
                    if (tt != null) return tt;
                }
            }
            catch
            {
                /*
                 * 兼容入口在遍历失败时返回空值；隔离范围覆盖整个遍历，不是逐程序集重试。
                 */
            }

            return null;
        }

        /// <summary>
        /// 按名称查找公开静态方法并以空参数调用，失败时返回假。
        /// </summary>
        /// <remarks>
        /// 查找使用 Public 与 Static 标记，不按参数签名筛选重载；歧义、参数数量、开放泛型及调用异常均被隔离。
        /// 返回值被丢弃；返回真只表示反射调用正常返回，不表示返回的任务已经成功完成。
        /// </remarks>
        /// <param name="typeNameOrAssemblyQualified">完整类型名或程序集限定名。</param>
        /// <param name="methodName">区分大小写的方法名。</param>
        /// <returns>找到方法并正常返回时为真；未找到或发生异常时为假。</returns>
        public static bool TryInvokeStaticMethod(string typeNameOrAssemblyQualified, string methodName)
        {
            try
            {
                var t = FindType(typeNameOrAssemblyQualified);
                if (t == null) return false;

                var m = t.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                if (m == null) return false;

                m.Invoke(null, null);
                return true;
            }
            catch
            {
                /*
                 * 此兼容重载不暴露异常；需要诊断时由带异常输出的重载保留原始反射异常。
                 */
                return false;
            }
        }

        /// <summary>
        /// 按名称查找公开静态方法并以空参数调用，同时输出被捕获的异常。
        /// </summary>
        /// <remarks>
        /// 不按参数签名筛选重载，不自动解包 TargetInvocationException，也不等待返回的任务。
        /// 查找与调用均在异常隔离范围内；方法或类型未找到时返回假，但异常输出仍为空。
        /// </remarks>
        /// <param name="typeNameOrAssemblyQualified">完整类型名或程序集限定名。</param>
        /// <param name="methodName">区分大小写的方法名。</param>
        /// <param name="exception">实际捕获的反射或解析异常；成功及无异常的未找到结果均为空。</param>
        /// <returns>找到方法并正常返回时为真；未找到或发生异常时为假。</returns>
        public static bool TryInvokeStaticMethod(string typeNameOrAssemblyQualified, string methodName, out Exception? exception)
        {
            try
            {
                exception = null;
                var t = FindType(typeNameOrAssemblyQualified);
                if (t == null) return false;

                var m = t.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                if (m == null) return false;

                m.Invoke(null, null);
                return true;
            }
            catch (Exception ex)
            {
                exception = ex;
                return false;
            }
        }
    }
}
