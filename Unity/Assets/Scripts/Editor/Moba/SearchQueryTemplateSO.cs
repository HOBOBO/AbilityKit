using System;
using System.Collections;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/SearchQueryTemplate", fileName = "SearchQueryTemplateCO")]
    public sealed class SearchQueryTemplateSO : MobaConfigTableAssetSO
    {
        public SearchQueryTemplateDTO[] dataList;

        public override string FileWithoutExt => MobaConfigPaths.SearchQueryTemplatesFile;
        public override Type EntryType => typeof(SearchQueryTemplateDTO);
        public override IEnumerable GetEntries() => dataList;
    }
}
