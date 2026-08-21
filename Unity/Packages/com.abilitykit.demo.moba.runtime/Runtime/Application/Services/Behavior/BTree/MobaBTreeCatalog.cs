using System;
using AbilityKit.BehaviorTree;

namespace AbilityKit.Demo.Moba.Services.Behavior.BTree
{
    /// <summary>
    /// MOBA 行为树节点目录：内置节点 + 扫描本程序集内带 <see cref="BtNodeTypeAttribute"/>
    /// 的领域节点（替代旧生成式节点清单；扫描进程内仅发生一次）。
    /// public 以便仓库本地工具（CLI 导出）与宿主直接复用同一注册中心。
    /// </summary>
    public static class MobaBTreeCatalog
    {
        private static readonly Lazy<BtNodeRegistry> RegistryLazy = new(CreateRegistry);

        public static BtNodeRegistry Registry => RegistryLazy.Value;

        private static BtNodeRegistry CreateRegistry()
        {
            var registry = new BtNodeRegistry();
            BtBuiltInNodes.RegisterAll(registry);
            registry.ScanAssembly(typeof(MobaBTreeCatalog).Assembly);
            return registry;
        }
    }
}
