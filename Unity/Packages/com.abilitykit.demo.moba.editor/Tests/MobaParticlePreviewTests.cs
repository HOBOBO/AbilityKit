using System.Collections.Generic;
using System.Linq;
using AbilityKit.ActionEditorImpl;
using AbilityKit.Demo.Moba.Editor;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Game.Flow;
using ActionEditorExample;
using NBC.ActionEditor;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaParticlePreviewTests
    {
        private sealed class OrderingProbe : IPresentationOrderingAdapter<GameObject>
        {
            public GameObject Target;
            public PresentationOrdering Ordering;
            public void Apply(GameObject visual, GameObject target, in PresentationOrdering ordering)
            { Target = target; Ordering = ordering; }
        }

        private sealed class Preview : MobaPlayParticlePreview
        {
            public MobaPresentationTemplateResolver Resolver;
            public Transform Source;
            public GameObject Prefab;
            public string LoadedKey;
            public readonly OrderingProbe Ordering = new OrderingProbe();
            public GameObject Visual => EffectObject;
            protected override Transform SourceTransform => Source;
            protected override MobaPresentationTemplateResolver CreateTemplateResolver() => Resolver;
            protected override GameObject LoadResource(string key) { LoadedKey = key; return Prefab; }
            protected override IPresentationOrderingAdapter<GameObject> CreateOrderingAdapter() => Ordering;
        }

        private readonly List<GameObject> _objects = new List<GameObject>();
        private Preview _preview;
        private readonly List<TextAsset> _assets = new List<TextAsset>();
        private GameObject NewObject(string name)
        {
            var result = new GameObject(name);
            _objects.Add(result);
            return result;
        }

        [TearDown]
        public void TearDown()
        {
            AssetPlayer.Inst.ResetPreview();
            App.TextAsset = null;
            _preview?.Dispose();
            foreach (var obj in _objects) if (obj != null) Object.DestroyImmediate(obj);
            _objects.Clear();
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        private static PresentationTemplateDTO Template() => new PresentationTemplateDTO
        {
            Id = 123, AssetId = 456, ResourceKey = "template-override", AttachMode = 1,
            Socket = "hand", Follow = true, Scale = 1.5f, Radius = 2f,
            OffsetX = 1, OffsetY = 2, OffsetZ = 3, OrderingMode = 1, OrderingPriority = 7
        };

        private Preview CreatePreview(PresentationTemplateDTO template, float speed = 1f)
        {
            var actor = NewObject("preview-actor");
            actor.transform.SetPositionAndRotation(new Vector3(3, 2, 4), Quaternion.Euler(0, 90, 0));
            actor.transform.localScale = Vector3.one * 2;
            var socket = NewObject("hand");
            socket.transform.SetParent(actor.transform, false);
            socket.transform.localPosition = Vector3.up;
            socket.transform.localScale = Vector3.one * 0.5f;
            var prefab = NewObject("preview-prefab");
            prefab.transform.localScale = new Vector3(2, 3, 4);
            _preview = new Preview
            {
                Source = actor.transform, Prefab = prefab,
                Resolver = new MobaPresentationTemplateResolver(new[] { template },
                    new[] { new VfxDTO { Id = 456, ResourceKey = "asset-key", Resource = "legacy-key", Speed = speed } })
            };
            _preview.SetTarget(new PlayParticle { presentationTemplateId = template.Id, Length = 3 });
            return _preview;
        }

        private static void Equal(Vector3 expected, Vector3 actual) =>
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(0.001f));

        [TestCase(0, 0)]
        [TestCase(0, 1)]
        [TestCase(1, 0)]
        [TestCase(1, 1)]
        public void PreviewUsesRuntimeResourceOffsetAndStableScale(int offsetSpace, int parentScaleMode)
        {
            var template = Template();
            template.OffsetSpace = offsetSpace;
            template.ParentScaleMode = parentScaleMode;
            var preview = CreatePreview(template);
            preview.Enter();
            Assert.That(preview.LoadedKey, Is.EqualTo("template-override"));
            Assert.That(preview.Visual.transform.parent, Is.Null);
            var target = preview.Source.Find("hand");
            foreach (var scale in new[] { 2f, 4f, 2f })
            {
                preview.Source.localScale = Vector3.one * scale;
                preview.Source.position += Vector3.right;
                preview.Update(0.25f, 0.5f);
                var offset = new Vector3(1, 2, 3);
                Equal(offsetSpace == 1 ? target.position + offset : target.TransformPoint(offset), preview.Visual.transform.position);
                Equal(new Vector3(2, 3, 4) * 3f * (parentScaleMode == 0 ? scale * 0.5f : 1f), preview.Visual.transform.lossyScale);
                Assert.That(preview.Ordering.Target, Is.SameAs(target.gameObject));
                Assert.That(preview.Ordering.Ordering.Priority, Is.EqualTo(7));
            }
        }

        [Test]
        public void OneShotPlacesAtSocketWithoutFollowingOrInheritingScale()
        {
            var template = Template();
            template.Follow = false;
            var preview = CreatePreview(template);
            preview.Enter();
            Equal(preview.Source.Find("hand").TransformPoint(new Vector3(1, 2, 3)), preview.Visual.transform.position);
            Equal(new Vector3(6, 9, 12), preview.Visual.transform.lossyScale);
            var position = preview.Visual.transform.position;
            preview.Source.position += Vector3.one * 10;
            preview.Source.localScale = Vector3.one * 10;
            preview.Update(0.5f, 0);
            Equal(position, preview.Visual.transform.position);
            preview.Exit();
            preview.ReverseEnter();
            preview.Update(0.25f, 0.5f);
            Equal(position, preview.Visual.transform.position);
            preview.Reverse();
            Assert.That(preview.Visual.activeSelf, Is.False);
            preview.Enter();
            Equal(preview.Source.Find("hand").TransformPoint(new Vector3(1, 2, 3)), preview.Visual.transform.position);
        }

        [Test]
        public void WorldTemplateIgnoresStaleSocketAndActorTransform()
        {
            var template = Template();
            template.AttachMode = 0;
            var preview = CreatePreview(template);
            Assert.That(preview.Resolver.TryCreateRequest(123, "runtime", 42, 1000, 0, out var request), Is.True);
            Assert.That(request.Attachment.Kind, Is.EqualTo(PresentationAttachmentKind.World));
            preview.Enter();
            preview.Source.position += Vector3.one * 10;
            preview.Update(0.5f, 0);
            Equal(new Vector3(1, 2, 3), preview.Visual.transform.position);
            Equal(new Vector3(6, 9, 12), preview.Visual.transform.lossyScale);
            Assert.That(preview.Ordering.Target, Is.Null);
        }

        [Test]
        public void SocketFallbackMatchesExactPathContract()
        {
            var template = Template();
            template.Socket = "nested";
            var preview = CreatePreview(template);
            var nested = NewObject("nested");
            nested.transform.SetParent(preview.Source.Find("hand"), false);
            nested.transform.localPosition = Vector3.one * 10;
            preview.Enter();
            Equal(preview.Source.TransformPoint(new Vector3(1, 2, 3)), preview.Visual.transform.position);
            Assert.That(preview.Ordering.Target, Is.SameAs(preview.Source.gameObject));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingSourceFallsBackAndOnlyFollowCanBindLater(bool follow)
        {
            var template = Template();
            template.Follow = follow;
            var preview = CreatePreview(template);
            var source = preview.Source;
            preview.Source = null;
            preview.Enter();
            Equal(new Vector3(1, 2, 3), preview.Visual.transform.position);
            preview.Source = source;
            preview.Update(0.5f, 0);
            Equal(follow ? source.Find("hand").TransformPoint(new Vector3(1, 2, 3)) : new Vector3(1, 2, 3), preview.Visual.transform.position);
        }

        [TestCase("template", "asset", "legacy", "template")]
        [TestCase("", "asset", "legacy", "asset")]
        [TestCase("", "", "legacy", "legacy")]
        public void ResolverPreservesResourcePriorityAndActorId(string templateKey, string assetKey, string legacy, string expected)
        {
            var template = Template();
            template.ResourceKey = templateKey;
            var resolver = new MobaPresentationTemplateResolver(new[] { template },
                new[] { new VfxDTO { Id = 456, ResourceKey = assetKey, Resource = legacy } });
            Assert.That(resolver.TryCreateRequest(123, "runtime", 42, 1000, 0, out var request), Is.True);
            Assert.That(request.ResourceKey, Is.EqualTo(expected));
            Assert.That(request.Attachment.ActorId, Is.EqualTo(42));
        }

        [Test]
        public void ParticleScrubbingCoversAllRootsAndRestoresEarlierState()
        {
            var preview = CreatePreview(Template(), 2f);
            for (var i = 0; i < 2; i++)
            {
                var child = NewObject("particle-" + i);
                child.transform.SetParent(preview.Prefab.transform, false);
                var particle = child.AddComponent<ParticleSystem>();
                var main = particle.main;
                main.startLifetime = 10;
                main.startSpeed = 1;
                main.simulationSpeed = 3;
                var emission = particle.emission;
                emission.rateOverTime = 20;
            }
            preview.Enter();
            var particles = preview.Visual.GetComponentsInChildren<ParticleSystem>();
            preview.Update(0.25f, 0);
            var snapshots = new List<ParticleSystem.Particle[]>();
            foreach (var particle in particles)
            {
                Assert.That(particle.time, Is.EqualTo(0.5f).Within(0.025f));
                Assert.That(particle.particleCount, Is.GreaterThan(0));
                var snapshot = new ParticleSystem.Particle[particle.particleCount];
                particle.GetParticles(snapshot);
                snapshots.Add(snapshot);
            }
            preview.Update(1f, 0.25f);
            preview.Update(0.25f, 1f);
            for (var i = 0; i < particles.Length; i++)
            {
                Assert.That(particles[i].particleCount, Is.EqualTo(snapshots[i].Length));
                var actual = new ParticleSystem.Particle[particles[i].particleCount];
                particles[i].GetParticles(actual);
                for (var j = 0; j < actual.Length; j++) Equal(snapshots[i][j].position, actual[j].position);
            }
            var visual = preview.Visual;
            preview.Dispose();
            preview.Dispose();
            Assert.That(visual == null, Is.True);
        }

        [Test]
        public void PlayerRebuildStopAndAssetSwitchReleaseOwnedPreviews()
        {
            const string resource = "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_Q";
            var asset = new SkillAsset();
            var track = asset.AddGroup<Group>().AddTrack<EffectTrack>();
            var clip = track.AddClip<PlayParticle>(0);
            clip.Length = 3;
            clip.resourceKey = resource;
            var text = new TextAsset(Json.Serialize(asset));
            _assets.Add(text);
            App.TextAsset = text;
            var player = AssetPlayer.Inst;
            player.Sample(0.5f);
            var previews = (List<PreviewBase>)typeof(AssetPlayer).GetField(
                "previews", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(player);
            Assert.That(previews.Single(), Is.TypeOf<MobaPlayParticlePreview>());
            var visual = Resources.FindObjectsOfTypeAll<GameObject>().Single(obj => obj.name == "Jhin_Battlecast_Q (ActionEditor Preview)");
            Assert.That(visual.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave));
            player.InitializePreviewPointers();
            Assert.That(visual == null, Is.True);
            player.Sample(0.5f);
            visual = Resources.FindObjectsOfTypeAll<GameObject>().Single(obj => obj.name == "Jhin_Battlecast_Q (ActionEditor Preview)");
            App.Stop();
            Assert.That(visual == null, Is.True);
            player.Sample(0.5f);
            visual = Resources.FindObjectsOfTypeAll<GameObject>().Single(obj => obj.name == "Jhin_Battlecast_Q (ActionEditor Preview)");
            App.Play();
            App.TextAsset = null;
            Assert.That(visual == null, Is.True);
            Assert.That(App.IsPlay, Is.False);
            player.InitializePreviewPointers();
            Assert.That(player.CurrentTime, Is.Zero);
        }
    }
}
