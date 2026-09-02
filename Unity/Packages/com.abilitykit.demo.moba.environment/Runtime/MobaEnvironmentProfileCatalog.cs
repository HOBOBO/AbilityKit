using System.Collections.Generic;
using AbilityKit.EnvironmentModel;

namespace AbilityKit.Demo.Moba.EnvironmentModel
{
/// <summary>
/// MOBA 环境 Profile 目录工厂：声明 starter 关注点 taxonomy 与几个具名场景。
/// 这是「项目给分类」的入口——具体项目（MOBA）在这里声明自己的关注点/取值/场景，框架不内置任何业务内容。
/// </summary>
public static class MobaEnvironmentProfileCatalog
{
    /// <summary>构建带 MOBA starter 关注点与场景的目录。</summary>
    public static EnvironmentProfileCatalog CreateDefault()
    {
        var catalog = new EnvironmentProfileCatalog()
            .AddConcern(new EnvironmentConcern(MobaEnvironmentConcerns.UnitClass, MobaEnvironmentConcerns.UnitClassValues, "单位类别"))
            .AddConcern(new EnvironmentConcern(MobaEnvironmentConcerns.TargetShape, MobaEnvironmentConcerns.TargetShapeValues, "目标形态"))
            .AddConcern(new EnvironmentConcern(MobaEnvironmentConcerns.Geometry, MobaEnvironmentConcerns.GeometryValues, "场景几何"))
            .AddConcern(new EnvironmentConcern(MobaEnvironmentConcerns.State, MobaEnvironmentConcerns.StateValues, "状态挂载"));

        AddStarterProfiles(catalog);
        return catalog;
    }

    private static void AddStarterProfiles(EnvironmentProfileCatalog catalog)
    {
        catalog
            .AddProfile(new EnvironmentProfile
            {
                Id = "jungle-camp",
                Selections = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
                {
                    [MobaEnvironmentConcerns.UnitClass] = "jungle",
                    [MobaEnvironmentConcerns.Geometry] = "open",
                },
            })
            .AddProfile(new EnvironmentProfile
            {
                Id = "minion-wave",
                Selections = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
                {
                    [MobaEnvironmentConcerns.UnitClass] = "minion",
                },
            })
            .AddProfile(new EnvironmentProfile
            {
                Id = "walled-arena",
                Selections = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
                {
                    [MobaEnvironmentConcerns.Geometry] = "walled",
                },
            });
    }
}
}
