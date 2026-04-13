using System;
using System.Reflection;
using AbilityKit.Core.Common.Config;
using AbilityKit.Core.Common.Log;
using AbilityKit.Core.Common.Reflection;

namespace AbilityKit.Demo.Moba.Serialization
{
    public static class DemoWireSerializerBootstrap
    {
        private static bool s_tried;
        private static bool s_installed;
        private static ModuleInstallerConfig s_installer;

        public static void SetProtocolWireSerializerInstaller(ModuleInstallerConfig installer)
        {
            s_installer = installer;
        }

        public static bool TryInstallMemoryPack()
        {
            if (s_tried) return s_installed;
            s_tried = true;

            var module = s_installer;
            if (module == null || !module.IsValid) return false;

            try
            {
                if (!ReflectionInvokeUtils.TryInvokeStaticMethod(module.InstallerType, module.GetEffectiveMethod()))
                {
                    Log.Info("[DemoWireSerializerBootstrap] Wire serializer installer not found/invokable; skip");
                    return false;
                }

                Log.Info($"[DemoWireSerializerBootstrap] MemoryPack wire serializer installed. current={TryGetCurrentWireSerializerTypeName()}");
                s_installed = true;
                return true;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[DemoWireSerializerBootstrap] Install MemoryPack wire serializer failed");
                return false;
            }
        }

        private static string TryGetCurrentWireSerializerTypeName()
        {
            try
            {
                var t = Type.GetType("AbilityKit.Protocol.Serialization.WireSerializer, AbilityKit.Protocol", throwOnError: false);
                if (t == null)
                {
                    var asms = AppDomain.CurrentDomain.GetAssemblies();
                    for (int i = 0; i < asms.Length; i++)
                    {
                        var tt = asms[i].GetType("AbilityKit.Protocol.Serialization.WireSerializer", throwOnError: false);
                        if (tt != null)
                        {
                            t = tt;
                            break;
                        }
                    }
                }

                if (t == null) return "<WireSerializer type not found>";

                var p = t.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
                if (p == null) return "<WireSerializer.Current not found>";

                var cur = p.GetValue(null);
                return cur != null ? cur.GetType().FullName : "<null>";
            }
            catch
            {
                return "<error>";
            }
        }
    }
}
