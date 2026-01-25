using System;
using System.Collections;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/ProjectileLauncher", fileName = "ProjectileLauncherCO")]
    public sealed class ProjectileLauncherSO : MobaConfigTableAssetSO
    {
        public ProjectileLauncherDTO[] dataList;

        public override string FileWithoutExt => MobaConfigPaths.ProjectileLaunchersFile;
        public override Type EntryType => typeof(ProjectileLauncherDTO);
        public override IEnumerable GetEntries() => dataList;
    }
}
