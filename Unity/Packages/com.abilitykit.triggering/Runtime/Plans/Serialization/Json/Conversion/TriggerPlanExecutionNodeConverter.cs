using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    /// <summary>
    /// 负责触发计划执行树的反序列化与执行节点展开，避免 TriggerPlanConverter 同时承担值转换和执行树装配。
    /// </summary>
    internal sealed class TriggerPlanExecutionNodeConverter
    {
        private readonly TriggerPlanConverter _context;
        private readonly TriggerPlanExecutionNodeReferenceResolver _referenceResolver = new TriggerPlanExecutionNodeReferenceResolver();
        private static readonly Dictionary<ETriggerPlanExecutableKind, ExecutionNodeConverterBase> _executionNodeConverters = BuildExecutionNodeConverters();

        public TriggerPlanExecutionNodeConverter(TriggerPlanConverter context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public ITriggerPlanExecutable ConvertExecutionRoot(TriggerPlanJsonDatabase.TriggerPlanDto dto, TriggerPlanJsonDatabase.TriggerPlanDatabaseDto databaseDto)
        {
            if (dto == null)
            {
                return null;
            }

            var explicitRoot = ConvertExecutionNode(dto.ExecutionRoot, databaseDto);
            if (explicitRoot != null)
            {
                return explicitRoot;
            }

            var actions = _context.ConvertActions(dto.Actions);
            if (actions.Length == 0)
            {
                return null;
            }

            var children = new ITriggerPlanExecutable[actions.Length];
            for (int i = 0; i < actions.Length; i++)
            {
                children[i] = new ActionCallTriggerPlanExecutable(actions[i], i);
            }

            return new SequenceTriggerPlanExecutable(children);
        }

        private ITriggerPlanExecutable ConvertExecutionNode(TriggerPlanJsonDatabase.ExecutionNodeDto dto, TriggerPlanJsonDatabase.TriggerPlanDatabaseDto databaseDto)
        {
            if (dto == null)
            {
                return null;
            }

            var resolvingKeys = new List<string>();
            dto = _referenceResolver.Resolve(dto, databaseDto, resolvingKeys);
            try
            {
                if (!Enum.TryParse<ETriggerPlanExecutableKind>(NormalizeKind(dto.Kind), true, out var kind))
                {
                    throw new InvalidOperationException($"Execution node kind not supported: {dto.Kind}");
                }

                if (!_executionNodeConverters.TryGetValue(kind, out var converter) || converter == null)
                {
                    throw new InvalidOperationException($"Execution node kind not supported: {kind}");
                }

                return converter.Convert(this, dto, databaseDto);
            }
            finally
            {
                _referenceResolver.EndResolve(resolvingKeys);
            }
        }

        private ITriggerPlanExecutable[] ConvertExecutionNodes(List<TriggerPlanJsonDatabase.ExecutionNodeDto> dtos, TriggerPlanJsonDatabase.TriggerPlanDatabaseDto databaseDto)
        {
            if (dtos == null || dtos.Count == 0)
            {
                return Array.Empty<ITriggerPlanExecutable>();
            }

            var nodes = new ITriggerPlanExecutable[dtos.Count];
            for (int i = 0; i < dtos.Count; i++)
            {
                nodes[i] = ConvertExecutionNode(dtos[i], databaseDto);
            }

            return nodes;
        }

        private static ITriggerPlanExecutable BuildBranch(ITriggerPlanExecutable[] children)
        {
            if (children == null || children.Length == 0)
            {
                return null;
            }

            return children.Length == 1 ? children[0] : new SequenceTriggerPlanExecutable(children);
        }

        private static Dictionary<ETriggerPlanExecutableKind, ExecutionNodeConverterBase> BuildExecutionNodeConverters()
        {
            return new Dictionary<ETriggerPlanExecutableKind, ExecutionNodeConverterBase>
            {
                [ETriggerPlanExecutableKind.Action] = new ActionExecutionNodeConverter(),
                [ETriggerPlanExecutableKind.Sequence] = new SequenceExecutionNodeConverter(),
                [ETriggerPlanExecutableKind.Selector] = new SelectorExecutionNodeConverter(),
                [ETriggerPlanExecutableKind.Random] = new RandomExecutionNodeConverter(),
                [ETriggerPlanExecutableKind.Parallel] = new ParallelExecutionNodeConverter(),
                [ETriggerPlanExecutableKind.If] = new IfExecutionNodeConverter(),
                [ETriggerPlanExecutableKind.Repeat] = new RepeatExecutionNodeConverter(),
                [ETriggerPlanExecutableKind.Until] = new UntilExecutionNodeConverter(),
                [ETriggerPlanExecutableKind.ForEach] = new ForEachExecutionNodeConverter(),
                [ETriggerPlanExecutableKind.Invert] = new InvertExecutionNodeConverter(),
                [ETriggerPlanExecutableKind.Succeed] = new SucceedExecutionNodeConverter(),
                [ETriggerPlanExecutableKind.Fail] = new FailExecutionNodeConverter(),
                [ETriggerPlanExecutableKind.Metadata] = new MetadataExecutionNodeConverter()
            };
        }

        private static string NormalizeKind(string kind)
        {
            if (string.IsNullOrEmpty(kind))
            {
                return nameof(ETriggerPlanExecutableKind.Sequence);
            }

            switch (kind.Trim().ToLowerInvariant())
            {
                case "action":
                    return nameof(ETriggerPlanExecutableKind.Action);
                case "sequence":
                case "seq":
                    return nameof(ETriggerPlanExecutableKind.Sequence);
                case "selector":
                case "select":
                    return nameof(ETriggerPlanExecutableKind.Selector);
                case "random":
                case "random_selector":
                case "randomselector":
                    return nameof(ETriggerPlanExecutableKind.Random);
                case "if":
                case "ifelse":
                case "if_else":
                    return nameof(ETriggerPlanExecutableKind.If);
                case "parallel":
                case "all":
                    return nameof(ETriggerPlanExecutableKind.Parallel);
                case "repeat":
                case "loop":
                    return nameof(ETriggerPlanExecutableKind.Repeat);
                case "until":
                case "repeat_until":
                case "repeatuntil":
                    return nameof(ETriggerPlanExecutableKind.Until);
                case "foreach":
                case "for_each":
                    return nameof(ETriggerPlanExecutableKind.ForEach);
                case "invert":
                case "not":
                    return nameof(ETriggerPlanExecutableKind.Invert);
                case "succeed":
                case "success":
                case "always_success":
                case "alwayssuccess":
                    return nameof(ETriggerPlanExecutableKind.Succeed);
                case "fail":
                case "failure":
                case "always_fail":
                case "alwaysfail":
                    return nameof(ETriggerPlanExecutableKind.Fail);
                case "metadata":
                case "decorator":
                case "tags":
                case "tag":
                case "modifiers":
                case "modifier":
                case "stack":
                case "hierarchy":
                case "capability":
                case "duration":
                case "continuous":
                    return nameof(ETriggerPlanExecutableKind.Metadata);
                default:
                    return kind;
            }
        }

        private abstract class ExecutionNodeConverterBase
        {
            public abstract ITriggerPlanExecutable Convert(
                TriggerPlanExecutionNodeConverter context,
                TriggerPlanJsonDatabase.ExecutionNodeDto dto,
                TriggerPlanJsonDatabase.TriggerPlanDatabaseDto databaseDto);

            protected static ITriggerPlanCondition Condition(
                TriggerPlanExecutionNodeConverter context,
                TriggerPlanJsonDatabase.ExecutionNodeDto dto)
            {
                return context._context.ConvertCondition(dto.Condition);
            }

            protected static ITriggerPlanExecutable[] Children(
                TriggerPlanExecutionNodeConverter context,
                TriggerPlanJsonDatabase.ExecutionNodeDto dto,
                TriggerPlanJsonDatabase.TriggerPlanDatabaseDto databaseDto)
            {
                return context.ConvertExecutionNodes(dto.Children, databaseDto);
            }

            protected static ITriggerPlanExecutable Branch(ITriggerPlanExecutable[] children)
            {
                return BuildBranch(children);
            }
        }

        private sealed class ActionExecutionNodeConverter : ExecutionNodeConverterBase
        {
            public override ITriggerPlanExecutable Convert(
                TriggerPlanExecutionNodeConverter context,
                TriggerPlanJsonDatabase.ExecutionNodeDto dto,
                TriggerPlanJsonDatabase.TriggerPlanDatabaseDto databaseDto)
            {
                if (dto.Action == null)
                {
                    throw new InvalidOperationException("Action execution node requires Action payload.");
                }

                return new ActionCallTriggerPlanExecutable(
                    context._context.ConvertAction(dto.Action),
                    originalActionIndex: -1,
                    condition: Condition(context, dto),
                    weight: dto.Weight);
            }
        }

        private sealed class SequenceExecutionNodeConverter : ExecutionNodeConverterBase
        {
            public override ITriggerPlanExecutable Convert(
                TriggerPlanExecutionNodeConverter context,
                TriggerPlanJsonDatabase.ExecutionNodeDto dto,
                TriggerPlanJsonDatabase.TriggerPlanDatabaseDto databaseDto)
            {
                return new SequenceTriggerPlanExecutable(Children(context, dto, databaseDto), Condition(context, dto), dto.Weight);
            }
        }

        private sealed class SelectorExecutionNodeConverter : ExecutionNodeConverterBase
        {
            public override ITriggerPlanExecutable Convert(
                TriggerPlanExecutionNodeConverter context,
                TriggerPlanJsonDatabase.ExecutionNodeDto dto,
                TriggerPlanJsonDatabase.TriggerPlanDatabaseDto databaseDto)
            {
                return new SelectorTriggerPlanExecutable(Children(context, dto, databaseDto), Condition(context, dto), dto.Weight);
            }
        }

        private sealed class RandomExecutionNodeConverter : ExecutionNodeConverterBase
        {
            public override ITriggerPlanExecutable Convert(
                TriggerPlanExecutionNodeConverter context,
                TriggerPlanJsonDatabase.ExecutionNodeDto dto,
                TriggerPlanJsonDatabase.TriggerPlanDatabaseDto databaseDto)
            {
                return new RandomTriggerPlanExecutable(Children(context, dto, databaseDto), Condition(context, dto), dto.Weight);
            }
        }

        private sealed class ParallelExecutionNodeConverter : ExecutionNodeConverterBase
        {
            public override ITriggerPlanExecutable Convert(
                TriggerPlanExecutionNodeConverter context,
                TriggerPlanJsonDatabase.ExecutionNodeDto dto,
                TriggerPlanJsonDatabase.TriggerPlanDatabaseDto databaseDto)
            {
                return new ParallelTriggerPlanExecutable(Children(context, dto, databaseDto), Condition(context, dto), dto.Weight);
            }
        }

        private sealed class IfExecutionNodeConverter : ExecutionNodeConverterBase
        {
            public override ITriggerPlanExecutable Convert(
                TriggerPlanExecutionNodeConverter context,
                TriggerPlanJsonDatabase.ExecutionNodeDto dto,
                TriggerPlanJsonDatabase.TriggerPlanDatabaseDto databaseDto)
            {
                return new IfTriggerPlanExecutable(
                    Condition(context, dto),
                    Branch(Children(context, dto, databaseDto)),
                    Branch(context.ConvertExecutionNodes(dto.ElseChildren, databaseDto)),
                    guardCondition: null,
                    weight: dto.Weight);
            }
        }

        private sealed class RepeatExecutionNodeConverter : ExecutionNodeConverterBase
        {
            public override ITriggerPlanExecutable Convert(
                TriggerPlanExecutionNodeConverter context,
                TriggerPlanJsonDatabase.ExecutionNodeDto dto,
                TriggerPlanJsonDatabase.TriggerPlanDatabaseDto databaseDto)
            {
                return new RepeatTriggerPlanExecutable(Branch(Children(context, dto, databaseDto)), dto.Count, Condition(context, dto), dto.Weight);
            }
        }

        private sealed class UntilExecutionNodeConverter : ExecutionNodeConverterBase
        {
            public override ITriggerPlanExecutable Convert(
                TriggerPlanExecutionNodeConverter context,
                TriggerPlanJsonDatabase.ExecutionNodeDto dto,
                TriggerPlanJsonDatabase.TriggerPlanDatabaseDto databaseDto)
            {
                return new UntilTriggerPlanExecutable(
                    Branch(Children(context, dto, databaseDto)),
                    context._context.ConvertCondition(dto.UntilCondition),
                    dto.MaxIterations,
                    Condition(context, dto),
                    dto.Weight);
            }
        }

        private sealed class ForEachExecutionNodeConverter : ExecutionNodeConverterBase
        {
            public override ITriggerPlanExecutable Convert(
                TriggerPlanExecutionNodeConverter context,
                TriggerPlanJsonDatabase.ExecutionNodeDto dto,
                TriggerPlanJsonDatabase.TriggerPlanDatabaseDto databaseDto)
            {
                var target = context._context.ConvertExecutionBlackboardTarget(dto.ItemTarget, "itemTarget");
                return new ForEachTriggerPlanExecutable(
                    context._context.ConvertExecutionNumericValue(dto.Collection),
                    in target,
                    Branch(Children(context, dto, databaseDto)),
                    dto.MaxIterations,
                    Condition(context, dto),
                    dto.Weight);
            }
        }

        private sealed class InvertExecutionNodeConverter : ExecutionNodeConverterBase
        {
            public override ITriggerPlanExecutable Convert(
                TriggerPlanExecutionNodeConverter context,
                TriggerPlanJsonDatabase.ExecutionNodeDto dto,
                TriggerPlanJsonDatabase.TriggerPlanDatabaseDto databaseDto)
            {
                return new InvertTriggerPlanExecutable(Branch(Children(context, dto, databaseDto)), Condition(context, dto), dto.Weight);
            }
        }

        private sealed class SucceedExecutionNodeConverter : ExecutionNodeConverterBase
        {
            public override ITriggerPlanExecutable Convert(
                TriggerPlanExecutionNodeConverter context,
                TriggerPlanJsonDatabase.ExecutionNodeDto dto,
                TriggerPlanJsonDatabase.TriggerPlanDatabaseDto databaseDto)
            {
                return new SucceedTriggerPlanExecutable(Branch(Children(context, dto, databaseDto)), Condition(context, dto), dto.Weight);
            }
        }

        private sealed class FailExecutionNodeConverter : ExecutionNodeConverterBase
        {
            public override ITriggerPlanExecutable Convert(
                TriggerPlanExecutionNodeConverter context,
                TriggerPlanJsonDatabase.ExecutionNodeDto dto,
                TriggerPlanJsonDatabase.TriggerPlanDatabaseDto databaseDto)
            {
                return new FailTriggerPlanExecutable(Branch(Children(context, dto, databaseDto)), dto.Reason, Condition(context, dto), dto.Weight);
            }
        }

        private sealed class MetadataExecutionNodeConverter : ExecutionNodeConverterBase
        {
            public override ITriggerPlanExecutable Convert(
                TriggerPlanExecutionNodeConverter context,
                TriggerPlanJsonDatabase.ExecutionNodeDto dto,
                TriggerPlanJsonDatabase.TriggerPlanDatabaseDto databaseDto)
            {
                var metadataKind = ParseMetadataKind(dto);
                return new MetadataTriggerPlanExecutable(
                    Branch(Children(context, dto, databaseDto)),
                    metadataKind,
                    dto.Values,
                    Condition(context, dto),
                    dto.Weight);
            }

            private static ETriggerPlanMetadataKind ParseMetadataKind(TriggerPlanJsonDatabase.ExecutionNodeDto dto)
            {
                var value = !string.IsNullOrWhiteSpace(dto.MetadataKind) ? dto.MetadataKind : dto.Kind;
                if (string.IsNullOrWhiteSpace(value))
                {
                    return ETriggerPlanMetadataKind.Generic;
                }

                switch (value.Trim().ToLowerInvariant())
                {
                    case "tag":
                    case "tags":
                        return ETriggerPlanMetadataKind.Tags;
                    case "modifier":
                    case "modifiers":
                        return ETriggerPlanMetadataKind.Modifiers;
                    case "stack":
                        return ETriggerPlanMetadataKind.Stack;
                    case "hierarchy":
                        return ETriggerPlanMetadataKind.Hierarchy;
                    case "capability":
                        return ETriggerPlanMetadataKind.Capability;
                    case "duration":
                        return ETriggerPlanMetadataKind.Duration;
                    case "continuous":
                        return ETriggerPlanMetadataKind.Continuous;
                }

                return Enum.TryParse<ETriggerPlanMetadataKind>(value, true, out var kind)
                    ? kind
                    : ETriggerPlanMetadataKind.Generic;
            }
        }
    }
}
