using System;
using System.Collections;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/TagTemplate", fileName = "TagTemplateCO")]
    public sealed class TagTemplateSO : MobaConfigTableAssetSO
    {
        public TagTemplateDTO[] dataList;

        public override string FileWithoutExt => MobaConfigPaths.TagTemplatesFile;
        public override Type EntryType => typeof(TagTemplateDTO);
        public override IEnumerable GetEntries() => dataList;
    }
}
