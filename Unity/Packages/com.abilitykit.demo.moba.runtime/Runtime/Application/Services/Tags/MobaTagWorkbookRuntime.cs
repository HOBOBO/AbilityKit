using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.Config;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.GameplayTags;
using Luban;
using Newtonsoft.Json.Linq;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaTagWorkbookRuntime
    {
        private readonly Dictionary<int, moba_luban.DRTagDefinitions> _tags;
        private readonly Dictionary<int, moba_luban.DRTagRuleGroups> _groups;
        private readonly Dictionary<int, moba_luban.DRTagRuleNodes> _nodes;
        private readonly Dictionary<int, TagConditionDTO> _conditions = new Dictionary<int, TagConditionDTO>();
        private readonly Dictionary<int, GameplayTagQuery> _queries = new Dictionary<int, GameplayTagQuery>();
        private readonly Dictionary<int, MobaTagResultFlags> _flagsById = new Dictionary<int, MobaTagResultFlags>();
        private readonly Dictionary<moba_luban.MobaTagBehaviorGate, MobaTagResultFlags> _behaviorGates =
            new Dictionary<moba_luban.MobaTagBehaviorGate, MobaTagResultFlags>();
        private readonly Dictionary<int, moba_luban.DRTagResultFlags> _flagRows;
        private readonly List<moba_luban.DRTagResultMappings> _mappingRows;
        private readonly List<moba_luban.DRTagBehaviorGates> _gateRows;
        private readonly Dictionary<int, List<moba_luban.DRTagRuleNodes>> _children =
            new Dictionary<int, List<moba_luban.DRTagRuleNodes>>();
        private readonly List<(MobaTagResultFlags Flag, GameplayTagQuery Query)> _mappings =
            new List<(MobaTagResultFlags, GameplayTagQuery)>();

        public MobaTagWorkbookRuntime(ITextAssetLoader assets)
        {
            MobaGameplayTagCatalog.RegisterDefinitions(assets);
            _tags = new moba_luban.TagDefinitions(Load(assets, "tag_definitions")).DataMap;
            _groups = new moba_luban.TagRuleGroups(Load(assets, "tag_rule_groups")).DataMap;
            _nodes = new moba_luban.TagRuleNodes(Load(assets, "tag_rule_nodes")).DataMap;
            foreach (var node in _nodes.Values)
            {
                if (!_children.TryGetValue(node.ParentId, out var siblings))
                    _children.Add(node.ParentId, siblings = new List<moba_luban.DRTagRuleNodes>());
                siblings.Add(node);
            }
            foreach (var siblings in _children.Values)
                siblings.Sort((left, right) => left.Order.CompareTo(right.Order));
            var reachable = new HashSet<int>();
            foreach (var group in _groups.Values)
            {
                var count = 0;
                var condition = CompileGroup(group.Id, new HashSet<int>(), 0, ref count, reachable);
                _conditions.Add(group.Id, condition);
                _queries.Add(group.Id, MobaTagConditionCompiler.Compile(condition));
            }
            if (reachable.Count != _nodes.Count)
                throw new InvalidOperationException("Tag rules contain orphan nodes.");
            var flags = new moba_luban.TagResultFlags(Load(assets, "tag_result_flags")).DataMap;
            _flagRows = flags;
            var bits = new HashSet<int>();
            foreach (var flag in flags.Values)
            {
                if (flag.BitIndex < 0 || flag.BitIndex >= 64 || !bits.Add(flag.BitIndex) ||
                    !Enum.TryParse(flag.Key, out MobaTagResultFlags generated) ||
                    (ulong)generated != (1UL << flag.BitIndex))
                    throw new InvalidOperationException($"Tag result flag disagrees with generated code: {flag.Key}");
                _flagsById.Add(flag.Id, (MobaTagResultFlags)(1UL << flag.BitIndex));
            }
            _mappingRows = new moba_luban.TagResultMappings(Load(assets, "tag_result_mappings")).DataList;
            foreach (var mapping in _mappingRows)
            {
                if (!flags.TryGetValue(mapping.FlagId, out var flag) || !_queries.TryGetValue(mapping.RuleGroupId, out var query))
                    throw new InvalidOperationException($"Invalid tag result mapping: {mapping.Id}");
                _mappings.Add(((MobaTagResultFlags)(1UL << flag.BitIndex), query));
            }
            _gateRows = new moba_luban.TagBehaviorGates(Load(assets, "tag_behavior_gates")).DataList;
            foreach (var gate in _gateRows)
            {
                if (!Enum.IsDefined(typeof(moba_luban.MobaTagBehaviorGate), gate.Id))
                    throw new InvalidOperationException($"Unknown behavior gate: {gate.Id}");
                var mask = MobaTagResultFlags.None;
                foreach (var flagId in gate.BlockedFlagIds)
                {
                    if (!_flagsById.TryGetValue(flagId, out var flag) || (mask & flag) != 0)
                        throw new InvalidOperationException($"Invalid behavior gate flag: {gate.Id}/{flagId}");
                    mask |= flag;
                }
                if (mask == MobaTagResultFlags.None || !_behaviorGates.TryAdd(gate.Id, mask))
                    throw new InvalidOperationException($"Invalid behavior gate: {gate.Id}");
            }
        }

        public bool TryGetBehaviorGateMask(moba_luban.MobaTagBehaviorGate gateId, out MobaTagResultFlags mask) =>
            _behaviorGates.TryGetValue(gateId, out mask);

        public MobaTagResultFlags Evaluate(GameplayTagContainer tags)
        {
            var result = MobaTagResultFlags.None;
            for (var i = 0; i < _mappings.Count; i++)
                if (_mappings[i].Query.Matches(tags)) result |= _mappings[i].Flag;
            return result;
        }

        internal int ResultMappingCount => _mappings.Count;

        public GameplayTagQuery GetRule(int groupId) => _queries[groupId];

        public bool TryGetRule(int groupId, out GameplayTagQuery query) => _queries.TryGetValue(groupId, out query);

        public bool TryGetActiveTagName(int definitionId, out string name)
        {
            name = null;
            if (!_tags.TryGetValue(definitionId, out var tag) ||
                tag.Namespace != moba_luban.MobaTagNamespace.GameplayState ||
                tag.Status != moba_luban.MobaTagStatus.Active)
                return false;
            name = tag.Name;
            return true;
        }

        public bool TryGetTagDefinition(string name, out int id, out string displayName, out int parentId)
        {
            foreach (var row in _tags.Values)
                if (string.Equals(row.Name, name, StringComparison.Ordinal))
                {
                    id = row.Id;
                    displayName = row.DisplayName;
                    parentId = row.ParentId;
                    return true;
                }
            id = 0;
            displayName = string.Empty;
            parentId = 0;
            return false;
        }

        public string[] GetDebugResultFlags(MobaTagResultFlags mask) =>
            _flagRows.Values.Where(row => ((ulong)mask & (1UL << row.BitIndex)) != 0)
                .OrderBy(row => row.BitIndex).Select(row => row.Key).ToArray();

        public MobaTagDebugGate[] GetDebugGates(MobaTagResultFlags mask) =>
            _gateRows.OrderBy(row => row.Id).Select(row => new MobaTagDebugGate
            {
                Id = (int)row.Id,
                Key = row.Id.ToString(),
                Name = row.Name,
                Blocked = (mask & _behaviorGates[row.Id]) != 0,
                BlockingFlags = row.BlockedFlagIds
                    .Where(id => (mask & _flagsById[id]) != 0)
                    .Select(id => _flagRows[id].Key).ToArray()
            }).ToArray();

        public MobaTagDebugRule[] GetDebugRules(GameplayTagContainer tags,
            IReadOnlyList<MobaTagDebugSource> sources)
        {
            var result = new List<MobaTagDebugRule>(_mappingRows.Count);
            foreach (var mapping in _mappingRows.OrderBy(row => row.Id))
            {
                var group = _groups[mapping.RuleGroupId];
                var nodes = new List<MobaTagDebugRuleNode>();
                AppendDebugNode(group.RootNodeId, 0, tags, sources, nodes);
                result.Add(new MobaTagDebugRule
                {
                    MappingId = mapping.Id,
                    GroupId = group.Id,
                    GroupName = group.Name,
                    ResultFlag = _flagRows[mapping.FlagId].Key,
                    Matches = _queries[group.Id].Matches(tags),
                    Nodes = nodes.ToArray()
                });
            }
            return result.ToArray();
        }

        private bool AppendDebugNode(int nodeId, int depth, GameplayTagContainer tags,
            IReadOnlyList<MobaTagDebugSource> sources, List<MobaTagDebugRuleNode> output)
        {
            var row = _nodes[nodeId];
            var view = new MobaTagDebugRuleNode
            {
                NodeId = row.Id,
                Depth = depth,
                Operator = row.Operator.ToString(),
                TagId = row.TagId,
                TagName = row.TagId > 0 ? _tags[row.TagId].Name : string.Empty,
                Exact = row.Exact,
                RefGroupId = row.RefGroupId
            };
            output.Add(view);
            if (row.Operator == moba_luban.TagRuleOperator.Tag)
            {
                MobaGameplayTagCatalog.TryGet(view.TagName, out var tag);
                view.Matches = tags != null && (row.Exact ? tags.HasTagExact(tag) : tags.HasTag(tag));
                view.SourceIndices = sources.Where(source => source.IsEffective &&
                    source.GrantedTagIds.Any(id => _tags.TryGetValue(id, out var granted) &&
                        (string.Equals(granted.Name, view.TagName, StringComparison.Ordinal) ||
                         !row.Exact && granted.Name.StartsWith(view.TagName + ".", StringComparison.Ordinal))))
                    .Select(source => source.Index).ToArray();
                return view.Matches;
            }
            if (row.Operator == moba_luban.TagRuleOperator.GroupRef)
            {
                view.Matches = AppendDebugNode(_groups[row.RefGroupId].RootNodeId, depth + 1, tags, sources, output);
                return view.Matches;
            }
            var childResults = new List<bool>();
            if (_children.TryGetValue(row.Id, out var children))
                foreach (var child in children)
                    childResults.Add(AppendDebugNode(child.Id, depth + 1, tags, sources, output));
            view.Matches = row.Operator == moba_luban.TagRuleOperator.All
                ? childResults.All(value => value)
                : row.Operator == moba_luban.TagRuleOperator.Any
                    ? childResults.Any(value => value)
                    : childResults.Count == 1 && !childResults[0];
            return view.Matches;
        }

        public void RestoreTemplate(JObject row)
        {
            RestoreExecutionMask(row);
            var fields = new[] { "ActivationRequired", "ActivationBlocked", "Application", "RemovalRequired",
                                 "RemovalBlocked", "OngoingRequired", "OngoingBlocked", "Removal" };
            foreach (var field in fields)
            {
                var ids = row[field + "TagIds"] as JArray ?? new JArray();
                var names = new JArray();
                var seen = new HashSet<int>();
                foreach (var token in ids)
                {
                    var id = token.Value<int>();
                    if (!seen.Add(id) || !_tags.TryGetValue(id, out var tag) ||
                        tag.Namespace == moba_luban.MobaTagNamespace.SkillSelection ||
                        tag.Status != moba_luban.MobaTagStatus.Active ||
                        (field == "Application" || field == "Removal") && tag.Kind != moba_luban.MobaTagKind.Fact)
                        throw new InvalidOperationException($"Invalid template tag ID: {id}");
                    names.Add(tag.Name);
                }
                row.Remove(field + "TagIds");
                row[field + "Tags"] = names;
            }
            foreach (var field in new[] { "Activation", "Removal", "Ongoing" })
            {
                var source = field + "RuleGroupId";
                var groupId = row.Value<int>(source);
                row.Remove(source);
                if (groupId > 0)
                    row[field + "Condition"] = JObject.FromObject(_conditions[groupId]);
            }
        }

        public void RestoreSkill(JObject row) => RestoreExecutionMask(row);

        private void RestoreExecutionMask(JObject row)
        {
            var blockedFlagIds = row["ExecutionBlockedFlagIds"] as JArray ?? new JArray();
            var blockedMask = MobaTagResultFlags.None;
            foreach (var token in blockedFlagIds)
            {
                var id = token.Value<int>();
                if (!_flagsById.TryGetValue(id, out var flag) || (blockedMask & flag) != 0)
                    throw new InvalidOperationException($"Invalid execution blocked result flag ID: {id}");
                blockedMask |= flag;
            }
            row.Remove("ExecutionBlockedFlagIds");
            row["ExecutionBlockedResultMask"] = (ulong)blockedMask;
        }

        private TagConditionDTO CompileGroup(int id, HashSet<int> stack, int depth, ref int count, HashSet<int> reachable)
        {
            if (!_groups.TryGetValue(id, out var group) || !stack.Add(id))
                throw new InvalidOperationException($"Missing or cyclic tag rule group: {id}");
            if (!_nodes.TryGetValue(group.RootNodeId, out var root) || root.GroupId != id || root.ParentId != 0 ||
                _nodes.Values.Count(node => node.GroupId == id && node.ParentId == 0) != 1)
                throw new InvalidOperationException($"Invalid tag rule root: {id}");
            var result = CompileNode(root, stack, depth, ref count, reachable);
            stack.Remove(id);
            return result;
        }

        private TagConditionDTO CompileNode(moba_luban.DRTagRuleNodes node, HashSet<int> stack,
            int depth, ref int count, HashSet<int> reachable)
        {
            if (depth >= 16 || ++count > 64)
                throw new InvalidOperationException("Tag rule exceeds depth or node limit.");
            reachable.Add(node.Id);
            var children = _nodes.Values.Where(child => child.GroupId == node.GroupId && child.ParentId == node.Id)
                .OrderBy(child => child.Order).ToArray();
            if (children.Select(child => child.Order).Distinct().Count() != children.Length)
                throw new InvalidOperationException($"Duplicate tag rule sibling order: {node.Id}");
            if (node.Operator == moba_luban.TagRuleOperator.Tag)
            {
                if (children.Length > 0 || node.RefGroupId != 0 || !_tags.TryGetValue(node.TagId, out var tag) ||
                    tag.Namespace == moba_luban.MobaTagNamespace.SkillSelection ||
                    tag.Status != moba_luban.MobaTagStatus.Active ||
                    tag.Kind == moba_luban.MobaTagKind.Category && node.Exact)
                    throw new InvalidOperationException($"Invalid tag rule leaf: {node.Id}");
                return new TagConditionDTO { Operator = "Tag", Tag = tag.Name, Exact = node.Exact };
            }
            if (node.Operator == moba_luban.TagRuleOperator.GroupRef)
            {
                if (children.Length > 0 || node.TagId != 0 || node.Exact)
                    throw new InvalidOperationException($"Invalid tag group reference: {node.Id}");
                return CompileGroup(node.RefGroupId, stack, depth + 1, ref count, reachable);
            }
            if (node.TagId != 0 || node.RefGroupId != 0 || node.Exact || children.Length == 0 ||
                node.Operator == moba_luban.TagRuleOperator.Not && children.Length != 1 ||
                node.Operator != moba_luban.TagRuleOperator.All && node.Operator != moba_luban.TagRuleOperator.Any &&
                node.Operator != moba_luban.TagRuleOperator.Not)
                throw new InvalidOperationException($"Invalid compound tag rule: {node.Id}");
            var compiled = new TagConditionDTO[children.Length];
            for (var i = 0; i < children.Length; i++)
                compiled[i] = CompileNode(children[i], stack, depth + 1, ref count, reachable);
            return new TagConditionDTO { Operator = node.Operator.ToString(), Children = compiled };
        }

        private static ByteBuf Load(ITextAssetLoader assets, string name)
        {
            if (assets == null || !assets.TryLoadBytes("luban/moba_bytes/" + name + ".bytes", out var bytes) || bytes == null || bytes.Length == 0)
                throw new InvalidOperationException($"Missing tag workbook table: {name}");
            return ByteBuf.Wrap(bytes);
        }
    }
}
