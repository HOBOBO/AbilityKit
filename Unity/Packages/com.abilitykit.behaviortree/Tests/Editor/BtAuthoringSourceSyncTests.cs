#if UNITY_EDITOR
using System;
using System.IO;
using AbilityKit.BehaviorTree.Authoring;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.BehaviorTree.Editor.Tests
{
    public sealed class BtAuthoringSourceSyncTests
    {
        private string _directory = null!;
        private string _sourcePath = null!;
        private BtAuthoringAsset _asset = null!;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "AbilityKit.BtSync." + Guid.NewGuid().ToString("N"));
            _sourcePath = Path.Combine(_directory, "tree.json");
            _asset = ScriptableObject.CreateInstance<BtAuthoringAsset>();
            var document = BtAuthoringTemplates.BuildEmpty();
            document.Tree.TreeId = "sync_test";
            _asset.SaveDocument(document);
            Assert.That(BtAuthoringSourceSync.Export(_asset, _sourcePath).Success, Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            if (_asset != null) UnityEngine.Object.DestroyImmediate(_asset);
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }

        [Test]
        public void Inspect_ClassifiesEveryChangeQuadrant()
        {
            Assert.That(BtAuthoringSourceSync.Inspect(_asset).State, Is.EqualTo(BtAuthoringSyncState.InSync));

            SaveAssetDescription("local");
            Assert.That(BtAuthoringSourceSync.Inspect(_asset).State, Is.EqualTo(BtAuthoringSyncState.AssetChanged));

            Assert.That(BtAuthoringSourceSync.Import(_asset, _sourcePath, force: true).Success, Is.True);
            SaveFileDescription("external");
            Assert.That(BtAuthoringSourceSync.Inspect(_asset).State, Is.EqualTo(BtAuthoringSyncState.JsonChanged));

            Assert.That(BtAuthoringSourceSync.Import(_asset, _sourcePath, force: true).Success, Is.True);
            SaveAssetDescription("local-again");
            SaveFileDescription("external-again");
            Assert.That(BtAuthoringSourceSync.Inspect(_asset).State, Is.EqualTo(BtAuthoringSyncState.Conflict));
        }

        [Test]
        public void Inspect_TreatsEquivalentFormattingAndConvergedContentAsInSync()
        {
            SaveAssetDescription("same");
            var json = BtAuthoringJson.Save(_asset.LoadDocument());
            File.WriteAllText(_sourcePath, JObject.Parse(json).ToString(Formatting.None));

            Assert.That(BtAuthoringSourceSync.Inspect(_asset).State, Is.EqualTo(BtAuthoringSyncState.InSync));
        }

        [Test]
        public void Export_RequiresForceBeforeOverwritingExternalChanges()
        {
            SaveFileDescription("external");
            SaveAssetDescription("local");

            var blocked = BtAuthoringSourceSync.Export(_asset, _sourcePath);
            Assert.That(blocked.Success, Is.False);
            Assert.That(blocked.CanForce, Is.True);
            Assert.That(BtAuthoringJson.Load(File.ReadAllText(_sourcePath)).Metadata.Description, Is.EqualTo("external"));

            Assert.That(BtAuthoringSourceSync.Export(_asset, _sourcePath, force: true).Success, Is.True);
            Assert.That(BtAuthoringSourceSync.Inspect(_asset).State, Is.EqualTo(BtAuthoringSyncState.InSync));
        }

        [Test]
        public void Export_RequiresForceBeforeOverwritingInvalidJson()
        {
            File.WriteAllText(_sourcePath, "not-json");

            var blocked = BtAuthoringSourceSync.Export(_asset, _sourcePath);
            Assert.That(blocked.Success, Is.False);
            Assert.That(blocked.CanForce, Is.True);
            Assert.That(File.ReadAllText(_sourcePath), Is.EqualTo("not-json"));
        }

        private void SaveAssetDescription(string value)
        {
            var document = _asset.LoadDocument();
            document.Metadata.Description = value;
            _asset.SaveDocument(document);
        }

        private void SaveFileDescription(string value)
        {
            var document = BtAuthoringJson.Load(File.ReadAllText(_sourcePath));
            document.Metadata.Description = value;
            File.WriteAllText(_sourcePath, BtAuthoringJson.Save(document));
        }
    }
}
#endif
