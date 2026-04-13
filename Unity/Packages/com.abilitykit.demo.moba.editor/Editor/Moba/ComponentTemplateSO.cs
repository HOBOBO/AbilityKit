using System;
using System.Collections;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/ComponentTemplate", fileName = "ComponentTemplateCO")]
    public sealed class ComponentTemplateSO : MobaConfigTableAssetSO
    {
        public ComponentTemplateDTO[] dataList;

        public override string FileWithoutExt => MobaConfigPaths.ComponentTemplatesFile;
        public override Type EntryType => typeof(ComponentTemplateDTO);
        public override IEnumerable GetEntries() => dataList;
    }
}
