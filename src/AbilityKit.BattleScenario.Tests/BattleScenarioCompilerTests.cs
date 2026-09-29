using System.Collections.Generic;
using AbilityKit.BattleScenario;
using AbilityKit.Scenario;
using Xunit;

namespace AbilityKit.BattleScenario.Tests;

/// <summary>验证积木模型：原子积木编译到 IR、复合积木展平、积木库注册（粒度项目可选）。</summary>
public sealed class BattleScenarioCompilerTests
{
    [Fact]
    public void AtomicBlocks_CompileToNeutralScenario()
    {
        var scenario = BattleScenarioCompiler.Compile("case-1", new BattleBlock[]
        {
            new SetEnvironmentBlock { Id = "env", ProfileId = "jungle-camp" },
            new SpawnActorBlock { Id = "caster", Alias = "caster", HeroId = 1001, Position = new TestVector3(-15, 0, 0) },
            new SpawnActorBlock { Id = "target", Alias = "target", TeamId = 2, Position = new TestVector3(-12, 0, 0) },
            new TimelineStepBlock { Id = "cast", AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1 },
        });

        Assert.Equal("case-1", scenario.CaseId);
        Assert.Equal("jungle-camp", scenario.EnvironmentProfileId);
        Assert.Equal(2, scenario.Actors.Count);
        Assert.Single(scenario.Timeline);
        Assert.Equal("cast_skill", scenario.Timeline[0].Action);
        Assert.Empty(TestScenarioValidator.Validate(scenario));
    }

    [Fact]
    public void ExecutionSettings_CompileToExplicitPolicy()
    {
        var scenario = BattleScenarioCompiler.Compile("execution", new BattleBlock[]
        {
            new ExecutionSettingsBlock
            {
                TickRate = 60,
                MaxDurationMs = 5_000,
                SettleDurationMs = 250,
                EndCondition = TestEndConditionKinds.Duration,
                DurationMs = 4_000,
            },
        });

        var execution = Assert.IsType<TestExecutionSpec>(scenario.Execution);
        Assert.Equal(60, execution.TickRate);
        Assert.Equal(5_000, execution.MaxDurationMs);
        Assert.Equal(250, execution.SettleDurationMs);
        Assert.Equal(TestEndConditionKinds.Duration, execution.EndCondition.Kind);
        Assert.Equal(4_000, execution.EndCondition.DurationMs);
        Assert.Empty(TestScenarioValidator.Validate(scenario));
    }

    [Fact]
    public void Dsl_ParsesExecutionPolicyAndRejectsUnknownVerb()
    {
        var blocks = BattleScenarioDslParser.Parse(
            "execution tick=60 max=5000 settle=250 end=duration duration=4000");
        var scenario = BattleScenarioCompiler.Compile("execution-dsl", blocks);

        Assert.Equal(60, scenario.ResolveExecution().TickRate);
        Assert.Equal(4_000, scenario.ResolveExecution().EndCondition.DurationMs);
        Assert.Throws<ArgumentException>(() => BattleScenarioDslParser.Parse("spwan caster hero=1001"));
    }

    [Fact]
    public void DocumentDsl_ParsesSceneTagsAndSemanticSections()
    {
        var document = BattleScenarioDslParser.ParseDocument("dsl-case", @"
scene shared-duel
tag smoke skill
execution tick=60 max=2000 settle=0 end=timeline
wait 100 at=200
");

        Assert.Equal("dsl-case", document.CaseId);
        Assert.Equal("shared-duel", document.ScenarioRef);
        Assert.Equal(new[] { "smoke", "skill" }, document.Tags);
        Assert.Single(document.Sections.Settings);
        Assert.Single(document.Sections.Timeline);
        Assert.Empty(BattleScenarioDocumentValidator.Validate(document));
    }

    [Fact]
    public void CompositeBlock_FlattensChildren()
    {
        // 项目定义一个「标准野怪测试」复合积木：环境 + 目标 + 施放
        var standardJungleTest = new BattleCompositeBlock
        {
            Id = "standard-jungle-test",
            DisplayName = "标准野怪测试",
            Children = new BattleBlock[]
            {
                new SetEnvironmentBlock { ProfileId = "jungle-camp" },
                new SpawnActorBlock { Alias = "target", TeamId = 2 },
                new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target" },
            },
        };

        var scenario = BattleScenarioCompiler.Compile("case-2", new BattleBlock[]
        {
            new SpawnActorBlock { Alias = "caster", HeroId = 1001 },
            standardJungleTest,
        });

        Assert.Equal("jungle-camp", scenario.EnvironmentProfileId);
        Assert.Equal(2, scenario.Actors.Count);          // caster + target
        Assert.Single(scenario.Timeline);
    }

    [Fact]
    public void Library_RegistersAndResolvesBlocks()
    {
        var library = new BattleBlockLibrary()
            .Add(new BattleCompositeBlock { Id = "standard-jungle-test", DisplayName = "标准野怪测试" });

        Assert.True(library.TryGet("standard-jungle-test", out var block));
        Assert.Equal("标准野怪测试", block.DisplayName);
    }

    [Fact]
    public void Library_RejectsDuplicateId()
    {
        var library = new BattleBlockLibrary().Add(new BattleCompositeBlock { Id = "dup" });
        Assert.Throws<ArgumentException>(() => library.Add(new BattleCompositeBlock { Id = "dup" }));
    }

    [Fact]
    public void AssertionBlock_SetsOpaqueExpectations()
    {
        var payload = new object();
        var scenario = BattleScenarioCompiler.Compile("case-assert", new BattleBlock[]
        {
            new ObjectAssertBlock { Id = "assert", Payload = payload },
        });

        Assert.Same(payload, scenario.Expectations);
    }

    [Fact]
    public void Codec_RoundTripsBlockTree()
    {
        var doc = new BattleScenarioDocument
        {
            CaseId = "case-flow",
            Blocks = new List<BattleBlock>
            {
                new SetEnvironmentBlock { ProfileId = "jungle-camp" },
                new SpawnActorBlock { Alias = "caster", HeroId = 1001 },
                new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster" },
                new BattleCompositeBlock { Id = "macro", Children = new List<BattleBlock> { new WaitBlock { AtMs = 200 } } },
            },
        };

        var json = BattleScenarioCodec.Serialize(doc);
        var back = BattleScenarioCodec.Parse(json);

        Assert.Equal("case-flow", back.CaseId);
        Assert.Equal(4, back.Blocks.Count);
        Assert.IsType<SetEnvironmentBlock>(back.Blocks[0]);
        Assert.IsType<SpawnActorBlock>(back.Blocks[1]);
        Assert.IsType<TimelineStepBlock>(back.Blocks[2]);
        Assert.IsType<BattleCompositeBlock>(back.Blocks[3]);
    }

    [Fact]
    public void SceneBackedCase_CompilesReusableSetupAndCaseBehavior()
    {
        var scene = new BattleSceneDocument
        {
            SceneId = "duel-scene",
            Sections = new BattleScenarioSections
            {
                Setup = new List<BattleBlock>
                {
                    new SetEnvironmentBlock { ProfileId = "duel-arena" },
                    new SpawnActorBlock { Alias = "caster", HeroId = 1001 },
                    new SpawnActorBlock { Alias = "target", HeroId = 1002, TeamId = 2 },
                },
            },
        };
        var document = new BattleScenarioDocument
        {
            CaseId = "cast-case",
            ScenarioRef = "duel-scene.battlescene",
            Sections = new BattleScenarioSections
            {
                Settings = new List<BattleBlock>
                {
                    new ExecutionSettingsBlock { TickRate = 60, MaxDurationMs = 2_000 },
                },
                Timeline = new List<BattleBlock>
                {
                    new TimelineStepBlock
                    {
                        AtMs = 100,
                        Action = "cast_skill",
                        ActorAlias = "caster",
                        TargetAlias = "target",
                    },
                },
            },
        };

        var scenario = BattleScenarioCompiler.Compile(document, reference =>
        {
            Assert.Equal("duel-scene.battlescene", reference);
            return scene;
        });

        Assert.Equal("duel-arena", scenario.EnvironmentProfileId);
        Assert.Equal(2, scenario.Actors.Count);
        Assert.Single(scenario.Timeline);
        Assert.Equal(60, scenario.ResolveExecution().TickRate);
    }

    [Fact]
    public void StructuredDocument_RejectsCrossSectionBlocksAndCopiedSetup()
    {
        var misplaced = new BattleScenarioDocument
        {
            CaseId = "misplaced",
            Sections = new BattleScenarioSections
            {
                Setup = new List<BattleBlock> { new WaitBlock { AtMs = 100, DurationMs = 100 } },
            },
        };
        var copiedSetup = new BattleScenarioDocument
        {
            CaseId = "copied-setup",
            ScenarioRef = "shared-scene",
            Sections = new BattleScenarioSections
            {
                Setup = new List<BattleBlock> { new SpawnActorBlock { Alias = "duplicate" } },
            },
        };

        Assert.Contains(BattleScenarioDocumentValidator.Validate(misplaced), error => error.Contains("Timeline"));
        Assert.Contains(BattleScenarioDocumentValidator.Validate(copiedSetup), error => error.Contains("scenarioRef"));
        Assert.Throws<InvalidDataException>(() => BattleScenarioCompiler.Compile(copiedSetup, _ => new BattleSceneDocument()));
    }

    [Fact]
    public void StructuredCodec_RoundTripsSemanticSectionsWithoutLegacyBlocks()
    {
        var document = new BattleScenarioDocument
        {
            CaseId = "structured",
            DisplayName = "Structured smoke case",
            ScenarioRef = "shared-arena",
            Tags = new List<string> { "smoke", "skill" },
            Sections = new BattleScenarioSections
            {
                Settings = new List<BattleBlock> { new SetScenarioSeedBlock { Seed = 17 } },
                Timeline = new List<BattleBlock> { new WaitBlock { AtMs = 100, DurationMs = 50 } },
            },
        };

        var json = BattleScenarioCodec.Serialize(document);
        var back = BattleScenarioCodec.Parse(json);

        Assert.Equal("shared-arena", back.ScenarioRef);
        Assert.Equal("Structured smoke case", back.DisplayName);
        Assert.Equal(new[] { "smoke", "skill" }, back.Tags);
        Assert.Single(back.Sections.Settings);
        Assert.Single(back.Sections.Timeline);
        Assert.Empty(back.Blocks);
        Assert.DoesNotContain("\"blocks\"", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompileFile_ResolvesSceneRelativeToCaseFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "abilitykit-battlescenario-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var scenePath = Path.Combine(directory, "arena.battlescene");
            var casePath = Path.Combine(directory, "cast.battlescenario");
            BattleScenarioCodec.SaveScene(scenePath, new BattleSceneDocument
            {
                SceneId = "arena",
                Sections = new BattleScenarioSections
                {
                    Setup = new List<BattleBlock> { new SpawnActorBlock { Alias = "caster", HeroId = 1001 } },
                },
            });
            BattleScenarioCodec.Save(casePath, new BattleScenarioDocument
            {
                CaseId = "relative-scene",
                ScenarioRef = "arena",
                Sections = new BattleScenarioSections
                {
                    Timeline = new List<BattleBlock> { new WaitBlock { AtMs = 0, DurationMs = 100 } },
                },
            });

            var scenario = BattleScenarioCompiler.CompileFile(casePath);

            Assert.Single(scenario.Actors);
            Assert.Single(scenario.Timeline);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AuthoringBlocks_ExpandIntoCanonicalSectionsAtCompileTime()
    {
        var document = new BattleScenarioDocument
        {
            CaseId = "authoring-duel",
            Authoring = new List<BattleBlock>
            {
                new DuelSetupBlock
                {
                    EnvironmentProfileId = "arena",
                    CasterHeroId = 1001,
                    CasterAttributeTemplateId = 1001,
                    TargetHeroId = 1002,
                    TargetAttributeTemplateId = 1002,
                    TargetDistance = 4f,
                },
                new CastSkillBlock { Slot = 2, AtMs = 250 },
            },
        };

        var expanded = BattleScenarioAuthoringExpander.Expand(document.Authoring);
        var scenario = BattleScenarioCompiler.Compile(document);

        Assert.Equal(3, expanded.Setup.Count);
        Assert.Single(expanded.Timeline);
        Assert.Equal("arena", scenario.EnvironmentProfileId);
        Assert.Equal(2, scenario.Actors.Count);
        Assert.Equal(4f, scenario.Actors[1].Position!.Value.X);
        Assert.Equal(2, scenario.Timeline[0].Slot);
        Assert.Empty(BattleScenarioDocumentValidator.Validate(document));
    }

    [Fact]
    public void AuthoringDocument_RoundTripsWithoutPersistingExpandedAtomicBlocks()
    {
        var document = new BattleScenarioDocument
        {
            CaseId = "authoring-codec",
            ExecutionProfileId = "extended",
            Authoring = new List<BattleBlock>
            {
                new CastSkillBlock { CasterAlias = "hero", TargetAlias = "dummy", Slot = 3 },
            },
        };

        var json = BattleScenarioCodec.Serialize(document);
        var back = BattleScenarioCodec.Parse(json);

        var intent = Assert.IsType<CastSkillBlock>(Assert.Single(back.Authoring));
        Assert.Equal(3, intent.Slot);
        Assert.Equal("extended", back.ExecutionProfileId);
        Assert.DoesNotContain("timelineStepBlock", json, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(back.Blocks);
        Assert.False(back.Sections.HasBlocks);
    }

    [Fact]
    public void SceneBackedAuthoring_RejectsSetupIntentButAllowsActionIntent()
    {
        var actionCase = new BattleScenarioDocument
        {
            CaseId = "scene-action",
            ScenarioRef = "duel",
            Authoring = new List<BattleBlock> { new CastSkillBlock() },
        };
        var invalidSetupCase = new BattleScenarioDocument
        {
            CaseId = "scene-duplicate-setup",
            ScenarioRef = "duel",
            Authoring = new List<BattleBlock> { new DuelSetupBlock() },
        };
        var invalidParameters = new BattleScenarioDocument
        {
            CaseId = "invalid-author-parameters",
            Authoring = new List<BattleBlock> { new CastSkillBlock { AtMs = -1 } },
        };
        var scene = new BattleSceneDocument
        {
            SceneId = "duel",
            Sections = new BattleScenarioSections
            {
                Setup = new List<BattleBlock>
                {
                    new SpawnActorBlock { Alias = "caster" },
                    new SpawnActorBlock { Alias = "target", TeamId = 2 },
                },
            },
        };

        var scenario = BattleScenarioCompiler.Compile(actionCase, _ => scene);

        Assert.Single(scenario.Timeline);
        Assert.Contains(BattleScenarioDocumentValidator.Validate(invalidSetupCase),
            error => error.Contains("scenarioRef"));
        Assert.Contains(BattleScenarioDocumentValidator.Validate(invalidParameters),
            error => error.Contains("cannot be negative"));
    }

    [Fact]
    public void ExecutionProfile_AppliesDefaultsAndExplicitSettingsOverrideThem()
    {
        const string profileId = "tests-fast";
        BattleExecutionProfileCatalog.Register(new BattleExecutionProfile
        {
            Id = profileId,
            TickRate = 20,
            MaxDurationMs = 2_000,
            SettleDurationMs = 100,
        });
        var profiled = BattleScenarioCompiler.Compile(new BattleScenarioDocument
        {
            CaseId = "profiled",
            ExecutionProfileId = profileId,
        });
        var overridden = BattleScenarioCompiler.Compile(new BattleScenarioDocument
        {
            CaseId = "profile-override",
            ExecutionProfileId = profileId,
            Sections = new BattleScenarioSections
            {
                Settings = new List<BattleBlock>
                {
                    new ExecutionSettingsBlock { TickRate = 60, MaxDurationMs = 5_000 },
                },
            },
        });

        Assert.Equal(20, profiled.ResolveExecution().TickRate);
        Assert.Equal(2_000, profiled.ResolveExecution().MaxDurationMs);
        Assert.Equal(60, overridden.ResolveExecution().TickRate);
        Assert.Equal(5_000, overridden.ResolveExecution().MaxDurationMs);
    }

    /// <summary>模拟项目自定义的断言积木：把 opaque 断言对象塞进 Expectations。</summary>
    private sealed class ObjectAssertBlock : BattleAtomicBlock
    {
        public object? Payload { get; set; }

        public override void Compile(BattleScenarioBuilder builder) => builder.SetExpectations(Payload);
    }
}
