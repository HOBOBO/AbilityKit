using Microsoft.CodeAnalysis;

namespace AbilityKit.Demo.Moba.CodeGen
{
    public static class MobaDiagnosticRules
    {
        public static readonly DiagnosticDescriptor InvalidConfigTableRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.InvalidConfigTableRuleId,
            title: "Invalid MOBA config table declaration",
            messageFormat: "MOBA config table declaration is invalid: {0}",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DuplicateConfigTableRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.DuplicateConfigTableRuleId,
            title: "Duplicate MOBA config table declaration",
            messageFormat: "MOBA config table {0} '{1}' is declared more than once",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidPlanActionModuleRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.InvalidPlanActionModuleRuleId,
            title: "Invalid plan action module",
            messageFormat: "Type '{0}' marked with PlanActionModuleAttribute must derive from MobaPlanActionModuleBase<TArgs, TModule>",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidPlanActionModuleShapeRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.InvalidPlanActionModuleShapeRuleId,
            title: "Plan action module must be concrete",
            messageFormat: "Plan action module '{0}' must be non-abstract and non-generic",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidPlanActionSelfTypeRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.InvalidPlanActionSelfTypeRuleId,
            title: "Invalid plan action self type",
            messageFormat: "Plan action module '{0}' must use itself as the second MobaPlanActionModuleBase type argument",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MissingPlanActionConstructorRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.MissingPlanActionConstructorRuleId,
            title: "Plan action module needs a parameterless constructor",
            messageFormat: "Plan action module '{0}' must have a parameterless constructor accessible from generated code",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MissingPlanActionModuleAttributeRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.MissingPlanActionModuleAttributeRuleId,
            title: "Plan action module is not discoverable",
            messageFormat: "Plan action module '{0}' is missing PlanActionModuleAttribute and will not be included in the generated manifest",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidPlanActionNameRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.InvalidPlanActionNameRuleId,
            title: "Invalid plan action name",
            messageFormat: "Plan action name '{0}' declared by '{1}' is empty or duplicated",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidPayloadFieldIdsDeclarationRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.InvalidPayloadFieldIdsDeclarationRuleId,
            title: "Invalid payload field ID declaration",
            messageFormat: "Payload field declaration on '{0}' is invalid: {1}",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidInputCommandHandlerRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.InvalidInputCommandHandlerRuleId,
            title: "Invalid MOBA input command handler",
            messageFormat: "MOBA input command handler '{0}' is invalid: {1}",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DuplicateBattleRouteRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.DuplicateBattleRouteRuleId,
            title: "Duplicate MOBA battle route",
            messageFormat: "MOBA battle route '{0}:{1}' is declared by both '{2}' and '{3}'",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnsupportedBattleRouteAttributeRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.UnsupportedBattleRouteAttributeRuleId,
            title: "Unsupported derived MOBA battle route attribute",
            messageFormat: "MOBA battle route attribute '{0}' on '{1}' has custom behavior that cannot be represented by the generated manifest",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidBattleRouteIdentityRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.InvalidBattleRouteIdentityRuleId,
            title: "Invalid MOBA battle route identity",
            messageFormat: "MOBA battle route on '{0}' must use a non-zero opCode and a route kind other than Unknown",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MissingInputHandlerFallbackConstructorRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.MissingInputHandlerFallbackConstructorRuleId,
            title: "MOBA input handler has no Activator fallback",
            messageFormat: "MOBA input command handler '{0}' has no public parameterless constructor; Activator fallback is unavailable and the handler must be registered in world DI",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidBattleRouteTypeRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.InvalidBattleRouteTypeRuleId,
            title: "Invalid MOBA battle route type",
            messageFormat: "MOBA battle route on '{0}' is invalid: {1}",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidEventMappingRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.InvalidEventMappingRuleId,
            title: "Invalid MOBA event mapping",
            messageFormat: "MOBA event mapping on '{0}' is invalid: {1}",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DuplicateEventMappingRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.DuplicateEventMappingRuleId,
            title: "Duplicate MOBA event mapping",
            messageFormat: "MOBA event mapping '{0}' is declared more than once",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidTargetQueryFactoryRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.InvalidTargetQueryFactoryRuleId,
            title: "Invalid target query factory",
            messageFormat: "Target query factory '{0}' is invalid: {1}",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DuplicateTargetQueryFactoryCodeRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.DuplicateTargetQueryFactoryCodeRuleId,
            title: "Duplicate target query factory code",
            messageFormat: "Target query {0} code '{1}' is declared by both '{2}' and '{3}'",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidProjectileEmitterRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.InvalidProjectileEmitterRuleId,
            title: "Invalid MOBA projectile emitter",
            messageFormat: "MOBA projectile emitter '{0}' is invalid: {1}",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor AmbiguousProjectileEmitterRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.AmbiguousProjectileEmitterRuleId,
            title: "Ambiguous MOBA projectile emitter",
            messageFormat: "MOBA projectile emitter type '{0}' at priority '{1}' is declared by both '{2}' and '{3}'",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor AmbiguousDefaultProjectileEmitterRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.AmbiguousDefaultProjectileEmitterRuleId,
            title: "Ambiguous default MOBA projectile emitter",
            messageFormat: "MOBA projectile emitter types '{0}' and '{1}' are both declared as default",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidBootstrapStageRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.InvalidBootstrapStageRuleId,
            title: "Invalid MOBA bootstrap stage",
            messageFormat: "MOBA bootstrap stage '{0}' is invalid: {1}",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DuplicateBootstrapStageNameRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.DuplicateBootstrapStageNameRuleId,
            title: "Duplicate MOBA bootstrap stage name",
            messageFormat: "MOBA bootstrap stage name '{0}' is declared by both '{1}' and '{2}'",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidBTreeNodeRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.InvalidBTreeNodeRuleId,
            title: "Invalid MOBA behavior-tree node",
            messageFormat: "MOBA behavior-tree node '{0}' is invalid: {1}",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DuplicateBTreeNodeNameRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.DuplicateBTreeNodeNameRuleId,
            title: "Duplicate MOBA behavior-tree node name",
            messageFormat: "MOBA behavior-tree node name '{0}' is declared by both '{1}' and '{2}'",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidSnapshotEmitterRule = new DiagnosticDescriptor(
            id: MobaDiagnosticIds.InvalidSnapshotEmitterRuleId,
            title: "Invalid MOBA snapshot emitter",
            messageFormat: "MOBA snapshot emitter '{0}' is invalid: {1}",
            category: "AbilityKit.Moba",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}
