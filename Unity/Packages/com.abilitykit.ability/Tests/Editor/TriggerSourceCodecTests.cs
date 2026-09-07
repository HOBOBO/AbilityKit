#if UNITY_EDITOR
using System;
using System.IO;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace AbilityKit.Ability.Editor.Tests
{
    /// <summary>
    /// Source codec 抽象的验收：注册表按扩展名解析、JSON codec 往返、
    /// 哈希与序列化文本无关（换格式不产生假冲突基线）、自定义 codec 可插拔。
    /// </summary>
    public sealed class TriggerSourceCodecTests
    {
        private string _tempDirectory;

        [SetUp]
        public void SetUp()
        {
            TriggerSourceCodecs.ResetToDefaults();
            _tempDirectory = Path.Combine(Path.GetTempPath(), "TriggerSourceCodecTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            TriggerSourceCodecs.ResetToDefaults();
            if (Directory.Exists(_tempDirectory)) Directory.Delete(_tempDirectory, true);
        }

        [Test]
        public void Registry_ResolvesDefaultJsonCodecByExtension()
        {
            var moduleCodecFound = TriggerSourceCodecs.TryResolveModule(
                Path.Combine(_tempDirectory, "module.json"),
                out var moduleCodec);
            var templateCodecFound = TriggerSourceCodecs.TryResolveTemplate(
                Path.Combine(_tempDirectory, "template.json"),
                out var templateCodec);

            Assert.That(moduleCodecFound, Is.True);
            Assert.That(moduleCodec.FormatId, Is.EqualTo("json"));
            Assert.That(templateCodecFound, Is.True);
            Assert.That(templateCodec.FormatId, Is.EqualTo("json"));
            Assert.That(TriggerSourceCodecs.TryResolveModule(
                Path.Combine(_tempDirectory, "module.yaml"), out _), Is.False);
            Assert.That(TriggerSourceCodecs.ModuleDefault.FileExtension, Is.EqualTo("json"));
            Assert.That(TriggerSourceCodecs.TemplateDefault.FileExtension, Is.EqualTo("json"));
        }

        [Test]
        public void ReadFile_RejectsUnregisteredExtension()
        {
            var path = Path.Combine(_tempDirectory, "module.xyz");
            File.WriteAllText(path, "{}");

            Assert.Throws<InvalidDataException>(() => TriggerAuthoringSourceCodec.ReadFile(path));
        }

        [Test]
        public void ModuleJsonCodec_RoundTripsDocument()
        {
            var codec = TriggerSourceCodecs.ModuleDefault;
            var document = BuildModuleDocument();

            var restored = codec.Deserialize(codec.Serialize(document));

            Assert.That(
                TriggerSourceCanonical.ComputeContentHash(restored),
                Is.EqualTo(TriggerSourceCanonical.ComputeContentHash(document)));
            Assert.That(restored.Module.ModuleId, Is.EqualTo("module.codec_fixture"));
            Assert.That(restored.Module.Kind, Is.EqualTo(TriggerModuleKind.Buff));
            Assert.That(restored.Module.Triggers[0].Id, Is.EqualTo(7));
            Assert.That(restored.Module.Triggers[0].GroupPath, Is.EqualTo("Combat/Reactions"));
            Assert.That(restored.Module.Triggers[0].Tags, Is.EquivalentTo(new[] { "combat", "reaction" }));
            Assert.That(restored.Module.Triggers[0].Actions.Children[0].Arguments[0].Value.StringValue,
                Is.EqualTo("hello"));
            Assert.That(restored.Module.Triggers[0].Condition.GroupReference, Is.EqualTo("condition_group_1"));
        }

        [Test]
        public void TemplateJsonCodec_RoundTripsDocument()
        {
            var codec = TriggerSourceCodecs.TemplateDefault;
            var document = BuildTemplateDocument();

            var restored = codec.Deserialize(codec.Serialize(document));

            Assert.That(
                TriggerSourceCanonical.ComputeContentHash(restored),
                Is.EqualTo(TriggerSourceCanonical.ComputeContentHash(document)));
            Assert.That(restored.Template.TemplateId, Is.EqualTo("template.codec_fixture"));
            Assert.That(restored.Template.Parameters[0].Name, Is.EqualTo("message"));
            Assert.That(restored.Template.Actions.Arguments[0].Value.Source,
                Is.EqualTo(TriggerValueSource.TemplateParameter));
        }

        [Test]
        public void JsonCodec_RejectsUnknownFieldsThroughInterface()
        {
            var text = TriggerSourceCodecs.ModuleDefault.Serialize(BuildModuleDocument())
                .Replace("\"schema\"", "\"totallyUnknownField\":\"x\",\"schema\"");

            Assert.Throws<InvalidDataException>(() => TriggerSourceCodecs.ModuleDefault.Deserialize(text));
        }

        [Test]
        public void ContentHash_IsIndependentOfSerializedText()
        {
            var codec = TriggerSourceCodecs.ModuleDefault;
            var document = BuildModuleDocument();
            var defaultText = codec.Serialize(document);

            var compact = new CompactModuleCodec();
            TriggerSourceCodecs.RegisterModule(compact);
            var compactText = compact.Serialize(document);

            Assert.That(compactText, Is.Not.EqualTo(defaultText), "测试前提：两种 codec 的文本形态必须不同");

            var viaDefault = codec.Deserialize(defaultText);
            var viaCompact = compact.Deserialize(compactText);
            Assert.That(
                TriggerSourceCanonical.ComputeContentHash(viaCompact),
                Is.EqualTo(TriggerSourceCanonical.ComputeContentHash(viaDefault)),
                "同一 DOM 经不同格式往返后基线哈希必须一致");
        }

        [Test]
        public void CustomCodec_SupportsFacadeRoundTripThroughRegistry()
        {
            TriggerSourceCodecs.RegisterModule(new CompactModuleCodec());
            var document = BuildModuleDocument();
            var path = Path.Combine(_tempDirectory, "module.trgsrc");

            TriggerAuthoringSourceCodec.WriteFileAtomic(path, document);
            var restored = TriggerAuthoringSourceCodec.ReadFile(path);

            Assert.That(
                TriggerAuthoringSourceCodec.ComputeContentHash(restored),
                Is.EqualTo(TriggerAuthoringSourceCodec.ComputeContentHash(document)));
        }

        [Test]
        public void SourceSchema_SerializesModuleAndTemplateContracts()
        {
            var module = JObject.Parse(TriggerAuthoringSourceSchema.Serialize(TriggerAuthoringSourceSchemaKind.Module));
            var template = JObject.Parse(TriggerAuthoringSourceSchema.Serialize(TriggerAuthoringSourceSchemaKind.Template));

            Assert.That((string)module["$schema"], Is.EqualTo("http://json-schema.org/draft-07/schema#"));
            Assert.That((string)module["properties"]["schema"]["const"], Is.EqualTo(TriggerAuthoringSchema.Id));
            Assert.That((string)module["properties"]["version"]["const"], Is.EqualTo(TriggerAuthoringSchema.Version));
            Assert.That(module["properties"]["module"], Is.Not.Null);
            Assert.That(module["properties"]["template"], Is.Null);
            Assert.That(
                module["definitions"]["triggerAuthoringModule"]["properties"]["triggers"]["items"]["$ref"].ToString(),
                Is.EqualTo("#/definitions/triggerDefinition"));
            Assert.That(module["definitions"]["triggerDefinition"]["properties"]["groupPath"], Is.Not.Null);
            Assert.That(module["definitions"]["triggerDefinition"]["properties"]["tags"], Is.Not.Null);

            Assert.That((string)template["properties"]["schema"]["const"], Is.EqualTo(TriggerAuthoringSchema.Id));
            Assert.That((string)template["properties"]["version"]["const"], Is.EqualTo("2.2"));
            Assert.That(template["properties"]["template"], Is.Not.Null);
            Assert.That(template["properties"]["module"], Is.Null);
            Assert.That(
                template["definitions"]["triggerAuthoringTemplate"]["properties"]["parameters"]["items"]["$ref"].ToString(),
                Is.EqualTo("#/definitions/templateParameter"));
        }

        [Test]
        public void SourceSchema_ExportsBothSchemasAtomically()
        {
            var result = TriggerAuthoringSourceSchema.ExportAll(_tempDirectory);

            Assert.That(result.TotalCount, Is.EqualTo(2));
            Assert.That(File.Exists(Path.Combine(_tempDirectory, TriggerAuthoringSourceSchema.ModuleSchemaFileName)), Is.True);
            Assert.That(File.Exists(Path.Combine(_tempDirectory, TriggerAuthoringSourceSchema.TemplateSchemaFileName)), Is.True);
            AssertNoAtomicArtifacts();

            var second = TriggerAuthoringSourceSchema.ExportAll(_tempDirectory);
            Assert.That(second.WrittenPaths, Is.Empty);
            Assert.That(second.UnchangedPaths.Count, Is.EqualTo(2));
            AssertNoAtomicArtifacts();
        }

        private static TriggerAuthoringSourceDocument BuildModuleDocument()
        {
            return new TriggerAuthoringSourceDocument
            {
                Metadata = new TriggerAuthoringSourceMetadata { Author = "codec-tests", Description = "fixture" },
                Module = new TriggerAuthoringModuleData
                {
                    ModuleId = "module.codec_fixture",
                    DisplayName = "Codec Fixture",
                    Kind = TriggerModuleKind.Buff,
                    Blackboard =
                    {
                        new TriggerBlackboardVariableData { Key = "stacks", Type = TriggerValueType.Integer }
                    },
                    ConditionGroups =
                    {
                        new TriggerNodeGroupData
                        {
                            Id = "condition_group_1",
                            DisplayName = "Low Health",
                            Root = new TriggerNodeData
                            {
                                Kind = TriggerNodeKind.Condition,
                                Type = "health_percent",
                                Arguments =
                                {
                                    new TriggerArgumentData
                                    {
                                        Name = "threshold",
                                        Value = new TriggerValueRefData
                                        {
                                            Source = TriggerValueSource.Constant,
                                            Type = TriggerValueType.Number,
                                            NumberValue = 0.35
                                        }
                                    }
                                }
                            }
                        }
                    },
                    Triggers =
                    {
                        new TriggerDefinitionData
                        {
                            Id = 7,
                            Name = "Vengeance",
                            GroupPath = "Combat/Reactions",
                            Tags = { "combat", "reaction" },
                            Event = "combat.damage_taken",
                            Phase = "early",
                            Priority = 20,
                            Condition = new TriggerNodeData
                            {
                                Kind = TriggerNodeKind.Condition,
                                GroupReference = "condition_group_1"
                            },
                            Actions = new TriggerNodeData
                            {
                                Kind = TriggerNodeKind.Action,
                                Type = "seq",
                                Children =
                                {
                                    new TriggerNodeData
                                    {
                                        Kind = TriggerNodeKind.Action,
                                        Type = "debug_log",
                                        Arguments =
                                        {
                                            new TriggerArgumentData
                                            {
                                                Name = "message",
                                                Value = new TriggerValueRefData
                                                {
                                                    Source = TriggerValueSource.Constant,
                                                    Type = TriggerValueType.String,
                                                    StringValue = "hello"
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        private static TriggerAuthoringTemplateSourceDocument BuildTemplateDocument()
        {
            return new TriggerAuthoringTemplateSourceDocument
            {
                Metadata = new TriggerAuthoringSourceMetadata { Author = "codec-tests" },
                Template = new TriggerAuthoringTemplateData
                {
                    TemplateId = "template.codec_fixture",
                    TemplateVersion = "1.0.0",
                    DisplayName = "Codec Template",
                    Event = "combat.damage_taken",
                    Parameters =
                    {
                        new TriggerAuthoringTemplateParameterData
                        {
                            Name = "message",
                            Type = TriggerValueType.String
                        }
                    },
                    Actions = new TriggerNodeData
                    {
                        Kind = TriggerNodeKind.Action,
                        Type = "debug_log",
                        Arguments =
                        {
                            new TriggerArgumentData
                            {
                                Name = "message",
                                Value = new TriggerValueRefData
                                {
                                    Source = TriggerValueSource.TemplateParameter,
                                    Type = TriggerValueType.String,
                                    Path = "message"
                                }
                            }
                        }
                    }
                }
            };
        }

        private void AssertNoAtomicArtifacts()
        {
            Assert.That(
                Directory.GetFiles(
                    _tempDirectory,
                    "*.abilitykit.tmp.*",
                    SearchOption.AllDirectories),
                Is.Empty);
            Assert.That(
                Directory.GetFiles(
                    _tempDirectory,
                    "*.abilitykit.bak.*",
                    SearchOption.AllDirectories),
                Is.Empty);
        }

        /// <summary>
        /// 第二个 codec：同一 DOM 的紧凑文本形态（无缩进、无结尾换行），
        /// 用于证明注册表可插拔且基线哈希不依赖文件文本。
        /// </summary>
        private sealed class CompactModuleCodec : ITriggerSourceCodec<TriggerAuthoringSourceDocument>
        {
            public string FormatId => "compact";

            public string FileExtension => "trgsrc";

            public string DisplayName => "Compact JSON";

            public string Serialize(TriggerAuthoringSourceDocument document)
            {
                TriggerSourceDocumentRules.ValidateModuleHeader(document);
                return JsonConvert.SerializeObject(
                    document,
                    TriggerSourceJson.CreateSettings(Formatting.None));
            }

            public TriggerAuthoringSourceDocument Deserialize(string text)
            {
                var document = TriggerSourceJson.Read<TriggerAuthoringSourceDocument>(
                    text, "Trigger authoring", "module");
                TriggerSourceDocumentRules.ValidateModuleHeader(document);
                return document;
            }
        }
    }
}
#endif
