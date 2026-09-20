using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AbilityKit.BattleFlow
{
    /// <summary>Semantic sections shared by scene and case assets.</summary>
    public sealed class BattleFlowSections
    {
        private List<BattleBlock> _settings = new List<BattleBlock>();
        private List<BattleBlock> _setup = new List<BattleBlock>();
        private List<BattleBlock> _timeline = new List<BattleBlock>();
        private List<BattleBlock> _assertions = new List<BattleBlock>();

        /// <summary>Case-wide execution and deterministic settings.</summary>
        public List<BattleBlock> Settings { get => _settings; set => _settings = value ?? new List<BattleBlock>(); }
        /// <summary>Static world and actor construction.</summary>
        public List<BattleBlock> Setup { get => _setup; set => _setup = value ?? new List<BattleBlock>(); }
        /// <summary>Timestamped actions and commands.</summary>
        public List<BattleBlock> Timeline { get => _timeline; set => _timeline = value ?? new List<BattleBlock>(); }
        /// <summary>Post-run project assertions.</summary>
        public List<BattleBlock> Assertions { get => _assertions; set => _assertions = value ?? new List<BattleBlock>(); }

        /// <summary>Whether any semantic section contains a block.</summary>
        public bool HasBlocks =>
            Settings.Count != 0 || Setup.Count != 0 || Timeline.Count != 0 || Assertions.Count != 0;

        /// <summary>Returns blocks in canonical compilation order.</summary>
        public List<BattleBlock> ToOrderedList()
        {
            var result = new List<BattleBlock>(
                Settings.Count + Setup.Count + Timeline.Count + Assertions.Count);
            result.AddRange(Settings);
            result.AddRange(Setup);
            result.AddRange(Timeline);
            result.AddRange(Assertions);
            return result;
        }

        /// <summary>Adds a block to the section declared by the block type.</summary>
        public void Add(BattleBlock block)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));
            switch (block.Section)
            {
                case BattleBlockSection.Settings:
                    Settings.Add(block);
                    break;
                case BattleBlockSection.Timeline:
                    Timeline.Add(block);
                    break;
                case BattleBlockSection.Assertion:
                    Assertions.Add(block);
                    break;
                default:
                    Setup.Add(block);
                    break;
            }
        }

        /// <summary>Classifies a flat legacy block list into semantic sections.</summary>
        public static BattleFlowSections FromBlocks(IEnumerable<BattleBlock> blocks)
        {
            if (blocks == null) throw new ArgumentNullException(nameof(blocks));
            var sections = new BattleFlowSections();
            foreach (var block in blocks)
                if (block != null) sections.Add(block);
            return sections;
        }
    }

    /// <summary>Expands author-facing blocks into canonical semantic sections.</summary>
    public static class BattleFlowAuthoringExpander
    {
        /// <summary>Expands author intents and composite macros into internal blocks.</summary>
        public static BattleFlowSections Expand(IEnumerable<BattleBlock> authoring)
        {
            if (authoring == null) throw new ArgumentNullException(nameof(authoring));
            var sections = new BattleFlowSections();
            foreach (var block in authoring) ExpandBlock(block, sections, 0);
            return sections;
        }

        /// <summary>Whether a block is valid at the root of an authoring document.</summary>
        public static bool IsAuthoringRoot(BattleBlock block) =>
            block is BattleAuthorBlock || block is BattleCompositeBlock;

        private static void ExpandBlock(BattleBlock block, BattleFlowSections sections, int depth)
        {
            if (block == null) throw new InvalidDataException("authoring contains a null block");
            if (depth > 64) throw new InvalidDataException("authoring expansion exceeded 64 levels");
            if (block is BattleAuthorBlock authorBlock)
            {
                var expanded = authorBlock.Expand()
                    ?? throw new InvalidDataException($"{block.GetType().Name} returned a null expansion");
                foreach (var child in expanded) ExpandBlock(child, sections, depth + 1);
                return;
            }
            if (block is BattleCompositeBlock composite)
            {
                foreach (var child in composite.Children) ExpandBlock(child, sections, depth + 1);
                return;
            }
            sections.Add(block);
        }
    }

    /// <summary>Named execution defaults selected by authors without adding low-level settings blocks.</summary>
    public sealed class BattleExecutionProfile
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int TickRate { get; set; } = 30;
        public int MaxDurationMs { get; set; } = 30_000;
        public int SettleDurationMs { get; set; } = 500;
        public string EndCondition { get; set; } = AbilityKit.Scenario.TestEndConditionKinds.TimelineComplete;
        public int DurationMs { get; set; }

        public ExecutionSettingsBlock CreateBlock() => new ExecutionSettingsBlock
        {
            TickRate = TickRate,
            MaxDurationMs = MaxDurationMs,
            SettleDurationMs = SettleDurationMs,
            EndCondition = EndCondition,
            DurationMs = DurationMs,
        };
    }

    /// <summary>Registry of execution profiles available to documents and editor UI.</summary>
    public static class BattleExecutionProfileCatalog
    {
        private static readonly Dictionary<string, BattleExecutionProfile> ProfilesById =
            new Dictionary<string, BattleExecutionProfile>(StringComparer.OrdinalIgnoreCase);

        static BattleExecutionProfileCatalog()
        {
            Register(new BattleExecutionProfile { Id = "default", DisplayName = "标准" });
            Register(new BattleExecutionProfile
            {
                Id = "extended",
                DisplayName = "长时间战斗",
                MaxDurationMs = 120_000,
                SettleDurationMs = 1_000,
            });
        }

        public static IReadOnlyCollection<BattleExecutionProfile> Profiles => ProfilesById.Values;

        public static void Register(BattleExecutionProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (string.IsNullOrWhiteSpace(profile.Id)) throw new ArgumentException("Profile id is required.", nameof(profile));
            ProfilesById[profile.Id] = profile;
        }

        public static bool TryGet(string id, out BattleExecutionProfile profile) =>
            ProfilesById.TryGetValue(string.IsNullOrWhiteSpace(id) ? "default" : id, out profile!);

        public static BattleExecutionProfile Resolve(string id)
        {
            if (TryGet(id, out var profile)) return profile;
            throw new InvalidDataException($"Unknown execution profile '{id}'.");
        }
    }

    /// <summary>
    /// Reusable static battle setup. Scene assets contain setup only; execution and timed behavior belong to cases.
    /// </summary>
    public sealed class BattleSceneDocument
    {
        private BattleFlowSections _sections = new BattleFlowSections();
        private List<BattleBlock> _blocks = new List<BattleBlock>();

        /// <summary>Scene asset schema version.</summary>
        public int SchemaVersion { get; set; } = 1;
        /// <summary>Stable scene identifier.</summary>
        public string SceneId { get; set; } = string.Empty;
        /// <summary>Optional author-facing name.</summary>
        public string DisplayName { get; set; } = string.Empty;
        /// <summary>Structured scene content.</summary>
        public BattleFlowSections Sections { get => _sections; set => _sections = value ?? new BattleFlowSections(); }

        /// <summary>Legacy flat input accepted for programmatic migration.</summary>
        public List<BattleBlock> Blocks { get => _blocks; set => _blocks = value ?? new List<BattleBlock>(); }

        /// <summary>Returns structured content, or legacy blocks when no sections are present.</summary>
        public IReadOnlyList<BattleBlock> GetOrderedBlocks() =>
            Sections.HasBlocks ? Sections.ToOrderedList() : Blocks;

        /// <summary>Json.NET compatibility hook for legacy flat documents.</summary>
        public bool ShouldSerializeBlocks() => Blocks.Count != 0 && !Sections.HasBlocks;
    }

    /// <summary>
    /// Battle case. It may reference a reusable scene or remain self-contained for legacy flows.
    /// </summary>
    public sealed class BattleFlowDocument
    {
        private List<string> _tags = new List<string>();
        private List<BattleBlock> _authoring = new List<BattleBlock>();
        private BattleFlowSections _sections = new BattleFlowSections();
        private List<BattleBlock> _blocks = new List<BattleBlock>();

        /// <summary>Case asset schema version.</summary>
        public int SchemaVersion { get; set; } = 3;
        /// <summary>Stable case identifier.</summary>
        public string CaseId { get; set; } = string.Empty;
        /// <summary>Optional author-facing name.</summary>
        public string DisplayName { get; set; } = string.Empty;
        /// <summary>Optional reusable scene path or identifier.</summary>
        public string ScenarioRef { get; set; } = string.Empty;
        /// <summary>Batch and CI filter tags.</summary>
        public List<string> Tags { get => _tags; set => _tags = value ?? new List<string>(); }
        /// <summary>Author-facing intent blocks. Mutually exclusive with sections and legacy blocks.</summary>
        public List<BattleBlock> Authoring { get => _authoring; set => _authoring = value ?? new List<BattleBlock>(); }
        /// <summary>Named execution defaults; explicit settings blocks override this profile.</summary>
        public string ExecutionProfileId { get; set; } = "default";
        /// <summary>Structured case content.</summary>
        public BattleFlowSections Sections { get => _sections; set => _sections = value ?? new BattleFlowSections(); }

        /// <summary>Legacy flat block list retained for existing files and source compatibility.</summary>
        public List<BattleBlock> Blocks { get => _blocks; set => _blocks = value ?? new List<BattleBlock>(); }

        /// <summary>Returns structured content, or legacy blocks when no sections are present.</summary>
        public IReadOnlyList<BattleBlock> GetOrderedBlocks() =>
            Authoring.Count != 0
                ? BattleFlowAuthoringExpander.Expand(Authoring).ToOrderedList()
                : Sections.HasBlocks ? Sections.ToOrderedList() : Blocks;

        /// <summary>Json.NET compatibility hook for legacy flat documents.</summary>
        public bool ShouldSerializeBlocks() => Blocks.Count != 0 && !Sections.HasBlocks;

        public bool ShouldSerializeAuthoring() => Authoring.Count != 0;
    }

    /// <summary>Validates asset ownership and semantic section placement before compilation.</summary>
    public static class BattleFlowDocumentValidator
    {
        /// <summary>Validates a reusable scene asset.</summary>
        public static IReadOnlyList<string> Validate(BattleSceneDocument scene)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(scene.SceneId)) errors.Add("sceneId is required");
            ValidateStorageShape(scene.Sections, scene.Blocks, errors);

            foreach (var block in scene.GetOrderedBlocks())
                ValidateBlockTree(block, BattleBlockSection.Setup, "scene setup", errors);
            return errors.Distinct(StringComparer.Ordinal).ToArray();
        }

        /// <summary>Validates a case asset and its semantic placement.</summary>
        public static IReadOnlyList<string> Validate(BattleFlowDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(document.CaseId)) errors.Add("caseId is required");
            ValidateStorageShape(document.Sections, document.Blocks, errors);
            if (document.Authoring.Count != 0 && (document.Sections.HasBlocks || document.Blocks.Count != 0))
                errors.Add("document cannot contain authoring together with semantic sections or legacy blocks");
            if (!BattleExecutionProfileCatalog.TryGet(document.ExecutionProfileId, out _))
                errors.Add($"unknown execution profile '{document.ExecutionProfileId}'");

            BattleFlowSections effectiveSections;
            if (document.Authoring.Count != 0)
            {
                foreach (var block in document.Authoring)
                {
                    if (block == null || !BattleFlowAuthoringExpander.IsAuthoringRoot(block))
                        errors.Add("authoring roots must be author blocks or composite templates");
                    else
                        ValidateAuthoringTree(block, errors);
                }
                try
                {
                    effectiveSections = BattleFlowAuthoringExpander.Expand(document.Authoring);
                }
                catch (Exception ex)
                {
                    errors.Add("authoring expansion failed: " + ex.Message);
                    effectiveSections = new BattleFlowSections();
                }
            }
            else
            {
                effectiveSections = document.Sections.HasBlocks
                    ? document.Sections
                    : BattleFlowSections.FromBlocks(document.Blocks);
            }

            if (document.Authoring.Count != 0 || document.Sections.HasBlocks)
            {
                ValidateSection(effectiveSections.Settings, BattleBlockSection.Settings, "settings", errors);
                ValidateSection(effectiveSections.Setup, BattleBlockSection.Setup, "setup", errors);
                ValidateSection(effectiveSections.Timeline, BattleBlockSection.Timeline, "timeline", errors);
                ValidateSection(effectiveSections.Assertions, BattleBlockSection.Assertion, "assertions", errors);
            }
            else if (!string.IsNullOrWhiteSpace(document.ScenarioRef))
            {
                foreach (var block in document.Blocks)
                    if (ContainsSection(block, BattleBlockSection.Setup))
                        errors.Add("a case with scenarioRef cannot contain setup blocks");
            }

            if (!string.IsNullOrWhiteSpace(document.ScenarioRef) && effectiveSections.Setup.Count != 0)
                errors.Add("a case with scenarioRef cannot contain setup blocks");

            var allBlocks = effectiveSections.ToOrderedList();
            if (CountBlocks<ExecutionSettingsBlock>(allBlocks) > 1)
                errors.Add("execution settings may be declared only once per case");
            if (CountBlocks<SetScenarioSeedBlock>(allBlocks) > 1)
                errors.Add("scenario seed may be declared only once per case");
            return errors.Distinct(StringComparer.Ordinal).ToArray();
        }

        /// <summary>Throws when a reusable scene asset is invalid.</summary>
        public static void ThrowIfInvalid(BattleSceneDocument scene)
        {
            var errors = Validate(scene);
            if (errors.Count != 0) throw new InvalidDataException(string.Join("; ", errors));
        }

        /// <summary>Throws when a case asset is invalid.</summary>
        public static void ThrowIfInvalid(BattleFlowDocument document)
        {
            var errors = Validate(document);
            if (errors.Count != 0) throw new InvalidDataException(string.Join("; ", errors));
        }

        private static void ValidateStorageShape(
            BattleFlowSections sections,
            IReadOnlyCollection<BattleBlock> legacyBlocks,
            ICollection<string> errors)
        {
            if (sections == null) errors.Add("sections is required");
            else if (sections.HasBlocks && legacyBlocks.Count != 0)
                errors.Add("document cannot contain both semantic sections and legacy blocks");
        }

        private static void ValidateSection(
            IEnumerable<BattleBlock> blocks,
            BattleBlockSection expected,
            string name,
            ICollection<string> errors)
        {
            foreach (var block in blocks) ValidateBlockTree(block, expected, name, errors);
        }

        private static void ValidateBlockTree(
            BattleBlock block,
            BattleBlockSection expected,
            string name,
            ICollection<string> errors)
        {
            if (block == null)
            {
                errors.Add($"{name} contains a null block");
                return;
            }

            if (block is BattleCompositeBlock composite)
            {
                foreach (var child in composite.Children)
                    ValidateBlockTree(child, expected, name, errors);
                return;
            }

            if (block.Section != expected)
                errors.Add($"{block.GetType().Name} belongs to {block.Section}, not {name}");
        }

        private static bool ContainsSection(BattleBlock block, BattleBlockSection section)
        {
            if (block is BattleCompositeBlock composite)
                return composite.Children.Any(child => child != null && ContainsSection(child, section));
            return block.Section == section;
        }

        private static void ValidateAuthoringTree(BattleBlock block, ICollection<string> errors)
        {
            if (block is BattleAuthorBlock authorBlock)
            {
                foreach (var error in authorBlock.Validate())
                    errors.Add($"{block.GetType().Name}: {error}");
                return;
            }
            if (block is BattleCompositeBlock composite)
                foreach (var child in composite.Children)
                    if (child != null) ValidateAuthoringTree(child, errors);
        }

        private static int CountBlocks<T>(IEnumerable<BattleBlock> blocks) where T : BattleBlock
        {
            var count = 0;
            foreach (var block in blocks)
            {
                if (block is T) count++;
                if (block is BattleCompositeBlock composite)
                    count += CountBlocks<T>(composite.Children);
            }
            return count;
        }
    }
}
