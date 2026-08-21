using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Authoring
{
    /// <summary>
    /// 导出管线：授权源文档 →（剥离布局）→ 运行时 IR → 校验 → JSON。
    /// 校验失败返回 null 并输出错误列表；调用方（编辑器导出按钮 / 测试）据此决定是否覆盖旧产物。
    /// </summary>
    public static class BtTreeExporter
    {
        /// <summary>提取运行时 IR（深拷贝，避免后续编辑污染已导出的引用）。</summary>
        public static BtTreeDefinition ToRuntimeDefinition(BtAuthoringSourceDocument document)
        {
            if (document == null || document.Tree == null)
                return new BtTreeDefinition();

            // 深拷贝：走 JSON 往返，确保返回对象与编辑态文档完全解耦
            return BtTreeJson.Load(BtTreeJson.Save(document.Tree));
        }

        /// <summary>
        /// 导出为运行时 IR JSON；<paramref name="errors"/> 为空则成功。
        /// 校验（结构/类型/属性/黑板）在导出前强制执行，错误不清空旧产物。
        /// </summary>
        public static string Export(BtAuthoringSourceDocument document, BtNodeRegistry registry, out List<string> errors)
        {
            if (document == null)
            {
                errors = new List<string> { "Authoring document is null." };
                return null!;
            }

            var definition = ToRuntimeDefinition(document);
            errors = BtTreeValidator.Validate(definition, registry);
            if (errors.Count > 0)
            {
                return null!;
            }

            return BtTreeJson.Save(definition);
        }

        /// <summary>从运行时 IR 构造授权文档（无布局；供导入既有 JSON 进入编辑器用）。</summary>
        public static BtAuthoringSourceDocument Import(BtTreeDefinition definition)
        {
            var document = new BtAuthoringSourceDocument();
            if (definition != null)
            {
                document.Tree = BtTreeJson.Load(BtTreeJson.Save(definition));
                foreach (var node in document.Tree.Nodes)
                {
                    document.Layout.Add(new BtNodeLayoutData { NodeId = node.Id });
                }
            }
            return document;
        }
    }
}
