using System;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.GameplayTags;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
{
    public sealed class ContinuousTagTemplateMO
    {
        public int Id { get; }
        public string Name { get; }
        public GameplayTagContainer ActivationRequiredTags { get; }
        public GameplayTagContainer ActivationBlockedTags { get; }
        public GameplayTagContainer ApplicationTags { get; }
        public GameplayTagContainer RemovalRequiredTags { get; }
        public GameplayTagContainer RemovalBlockedTags { get; }
        public GameplayTagContainer OngoingRequiredTags { get; }
        public GameplayTagContainer OngoingBlockedTags { get; }
        public GameplayTagQuery ActivationCondition { get; }
        public GameplayTagQuery RemovalCondition { get; }
        public GameplayTagQuery OngoingCondition { get; }
        public ulong ExecutionBlockedResultMask { get; }

        public ContinuousTagTemplateMO(ContinuousTagTemplateDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            Id = dto.Id;
            Name = dto.Name;
            ActivationRequiredTags = MobaGameplayTagCatalog.ToConfigContainer(dto.ActivationRequiredTagNames);
            ActivationBlockedTags = MobaGameplayTagCatalog.ToConfigContainer(dto.ActivationBlockedTagNames);
            ApplicationTags = MobaGameplayTagCatalog.ToConfigContainer(dto.ApplicationTagNames);
            RemovalRequiredTags = MobaGameplayTagCatalog.ToConfigContainer(dto.RemovalRequiredTagNames);
            RemovalBlockedTags = MobaGameplayTagCatalog.ToConfigContainer(dto.RemovalBlockedTagNames);
            OngoingRequiredTags = MobaGameplayTagCatalog.ToConfigContainer(dto.OngoingRequiredTagNames);
            OngoingBlockedTags = MobaGameplayTagCatalog.ToConfigContainer(dto.OngoingBlockedTagNames);
            ActivationCondition = MobaTagConditionCompiler.Compile(dto.ActivationCondition);
            RemovalCondition = MobaTagConditionCompiler.Compile(dto.RemovalCondition);
            OngoingCondition = MobaTagConditionCompiler.Compile(dto.OngoingCondition);
            ExecutionBlockedResultMask = dto.ExecutionBlockedResultMask;
        }
    }
}
