#if UNITY_EDITOR
using System;
using System.IO;
using AbilityKit.BehaviorTree.Authoring;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

using AbilityKit.BehaviorTree.Editor;
namespace AbilityKit.BehaviorTree.Editor.Tests
{
    public sealed class AuthoringSourceSyncTests
    {
        private string _directory = null!;
        private string _sourcePath = null!;
        private AuthoringAsset _asset = null!;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "AbilityKit.BtSync." + Guid.NewGuid().ToString("N"));
            _sourcePath = Path.Combine(_directory, "tree.json");
            _asset = ScriptableObject.CreateInstance<AuthoringAsset>();
            var document = AuthoringTemplates.BuildEmpty();
            document.Tree.TreeId = "sync_test";
            _asset.SaveDocument(document);
            Assert.That(AuthoringSourceSync.Export(_asset, _sourcePath).Success, Is.True);
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
            Assert.That(AuthoringSourceSync.Inspect(_asset).State, Is.EqualTo(AuthoringSyncState.InSync));

            SaveAssetDescription("local");
            Assert.That(AuthoringSourceSync.Inspect(_asset).State, Is.EqualTo(AuthoringSyncState.AssetChanged));

            Assert.That(AuthoringSourceSync.Import(_asset, _sourcePath, force: true).Success, Is.True);
            SaveFileDescription("external");
            Assert.That(AuthoringSourceSync.Inspect(_asset).State, Is.EqualTo(AuthoringSyncState.JsonChanged));

            Assert.That(AuthoringSourceSync.Import(_asset, _sourcePath, force: true).Success, Is.True);
            SaveAssetDescription("local-again");
            SaveFileDescription("external-again");
            Assert.That(AuthoringSourceSync.Inspect(_asset).State, Is.EqualTo(AuthoringSyncState.Conflict));
        }

        [Test]
        public void Inspect_ClassifiesUnboundAndMissingSourcesExplicitly()
        {
            var unbound = ScriptableObject.CreateInstance<AuthoringAsset>();
            try
            {
                unbound.SaveDocument(AuthoringTemplates.BuildEmpty());
                Assert.That(
                    AuthoringSourceSync.Inspect(unbound).State,
                    Is.EqualTo(AuthoringSyncState.Untracked));

                File.Delete(_sourcePath);
                Assert.That(
                    AuthoringSourceSync.Inspect(_asset).State,
                    Is.EqualTo(AuthoringSyncState.SourceMissing));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(unbound);
            }
        }

        [Test]
        public void Inspect_TreatsEquivalentFormattingAndConvergedContentAsInSync()
        {
            SaveAssetDescription("same");
            var json = AuthoringJson.Save(_asset.LoadDocument());
            File.WriteAllText(_sourcePath, JObject.Parse(json).ToString(Formatting.None));

            Assert.That(AuthoringSourceSync.Inspect(_asset).State, Is.EqualTo(AuthoringSyncState.InSync));
        }

        [Test]
        public void Export_RequiresForceBeforeOverwritingExternalChanges()
        {
            SaveFileDescription("external");
            SaveAssetDescription("local");

            var blocked = AuthoringSourceSync.Export(_asset, _sourcePath);
            Assert.That(blocked.Success, Is.False);
            Assert.That(blocked.CanForce, Is.True);
            Assert.That(AuthoringJson.Load(File.ReadAllText(_sourcePath)).Metadata.Description, Is.EqualTo("external"));

            Assert.That(AuthoringSourceSync.Export(_asset, _sourcePath, force: true).Success, Is.True);
            Assert.That(AuthoringSourceSync.Inspect(_asset).State, Is.EqualTo(AuthoringSyncState.InSync));
        }

        [Test]
        public void Export_RequiresForceBeforeOverwritingInvalidJson()
        {
            File.WriteAllText(_sourcePath, "not-json");

            var blocked = AuthoringSourceSync.Export(_asset, _sourcePath);
            Assert.That(blocked.Success, Is.False);
            Assert.That(blocked.CanForce, Is.True);
            Assert.That(File.ReadAllText(_sourcePath), Is.EqualTo("not-json"));
        }

        [Test]
        public void Inspect_ClassifiesInvalidSourceExplicitly()
        {
            File.WriteAllText(_sourcePath, "not-json");

            var inspection = AuthoringSourceSync.Inspect(_asset);

            Assert.That(inspection.State, Is.EqualTo(AuthoringSyncState.InvalidSource));
        }

        [Test]
        public void Import_ConflictRequiresForceThenConvergesToSource()
        {
            SaveAssetDescription("local");
            SaveFileDescription("external");

            var blocked = AuthoringSourceSync.Import(_asset, _sourcePath);
            Assert.That(blocked.Success, Is.False);
            Assert.That(blocked.CanForce, Is.True);
            Assert.That(_asset.LoadDocument().Metadata.Description, Is.EqualTo("local"));

            var forced = AuthoringSourceSync.Import(_asset, _sourcePath, force: true);
            Assert.That(forced.Success, Is.True);
            Assert.That(_asset.LoadDocument().Metadata.Description, Is.EqualTo("external"));
            Assert.That(AuthoringSourceSync.Inspect(_asset).State, Is.EqualTo(AuthoringSyncState.InSync));
        }

        [Test]
        public void Import_UntrackedAuthoredAssetRequiresForceThenConverges()
        {
            var unbound = ScriptableObject.CreateInstance<AuthoringAsset>();
            try
            {
                var document = AuthoringTemplates.BuildEmpty();
                document.Metadata.Description = "local-authored";
                unbound.SaveDocument(document);

                var blocked = AuthoringSourceSync.Import(unbound, _sourcePath);
                Assert.That(blocked.Success, Is.False);
                Assert.That(blocked.CanForce, Is.True);
                Assert.That(unbound.LoadDocument().Metadata.Description, Is.EqualTo("local-authored"));

                var forced = AuthoringSourceSync.Import(unbound, _sourcePath, force: true);
                Assert.That(forced.Success, Is.True);
                Assert.That(
                    AuthoringSourceSync.Inspect(unbound).State,
                    Is.EqualTo(AuthoringSyncState.InSync));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(unbound);
            }
        }

        [Test]
        public void Export_ConflictForceConvergesAndLeavesNoTemporaryFiles()
        {
            SaveFileDescription("external");
            SaveAssetDescription("local");

            Assert.That(AuthoringSourceSync.Export(_asset, _sourcePath).Success, Is.False);
            Assert.That(AuthoringSourceSync.Export(_asset, _sourcePath, force: true).Success, Is.True);
            Assert.That(AuthoringSourceSync.Inspect(_asset).State, Is.EqualTo(AuthoringSyncState.InSync));
            Assert.That(
                Directory.GetFiles(_directory, "*.abilitykit.tmp.*", SearchOption.TopDirectoryOnly),
                Is.Empty);
        }

        private void SaveAssetDescription(string value)
        {
            var document = _asset.LoadDocument();
            document.Metadata.Description = value;
            _asset.SaveDocument(document);
        }

        private void SaveFileDescription(string value)
        {
            var document = AuthoringJson.Load(File.ReadAllText(_sourcePath));
            document.Metadata.Description = value;
            File.WriteAllText(_sourcePath, AuthoringJson.Save(document));
        }
    }
}
#endif
