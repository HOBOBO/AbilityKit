using System;
using System.IO;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using NUnit.Framework;
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
    }
}
