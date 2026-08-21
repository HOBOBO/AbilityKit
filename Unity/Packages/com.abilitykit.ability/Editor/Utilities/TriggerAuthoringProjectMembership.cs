#if UNITY_EDITOR
using System;

namespace AbilityKit.Ability.Editor.Utilities
{
    /// <summary>
    /// 维护 Module 与 Project 的双向成员关系：模块的反向引用和项目的模块清单必须同时更新，
    /// 否则 TriggerAuthoringProjectValidator 的构建校验会漏掉未登记的模块。
    /// </summary>
    internal static class TriggerAuthoringProjectMembership
    {
        public static void Assign(TriggerAuthoringModuleAsset module, TriggerAuthoringProjectAsset project)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));

            var previous = module.Project;
            if (!ReferenceEquals(previous, project))
            {
                previous?.RemoveModule(module);
                module.SetProject(project);
            }

            project?.AddModule(module);
        }

        public static void Detach(TriggerAuthoringModuleAsset module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            Assign(module, null);
        }
    }
}
#endif
