using System;
using System.Collections.Generic;
using System.Reflection;

using AbilityKit.BehaviorTree.Editor.Authoring.Extensions;
using UnityEngine.Scripting.APIUpdating;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;
namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// 编辑器节点目录：进程内单例，内置节点 + 扫描所有已加载程序集中带
    /// <see cref="NodeTypeAttribute"/> 的包外节点。编辑器（图/搜索窗/属性面板）从这里
    /// **主动拉取**描述符，运行时不会反向知道任何编辑器类型。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtEditorNodeCatalog")]
    public static class EditorNodeCatalog
    {
        private static NodeRegistry? _registry;

        public static NodeRegistry Registry
        {
            get
            {
                if (_registry == null)
                {
                    _registry = new NodeRegistry();
                    BuiltInNodes.RegisterAll(_registry);

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

                    // 可编程目录源是显式装配，优先于约定式程序集扫描；源之间的冲突
                    // 已由扩展 registry 按 priority / 注册顺序确定性消解。
                    foreach (var descriptor in EditorExtensionRegistry.CollectCatalogDescriptors())
                    {
                        _registry.RegisterOrReplace(descriptor);
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
