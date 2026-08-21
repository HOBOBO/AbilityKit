using System;
using System.Collections.Generic;
using System.Reflection;

namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// 编辑器节点目录：进程内单例，内置节点 + 扫描所有已加载程序集中带
    /// <see cref="BtNodeTypeAttribute"/> 的包外节点。编辑器（图/搜索窗/属性面板）从这里
    /// **主动拉取**描述符，运行时不会反向知道任何编辑器类型。
    /// </summary>
    public static class BtEditorNodeCatalog
    {
        private static BtNodeRegistry? _registry;

        public static BtNodeRegistry Registry
        {
            get
            {
                if (_registry == null)
                {
                    _registry = new BtNodeRegistry();
                    BtBuiltInNodes.RegisterAll(_registry);

                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            _registry.ScanAssembly(assembly);
                        }
                        catch (ReflectionTypeLoadException)
                        {
                            // 含不可加载类型的程序集跳过（编辑器扫描容错）
                        }
                    }
                }
                return _registry;
            }
        }

        public static void Reset()
        {
            _registry = null;
        }
    }
}
