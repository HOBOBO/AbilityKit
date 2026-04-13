using System;
using System.Collections;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/Projectile", fileName = "ProjectileCO")]
    public sealed class ProjectileSO : MobaConfigTableAssetSO
    {
        public ProjectileDTO[] dataList;

        public override string FileWithoutExt => MobaConfigPaths.ProjectilesFile;
        public override Type EntryType => typeof(ProjectileDTO);
        public override IEnumerable GetEntries() => dataList;
    }
}
