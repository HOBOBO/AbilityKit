using System;
using System.IO;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Tests
{
    public sealed class TriggerAuthoringSourceSyncTests
    {
        private string _temporaryDirectory;
        private TriggerAuthoringModuleAsset _asset;

        [SetUp]
        public void SetUp()
        {
            _temporaryDirectory = Path.Combine(Path.GetTempPath(), "AbilityKitTriggerAuthoringTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryDirectory);
            _asset = ScriptableObject.CreateInstance<TriggerAuthoringModuleAsset>();
            _asset.Metadata.Description = "round trip";
            _asset.Module = CreateValidModule();
        }

        [TearDown]
        public void TearDown()
        {
            if (_asset != null) UnityEngine.Object.DestroyImmediate(_asset);
            if (!string.IsNullOrEmpty(_temporaryDirectory) && Directory.Exists(_temporaryDirectory))
                Directory.Delete(_temporaryDirectory, true);
        }

        [Test]
        public void Codec_RoundTripsStructuredModule()
        {
            var document = TriggerAuthoringSourceCodec.CreateDocument(_asset);
            var json = TriggerAuthoringSourceCodec.Serialize(document);
            var restored = TriggerAuthoringSourceCodec.Deserialize(json);

            Assert.That(restored.Schema, Is.EqualTo(TriggerAuthoringSchema.Id));
            Assert.That(restored.Version, Is.EqualTo(TriggerAuthoringSchema.Version));
            Assert.That(restored.Module.ModuleId, Is.EqualTo("skill.fireball"));
            Assert.That(restored.Module.Triggers.Count, Is.EqualTo(1));
            Assert.That(restored.Module.Triggers[0].Actions.Type, Is.EqualTo("debug_log"));
            Assert.That(restored.Module.Triggers[0].Actions.Arguments[0].Value.StringValue, Is.EqualTo("cast"));
            StringAssert.Contains("\"kind\": \"Ability\"", json);
            StringAssert.Contains("\"source\": \"Constant\"", json);
        }

        [Test]
        public void Codec_RejectsUnknownJsonFields()
        {
            var json = TriggerAuthoringSourceCodec.Serialize(TriggerAuthoringSourceCodec.CreateDocument(_asset));
            json = json.Replace("\"schema\":", "\"unexpected\": 1,\n  \"schema\":");

            var exception = Assert.Throws<InvalidDataException>(() => TriggerAuthoringSourceCodec.Deserialize(json));
            StringAssert.Contains("unexpected", exception.Message);
        }

        [Test]
        public void Sync_ReportsAssetJsonAndConflictChanges()
        {
            var path = GetSourcePath();
            var exported = TriggerAuthoringSourceSync.Export(_asset, path);
            Assert.That(exported.Success, Is.True, exported.Message);
            Assert.That(TriggerAuthoringSourceSync.Inspect(_asset, path).State, Is.EqualTo(TriggerAuthoringSyncState.InSync));

            _asset.Module.DisplayName = "Asset edit";
            Assert.That(TriggerAuthoringSourceSync.Inspect(_asset, path).State, Is.EqualTo(TriggerAuthoringSyncState.AssetChanged));

            var source = TriggerAuthoringSourceCodec.ReadFile(path);
            source.Metadata.Description = "JSON edit";
            TriggerAuthoringSourceCodec.WriteFileAtomic(path, source);
            Assert.That(TriggerAuthoringSourceSync.Inspect(_asset, path).State, Is.EqualTo(TriggerAuthoringSyncState.Conflict));
        }

        [Test]
        public void Import_AppliesExternalJsonEditAndUpdatesBaseline()
        {
            var path = GetSourcePath();
            var exported = TriggerAuthoringSourceSync.Export(_asset, path);
            Assert.That(exported.Success, Is.True, exported.Message);

            var source = TriggerAuthoringSourceCodec.ReadFile(path);
            source.Module.DisplayName = "Edited by AI";
            TriggerAuthoringSourceCodec.WriteFileAtomic(path, source);
            Assert.That(TriggerAuthoringSourceSync.Inspect(_asset, path).State, Is.EqualTo(TriggerAuthoringSyncState.JsonChanged));

            var imported = TriggerAuthoringSourceSync.Import(_asset, path);
            Assert.That(imported.Success, Is.True, imported.Message);
            Assert.That(_asset.Module.DisplayName, Is.EqualTo("Edited by AI"));
            Assert.That(TriggerAuthoringSourceSync.Inspect(_asset, path).State, Is.EqualTo(TriggerAuthoringSyncState.InSync));
        }

        [Test]
        public void Inspect_TreatsIndependentlyConvergedContentAsInSync()
        {
            var path = GetSourcePath();
            Assert.That(TriggerAuthoringSourceSync.Export(_asset, path).Success, Is.True);

            _asset.Module.DisplayName = "Converged";
            var source = TriggerAuthoringSourceCodec.ReadFile(path);
            source.Module.DisplayName = "Converged";
            TriggerAuthoringSourceCodec.WriteFileAtomic(path, source);

            Assert.That(
                TriggerAuthoringSourceSync.Inspect(_asset, path).State,
                Is.EqualTo(TriggerAuthoringSyncState.InSync));
        }

        [Test]
        public void Inspect_DistinguishesUntrackedMissingAndInvalidSources()
        {
            var path = GetSourcePath();
            Assert.That(
                TriggerAuthoringSourceSync.Inspect(_asset, path).State,
                Is.EqualTo(TriggerAuthoringSyncState.Untracked));

            Assert.That(TriggerAuthoringSourceSync.Export(_asset, path).Success, Is.True);
            File.Delete(path);
            Assert.That(
                TriggerAuthoringSourceSync.Inspect(_asset, path).State,
                Is.EqualTo(TriggerAuthoringSyncState.SourceMissing));

            File.WriteAllText(path, "not-json");
            Assert.That(
                TriggerAuthoringSourceSync.Inspect(_asset, path).State,
                Is.EqualTo(TriggerAuthoringSyncState.InvalidSource));
        }

        [Test]
        public void Export_DoesNotOverwriteUntrackedJsonWithoutForce()
        {
            var path = GetSourcePath();
            File.WriteAllText(path, "{}");

            var result = TriggerAuthoringSourceSync.Export(_asset, path);

            Assert.That(result.Success, Is.False);
            Assert.That(result.State, Is.EqualTo(TriggerAuthoringSyncState.InvalidSource));
        }

        [Test]
        public void Validator_ReportsDuplicateTriggerIdsAndMissingArguments()
        {
            _asset.Module.Triggers.Add(new TriggerDefinitionData
            {
                Id = 1001,
                Event = "skill.cast",
                Actions = new TriggerNodeData
                {
                    Kind = TriggerNodeKind.Action,
                    Type = "debug_log"
                }
            });

            var diagnostics = TriggerAuthoringValidator.Validate(_asset.Module);

            Assert.That(diagnostics.Exists(d => d.Code == "TRG1004"), Is.True);
            Assert.That(diagnostics.Exists(d => d.Code == "TRG1212"), Is.True);
        }

        [Test]
        public void Validator_UsesMatchedEventPayloadFields()
        {
            var module = CreateValidModule();
            var message = module.Triggers[0].Actions.Arguments[0].Value;
            message.Source = TriggerValueSource.Payload;
            message.Type = TriggerValueType.String;
            message.Path = "skill.stage";

            var context = CreateValidationContext(
                new TriggerEventDefinitionData
                {
                    Id = "skill.",
                    MatchMode = TriggerEventMatchMode.Prefix,
                    PayloadFields =
                    {
                        new TriggerPayloadFieldData { Path = "skill.stage", Type = TriggerValueType.String }
                    }
                });

            var valid = TriggerAuthoringValidator.Validate(module, context);
            Assert.That(TriggerAuthoringValidator.HasErrors(valid), Is.False, FormatDiagnostics(valid));

            message.Path = "skill.missing";
            var invalid = TriggerAuthoringValidator.Validate(module, context);
            Assert.That(invalid.Exists(d => d.Code == "TRG1307"), Is.True, FormatDiagnostics(invalid));
        }

        [Test]
        public void Validator_ReportsPayloadTypeMismatch()
        {
            var module = CreateValidModule();
            var message = module.Triggers[0].Actions.Arguments[0].Value;
            message.Source = TriggerValueSource.Payload;
            message.Type = TriggerValueType.String;
            message.Path = "skill.level";

            var context = CreateValidationContext(
                new TriggerEventDefinitionData
                {
                    Id = "skill.cast",
                    PayloadFields =
                    {
                        new TriggerPayloadFieldData { Path = "skill.level", Type = TriggerValueType.Integer }
                    }
                });

            var diagnostics = TriggerAuthoringValidator.Validate(module, context);
            Assert.That(diagnostics.Exists(d => d.Code == "TRG1308"), Is.True, FormatDiagnostics(diagnostics));
        }

        [Test]
        public void Validator_RejectsWriteToReadOnlyGlobalBlackboardKey()
        {
            var module = CreateValidModule();
            module.Triggers[0].Actions = new TriggerNodeData
            {
                Kind = TriggerNodeKind.Action,
                Type = "set_var",
                Arguments =
                {
                    new TriggerArgumentData
                    {
                        Name = "target",
                        Value = new TriggerValueRefData
                        {
                            Source = TriggerValueSource.GlobalBlackboard,
                            Type = TriggerValueType.String,
                            Path = "match.mode"
                        }
                    },
                    new TriggerArgumentData
                    {
                        Name = "value",
                        Value = new TriggerValueRefData
                        {
                            Source = TriggerValueSource.Constant,
                            Type = TriggerValueType.String,
                            StringValue = "ranked"
                        }
                    }
                }
            };

            var context = CreateValidationContext(
                new TriggerEventDefinitionData { Id = "skill.cast" },
                new TriggerGlobalBlackboardKeyData
                {
                    Key = "match.mode",
                    Type = TriggerValueType.String,
                    CanRead = true,
                    CanWrite = false
                });

            var diagnostics = TriggerAuthoringValidator.Validate(module, context);
            Assert.That(diagnostics.Exists(d => d.Code == "TRG1312"), Is.True, FormatDiagnostics(diagnostics));
        }

        [Test]
        public void Validator_RejectsWriteToReadOnlyLocalBlackboardKey()
        {
            var module = CreateValidModule();
            module.Blackboard.Add(new TriggerBlackboardVariableData
            {
                Key = "cast.label",
                Type = TriggerValueType.String,
                ReadOnly = true
            });
            module.Triggers[0].Actions = new TriggerNodeData
            {
                Kind = TriggerNodeKind.Action,
                Type = "set_var",
                Arguments =
                {
                    new TriggerArgumentData
                    {
                        Name = "target",
                        Value = new TriggerValueRefData
                        {
                            Source = TriggerValueSource.LocalBlackboard,
                            Type = TriggerValueType.String,
                            Path = "cast.label"
                        }
                    },
                    new TriggerArgumentData
                    {
                        Name = "value",
                        Value = new TriggerValueRefData
                        {
                            Source = TriggerValueSource.Constant,
                            Type = TriggerValueType.String
                        }
                    }
                }
            };

            var diagnostics = TriggerAuthoringValidator.Validate(
                module,
                CreateValidationContext(new TriggerEventDefinitionData { Id = "skill.cast" }));
            Assert.That(diagnostics.Exists(d => d.Code == "TRG1314"), Is.True, FormatDiagnostics(diagnostics));
        }

        [Test]
        public void Validator_RejectsSetVariableTargetValueTypeMismatch()
        {
            var module = CreateValidModule();
            module.Blackboard.Add(new TriggerBlackboardVariableData
            {
                Key = "enabled",
                Type = TriggerValueType.Boolean
            });
            module.Triggers[0].Actions = new TriggerNodeData
            {
                Kind = TriggerNodeKind.Action,
                Type = "set_var",
                Arguments =
                {
                    new TriggerArgumentData
                    {
                        Name = "target",
                        Value = new TriggerValueRefData
                        {
                            Source = TriggerValueSource.LocalBlackboard,
                            Type = TriggerValueType.Boolean,
                            Path = "enabled"
                        }
                    },
                    new TriggerArgumentData
                    {
                        Name = "value",
                        Value = new TriggerValueRefData
                        {
                            Source = TriggerValueSource.Constant,
                            Type = TriggerValueType.String,
                            StringValue = "true"
                        }
                    }
                }
            };

            var diagnostics = TriggerAuthoringValidator.Validate(
                module,
                CreateValidationContext(new TriggerEventDefinitionData { Id = "skill.cast" }));

            Assert.That(diagnostics.Exists(d => d.Code == "TRG1315"), Is.True, FormatDiagnostics(diagnostics));
        }

        [Test]
        public void ProjectDefaults_ResolveExactEventBeforePrefixFamily()
        {
            var catalog = new TriggerEventDescriptorCatalog(TriggerAuthoringProjectDefaults.CreateMobaEvents());

            Assert.That(catalog.TryResolve("skill.cast.start", out var exact), Is.True);
            Assert.That(exact.MatchMode, Is.EqualTo(TriggerEventMatchMode.Exact));
            Assert.That(exact.PayloadFields.Exists(f => f.Path == "skill.id"), Is.True);

            Assert.That(catalog.TryResolve("skill.custom.stage", out var family), Is.True);
            Assert.That(family.MatchMode, Is.EqualTo(TriggerEventMatchMode.Prefix));
        }

        [Test]
        public void ProjectDescriptors_ExposeTypedMobaActionParameters()
        {
            var catalog = TriggerTypeDescriptorCatalog.CreateProjectDefaults();

            Assert.That(catalog.TryGet(TriggerNodeKind.Action, "shoot_projectile", out var descriptor), Is.True);
            Assert.That(descriptor.Parameters.Count, Is.GreaterThanOrEqualTo(5));
            Assert.That(descriptor.Parameters[0].Name, Is.EqualTo("launcher_id"));
            Assert.That(descriptor.Parameters[0].Type, Is.EqualTo(TriggerValueType.Integer));
            Assert.That(descriptor.Parameters[0].Required, Is.True);
        }

        [Test]
        public void ProjectDescriptors_CoverRegisteredMobaPlanActions()
        {
            var catalog = TriggerTypeDescriptorCatalog.CreateProjectDefaults();
            var actionTypes = new[]
            {
                "give_damage", "adjust_damage_number", "take_damage", "debug_log",
                "shoot_projectile", "add_buff", "remove_buff", "cancel_skill",
                "add_shield", "remove_shield", "remove_summon", "remove_projectile",
                "remove_area", "spawn_area", "heal", "spawn_summon", "play_presentation",
                "emit", "end_game", "set_gameplay_var", "add_gameplay_var",
                "advance_gameplay_counter", "dash", "blink", "pull", "jump",
                "consume_resource", "modify_resource", "convert_resource_to_heal",
                "start_cooldown", "reset_cooldown"
            };

            for (var i = 0; i < actionTypes.Length; i++)
                Assert.That(catalog.TryGet(TriggerNodeKind.Action, actionTypes[i], out _), Is.True, actionTypes[i]);

            Assert.That(catalog.TryGet(TriggerNodeKind.Action, "aoe_burst", out _), Is.False);
            Assert.That(catalog.TryGet(TriggerNodeKind.Action, "effect_execute", out _), Is.False);
        }

        [Test]
        public void ProjectDescriptors_ExposeTypedConditionsAndEnumOptions()
        {
            var catalog = TriggerTypeDescriptorCatalog.CreateProjectDefaults();

            Assert.That(catalog.TryGet(TriggerNodeKind.Condition, "has_buff", out var hasBuff), Is.True);
            Assert.That(hasBuff.Parameters.Count, Is.EqualTo(3));
            Assert.That(hasBuff.Parameters[0].Name, Is.EqualTo("buff_id"));
            Assert.That(hasBuff.Parameters[0].Type, Is.EqualTo(TriggerValueType.Integer));
            Assert.That(hasBuff.Parameters[0].Required, Is.True);
            Assert.That(hasBuff.Parameters[2].Options.Count, Is.EqualTo(2));

            Assert.That(catalog.TryGet(TriggerNodeKind.Action, "modify_resource", out var modifyResource), Is.True);
            var resourceType = FindParameter(modifyResource, "resource_type");
            Assert.That(resourceType, Is.Not.Null);
            Assert.That(resourceType.Options.Count, Is.EqualTo(7));
        }

        [Test]
        public void Validator_RequiresAtLeastOneArgumentFromRequiredGroup()
        {
            var module = CreateValidModule();
            module.Triggers[0].Actions = new TriggerNodeData
            {
                Kind = TriggerNodeKind.Action,
                Type = "give_damage"
            };

            var diagnostics = TriggerAuthoringValidator.Validate(module);
            Assert.That(diagnostics.Exists(d => d.Code == "TRG1213"), Is.True, FormatDiagnostics(diagnostics));

            module.Triggers[0].Actions.Arguments.Add(new TriggerArgumentData
            {
                Name = "source_attack_ratio",
                Value = new TriggerValueRefData
                {
                    Source = TriggerValueSource.Constant,
                    Type = TriggerValueType.Number,
                    NumberValue = 1d
                }
            });
            diagnostics = TriggerAuthoringValidator.Validate(module);
            Assert.That(diagnostics.Exists(d => d.Code == "TRG1213"), Is.False, FormatDiagnostics(diagnostics));
        }

        [Test]
        public void Codec_RoundTripsReusableGroupReferencesWithoutInlining()
        {
            _asset.Module.ActionGroups.Add(new TriggerNodeGroupData
            {
                Id = "shared.log",
                DisplayName = "Shared Log",
                Root = CreateDebugLogNode("from group")
            });
            _asset.Module.Triggers[0].Actions = new TriggerNodeData
            {
                Kind = TriggerNodeKind.Action,
                GroupReference = "shared.log"
            };

            var json = TriggerAuthoringSourceCodec.Serialize(TriggerAuthoringSourceCodec.CreateDocument(_asset));
            var restored = TriggerAuthoringSourceCodec.Deserialize(json);

            Assert.That(restored.Module.ActionGroups.Count, Is.EqualTo(1));
            Assert.That(restored.Module.ActionGroups[0].Root.Type, Is.EqualTo("debug_log"));
            Assert.That(restored.Module.Triggers[0].Actions.GroupReference, Is.EqualTo("shared.log"));
            Assert.That(restored.Module.Triggers[0].Actions.Type, Is.Null.Or.Empty);
            StringAssert.Contains("\"groupReference\": \"shared.log\"", json);
        }

        [Test]
        public void GroupResolver_ExpandsNestedReferencesAndReturnsIndependentTree()
        {
            _asset.Module.ActionGroups.Add(new TriggerNodeGroupData
            {
                Id = "leaf",
                Root = CreateDebugLogNode("leaf value")
            });
            _asset.Module.ActionGroups.Add(new TriggerNodeGroupData
            {
                Id = "outer",
                Root = new TriggerNodeData
                {
                    Kind = TriggerNodeKind.Action,
                    Type = "seq",
                    Children =
                    {
                        new TriggerNodeData
                        {
                            Kind = TriggerNodeKind.Action,
                            GroupReference = "leaf"
                        }
                    }
                }
            });
            var reference = new TriggerNodeData
            {
                Kind = TriggerNodeKind.Action,
                GroupReference = "outer"
            };

            var success = TriggerAuthoringGroupResolver.TryExpand(
                _asset.Module,
                reference,
                TriggerNodeKind.Action,
                out var expanded,
                out var failure);

            Assert.That(success, Is.True, failure?.Message);
            Assert.That(expanded.Type, Is.EqualTo("seq"));
            Assert.That(expanded.Children[0].Type, Is.EqualTo("debug_log"));
            Assert.That(expanded.Children[0].GroupReference, Is.Null.Or.Empty);
            expanded.Children[0].Arguments[0].Value.StringValue = "local edit";
            Assert.That(_asset.Module.ActionGroups[0].Root.Arguments[0].Value.StringValue, Is.EqualTo("leaf value"));
        }

        [Test]
        public void Validator_ReportsMissingAndCyclicGroupReferences()
        {
            _asset.Module.Triggers[0].Actions = new TriggerNodeData
            {
                Kind = TriggerNodeKind.Action,
                GroupReference = "missing"
            };
            var missing = TriggerAuthoringValidator.Validate(_asset.Module);
            Assert.That(missing.Exists(d => d.Code == "TRG1505"), Is.True, FormatDiagnostics(missing));

            _asset.Module.ActionGroups.Add(new TriggerNodeGroupData
            {
                Id = "first",
                Root = new TriggerNodeData
                {
                    Kind = TriggerNodeKind.Action,
                    GroupReference = "second"
                }
            });
            _asset.Module.ActionGroups.Add(new TriggerNodeGroupData
            {
                Id = "second",
                Root = new TriggerNodeData
                {
                    Kind = TriggerNodeKind.Action,
                    GroupReference = "first"
                }
            });
            _asset.Module.Triggers[0].Actions.GroupReference = "first";

            var cyclic = TriggerAuthoringValidator.Validate(_asset.Module);
            Assert.That(cyclic.Exists(d => d.Code == "TRG1506"), Is.True, FormatDiagnostics(cyclic));
        }

        [Test]
        public void Validator_ValidatesExpandedGroupAgainstTriggerEventPayload()
        {
            var message = CreateDebugLogNode("unused");
            message.Arguments[0].Value = new TriggerValueRefData
            {
                Source = TriggerValueSource.Payload,
                Type = TriggerValueType.String,
                Path = "skill.stage"
            };
            _asset.Module.ActionGroups.Add(new TriggerNodeGroupData { Id = "payload.log", Root = message });
            _asset.Module.Triggers[0].Actions = new TriggerNodeData
            {
                Kind = TriggerNodeKind.Action,
                GroupReference = "payload.log"
            };
            var context = CreateValidationContext(new TriggerEventDefinitionData
            {
                Id = "skill.cast",
                PayloadFields =
                {
                    new TriggerPayloadFieldData { Path = "skill.stage", Type = TriggerValueType.String }
                }
            });

            var valid = TriggerAuthoringValidator.Validate(_asset.Module, context);
            Assert.That(TriggerAuthoringValidator.HasErrors(valid), Is.False, FormatDiagnostics(valid));

            message.Arguments[0].Value.Path = "skill.missing";
            var invalid = TriggerAuthoringValidator.Validate(_asset.Module, context);
            Assert.That(invalid.Exists(d => d.Code == "TRG1307"), Is.True, FormatDiagnostics(invalid));
        }

        [Test]
        public void Codec_RoundTripsVector3Constant()
        {
            var value = _asset.Module.Triggers[0].Actions.Arguments[0].Value;
            value.Type = TriggerValueType.Vector3;
            value.Vector3Value = new TriggerVector3Data { X = 1.25, Y = -2.5, Z = 3.75 };

            var json = TriggerAuthoringSourceCodec.Serialize(TriggerAuthoringSourceCodec.CreateDocument(_asset));
            var restored = TriggerAuthoringSourceCodec.Deserialize(json);
            var vector = restored.Module.Triggers[0].Actions.Arguments[0].Value.Vector3Value;

            Assert.That(vector.X, Is.EqualTo(1.25));
            Assert.That(vector.Y, Is.EqualTo(-2.5));
            Assert.That(vector.Z, Is.EqualTo(3.75));
        }

        [Test]
        public void ModuleAsset_UsesTriggerAuthoringWorkspaceInspector()
        {
            var editor = UnityEditor.Editor.CreateEditor(_asset);
            try
            {
                Assert.That(editor, Is.Not.Null);
                Assert.That(editor.GetType().Name, Is.EqualTo("TriggerAuthoringModuleAssetEditor"));
            }
            finally
            {
                if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
            }
        }

        [Test]
        public void TemplateCodec_RoundTripsReferenceDataWithoutUnityAssetReference()
        {
            var templateAsset = ScriptableObject.CreateInstance<TriggerAuthoringTemplateAsset>();
            try
            {
                templateAsset.Metadata.Description = "AI editable template";
                templateAsset.Template = CreateLogTemplate("template.log", "1.2.0");

                var json = TriggerAuthoringTemplateSourceCodec.Serialize(
                    TriggerAuthoringTemplateSourceCodec.CreateDocument(templateAsset));
                var restored = TriggerAuthoringTemplateSourceCodec.Deserialize(json);

                Assert.That(restored.Version, Is.EqualTo("2.2"));
                Assert.That(restored.Template.TemplateId, Is.EqualTo("template.log"));
                Assert.That(restored.Template.Parameters[0].Name, Is.EqualTo("message"));
                Assert.That(restored.Template.Actions.Arguments[0].Value.Source,
                    Is.EqualTo(TriggerValueSource.TemplateParameter));
                StringAssert.DoesNotContain("instanceID", json);
                StringAssert.DoesNotContain("assetGuid", json);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(templateAsset);
            }
        }

        [Test]
        public void Validator_RequiresExactTemplateVersionAndRequiredBindings()
        {
            var templateAsset = ScriptableObject.CreateInstance<TriggerAuthoringTemplateAsset>();
            try
            {
                templateAsset.Template = CreateLogTemplate("template.log", "1.2.0");
                var module = CreateValidModule();
                module.Triggers[0].Actions = null;
                module.Triggers[0].Template = new TriggerTemplateReferenceData
                {
                    TemplateId = "template.log",
                    Version = "1.1.0"
                };
                var context = new TriggerAuthoringValidationContext
                {
                    Types = TriggerTypeDescriptorCatalog.CreateProjectDefaults(),
                    Templates = new TriggerTemplateDescriptorCatalog(new[] { templateAsset })
                };

                var invalid = TriggerAuthoringValidator.Validate(module, context);
                Assert.That(invalid.Exists(d => d.Code == "TRG1602"), Is.True, FormatDiagnostics(invalid));
                Assert.That(invalid.Exists(d => d.Code == "TRG1607"), Is.True, FormatDiagnostics(invalid));

                module.Triggers[0].Template.Version = "1.2.0";
                module.Triggers[0].Template.Bindings.Add(new TriggerArgumentData
                {
                    Name = "message",
                    Value = new TriggerValueRefData
                    {
                        Source = TriggerValueSource.Constant,
                        Type = TriggerValueType.String,
                        StringValue = "bound"
                    }
                });
                var valid = TriggerAuthoringValidator.Validate(module, context);
                Assert.That(TriggerAuthoringValidator.HasErrors(valid), Is.False, FormatDiagnostics(valid));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(templateAsset);
            }
        }

        [Test]
        public void Validator_RejectsUnknownTemplateParameterReference()
        {
            var template = CreateLogTemplate("template.log", "1.0.0");
            template.Actions.Arguments[0].Value.Path = "missing";

            var diagnostics = TriggerAuthoringTemplateValidator.Validate(template);

            Assert.That(diagnostics.Exists(d => d.Code == "TRG1618"), Is.True, FormatDiagnostics(diagnostics));
        }

        [Test]
        public void TemplateAsset_UsesTemplateAuthoringInspector()
        {
            var templateAsset = ScriptableObject.CreateInstance<TriggerAuthoringTemplateAsset>();
            try
            {
                var editor = UnityEditor.Editor.CreateEditor(templateAsset);
                try
                {
                    Assert.That(editor, Is.Not.Null);
                    Assert.That(editor.GetType().Name, Is.EqualTo("TriggerAuthoringTemplateAssetEditor"));
                }
                finally
                {
                    if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(templateAsset);
            }
        }

        private string GetSourcePath()
        {
            return Path.Combine(_temporaryDirectory, "skill.fireball.json");
        }

        private static TriggerAuthoringModuleData CreateValidModule()
        {
            return new TriggerAuthoringModuleData
            {
                ModuleId = "skill.fireball",
                DisplayName = "Fireball",
                Kind = TriggerModuleKind.Ability,
                Triggers =
                {
                    new TriggerDefinitionData
                    {
                        Id = 1001,
                        Name = "Cast log",
                        Event = "skill.cast",
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
                                        Source = TriggerValueSource.Constant,
                                        Type = TriggerValueType.String,
                                        StringValue = "cast"
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        private static TriggerNodeData CreateDebugLogNode(string message)
        {
            return new TriggerNodeData
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
                            StringValue = message
                        }
                    }
                }
            };
        }

        private static TriggerAuthoringTemplateData CreateLogTemplate(string id, string version)
        {
            return new TriggerAuthoringTemplateData
            {
                TemplateId = id,
                TemplateVersion = version,
                Event = "skill.cast",
                Parameters =
                {
                    new TriggerAuthoringTemplateParameterData
                    {
                        Name = "message",
                        Type = TriggerValueType.String,
                        Required = true,
                        AllowedSources = TriggerTemplateValueSourceMask.Constant
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
            };
        }

        private static TriggerAuthoringValidationContext CreateValidationContext(
            TriggerEventDefinitionData eventDefinition,
            TriggerGlobalBlackboardKeyData globalKey = null)
        {
            return new TriggerAuthoringValidationContext
            {
                Types = TriggerTypeDescriptorCatalog.CreateProjectDefaults(),
                Events = new TriggerEventDescriptorCatalog(new[] { eventDefinition }),
                GlobalBlackboard = globalKey == null
                    ? null
                    : new TriggerGlobalBlackboardDescriptorCatalog(new[] { globalKey })
            };
        }

        private static string FormatDiagnostics(System.Collections.Generic.IReadOnlyList<TriggerAuthoringDiagnostic> diagnostics)
        {
            if (diagnostics == null) return string.Empty;
            var result = string.Empty;
            for (var i = 0; i < diagnostics.Count; i++)
                result += diagnostics[i].Code + " " + diagnostics[i].Path + ": " + diagnostics[i].Message + "\n";
            return result;
        }

        private static TriggerParameterDescriptor FindParameter(TriggerTypeDescriptor descriptor, string name)
        {
            for (var i = 0; i < descriptor.Parameters.Count; i++)
            {
                if (descriptor.Parameters[i].Name == name) return descriptor.Parameters[i];
            }
            return null;
        }
    }
}
