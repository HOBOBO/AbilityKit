using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.View.Config;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Hierarchy;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>
    /// Runtime-facing checks for Veigar's linked model shell and authored HFSM
    /// action variants. Resource catalog tests alone do not prove the view path
    /// can instantiate the configured model.
    /// </summary>
    public sealed class VeigarUnityPresentationConsumptionTests
    {
        private const int HeroId = 3004;
        private const string ModelResource =
            "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_Model";

        private static readonly (int Id, string Resource)[] VeigarVfx =
        {
            (90034001, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_AA_Projectile"),
            (90034002, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_AA_Hit"),
            (90034003, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_E"),
            (90034004, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_E_Activation"),
            (90034005, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_E_Cage"),
            (90034006, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_Q"),
            (90034007, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_Q_Hit"),
            (90034008, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_Q_Projectile"),
            (90034009, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_R"),
            (90034010, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_R_Hit"),
            (90034011, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_R_Projectile"),
            (90034012, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_W"),
            (90034013, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_W_Cast"),
            (90034014, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_W_Explosion"),
            (90034015, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_W_Projectile"),
        };

        [Test]
        public void PublishedVeigarVfx_ResolveThroughDatabaseAndInstantiateRenderers()
        {
            var vfxDb = BattleViewFactory.GetOrLoadVfxDb();
            Assert.IsNotNull(vfxDb, "The published VFX database must load.");
            foreach (var entry in VeigarVfx)
            {
                Assert.IsTrue(vfxDb.TryGet(entry.Id, out var row),
                    "Veigar VFX row is missing: " + entry.Id);
                var resource = string.IsNullOrEmpty(row.ResourceKey) ? row.Resource : row.ResourceKey;
                Assert.AreEqual(entry.Resource, resource,
                    "Veigar VFX row must point at the formal extracted resource.");
                var prefab = Resources.Load<GameObject>(entry.Resource);
                Assert.IsNotNull(prefab, "Veigar VFX prefab is missing: " + entry.Resource);
                var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                Assert.IsNotEmpty(renderers,
                    "Veigar VFX prefab must contain a renderer: " + entry.Resource);
                var instance = Object.Instantiate(prefab);
                try
                {
                    var enabledRenderer = false;
                    foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    {
                        if (renderer.enabled && renderer.sharedMaterial != null)
                        {
                            enabledRenderer = true;
                            break;
                        }
                    }
                    Assert.IsTrue(enabledRenderer,
                        "Veigar VFX prefab must expose an enabled material renderer: " + entry.Resource);
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        [Test]
        public void ModelSkinBindings_MatchImportedMeshBoneOrder()
        {
            var prefab = Resources.Load<GameObject>(ModelResource);
            var renderer = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(renderer.sharedMesh));
            var sourceRenderer = imported.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.IsNotNull(sourceRenderer);
            Assert.AreEqual(renderer.sharedMesh.bindposes.Length, renderer.bones.Length,
                "The prefab must bind every slot of the imported skin.");
            Assert.AreEqual(sourceRenderer.bones.Length, renderer.bones.Length);
            for (var i = 0; i < sourceRenderer.bones.Length; i++)
            {
                Assert.IsNotNull(renderer.bones[i], "Missing skin bone at slot " + i);
                Assert.AreEqual(sourceRenderer.bones[i].name, renderer.bones[i].name,
                    "Prefab bone order must match the mesh importer at slot " + i);
            }
        }

        [Test]
        public void ModelShell_UsesPublishedVeigarPrefabThroughViewFactory()
        {
            var resources = new BattleViewResourceProvider();
            var configs = resources.GetOrLoadConfigs();
            Assert.IsTrue(configs.TryGetCharacter(HeroId, out var character));
            Assert.AreEqual(HeroId, character.ModelId,
                "Veigar character must resolve the linked model table row.");

            var shell = resources.CreateShellGameObject(actorId: 3004001, modelId: character.ModelId);
            Assert.IsNotNull(shell, "Veigar view factory returned no model shell.");
            try
            {
                Assert.AreEqual("Actor_3004001", shell.name);
                Assert.IsNotNull(shell.GetComponentInChildren<Animator>(true),
                    "Veigar shell must use the authored Animator.");
                var renderers = shell.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.IsNotEmpty(renderers,
                    "Veigar shell must contain the authored skinned mesh.");
                foreach (var renderer in renderers)
                {
                    Assert.IsNotNull(renderer.sharedMesh,
                        "Veigar shell renderer must retain its imported mesh.");
                    Assert.IsNotEmpty(renderer.sharedMaterials,
                        "Veigar shell renderer must retain its imported materials.");
                    Assert.IsTrue(renderer.enabled,
                        "Veigar shell renderer must be enabled after instantiation.");
                }
            }
            finally
            {
                Object.DestroyImmediate(shell);
            }
        }

        [Test]
        public void CharacterProjection_ResolvesVeigarModelThroughViewBinder()
        {
            var world = new AbilityKit.World.ECS.EntityWorld();
            var lookup = new BattleEntityLookup();
            var factory = new BattleEntityFactory(world, lookup);
            var vfxRoot = world.Create("veigar-projection-vfx");
            var vfxDb = BattleViewFactory.GetOrLoadVfxDb();
            var resources = new BattleViewResourceProvider();
            var hierarchyRoot = BattleViewHierarchyRoot.CreateOrFind("[VeigarProjection]");
            var hierarchy = hierarchyRoot.Manager;
            resources.SetHierarchyManager(hierarchy);
            var shellPool = new BattleViewShellPool(
                modelId => resources.CreateShellGameObject(0, modelId),
                defaultCapacity: 1,
                maxSize: 2,
                hierarchy: hierarchy);
            var vfx = new BattleVfxManager(vfxDb);
            var binder = new BattleViewBinder(vfx, in vfxRoot, resources, shellPool, null, hierarchy);
            var context = new BattleContext
            {
                EntityWorld = world,
                EntityLookup = lookup,
                EntityFactory = factory,
                ViewVfxManager = vfx,
                ViewVfxNode = vfxRoot,
            };

            const int actorId = 3004001;
            try
            {
                BattleSnapshotEntityApplier.ApplySpawn(context, new[]
                {
                    new ActorSpawnData(
                        ActorSpawnKind.Character,
                        actorId,
                        ownerActorId: 0,
                        entityCode: HeroId,
                        characterId: HeroId,
                        name: "Veigar",
                        x: 3f,
                        y: 0f,
                        z: 7f,
                        rotationY: 0f,
                        scale: 1f,
                        teamId: 1,
                        maxHp: 1000f,
                        hp: 1000f)
                });

                Assert.IsTrue(lookup.TryResolve(world,
                    new AbilityKit.Game.Battle.Entity.BattleNetId(actorId), out var entity));
                Assert.IsTrue(entity.TryGetRef(out BattleEntityMetaComponent meta));
                Assert.AreEqual(BattleEntityKind.Character, meta.Kind);
                Assert.AreEqual(HeroId, meta.EntityCode);
                Assert.AreEqual(HeroId, resources.ResolveModelId(meta));

                binder.InterpolationEnabled = false;
                binder.SmoothingHz = 0f;
                binder.Sync(entity);
                binder.TickInterpolation(context, context, 1f / 30f);

                Assert.IsTrue(binder.TryGetShellGameObject(entity.Id, out var shell));
                Assert.IsNotNull(shell, "Character projection did not create a Veigar shell.");
                Assert.IsTrue(shell.activeInHierarchy, "Veigar shell is inactive in the active view.");
                Assert.That(shell.transform.lossyScale.sqrMagnitude, Is.GreaterThan(0.001f),
                    "Veigar shell was projected with a zero scale.");
                Assert.That(Vector3.Distance(new Vector3(3f, 0f, 7f), shell.transform.position),
                    Is.LessThan(0.01f), "Veigar shell did not receive the projected transform.");

                var renderers = shell.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.IsNotEmpty(renderers, "Character projection shell has no Veigar skinned mesh.");
                foreach (var renderer in renderers)
                {
                    Assert.IsTrue(renderer.enabled);
                    Assert.IsNotNull(renderer.sharedMesh);
                    Assert.IsNotEmpty(renderer.sharedMaterials);
                    TestContext.WriteLine($"Projected {renderer.name}: bounds={renderer.bounds}, scale={renderer.transform.lossyScale}");
                }
                AssertVisiblePixels(shell, new Vector3(3f, 0f, 7f));
            }
            finally
            {
                binder.Clear();
                shellPool.Clear();
                hierarchyRoot.DestroyHierarchy();
                if (vfxRoot.IsValid) vfxRoot.Destroy();
            }
        }

        private static void AssertVisiblePixels(GameObject shell, Vector3 anchor, string captureName = "veigar-projected-model")
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                TestContext.WriteLine("Pixel verification skipped: no graphics device. Run without -nographics for visual acceptance.");
                return;
            }

            var originalParent = shell.transform.parent;
            var originalScene = shell.scene;
            shell.transform.SetParent(null, true);
            var scene = EditorSceneManager.NewPreviewScene();
            var cameraObject = new GameObject("VeigarProjectionAcceptanceCamera");
            var camera = cameraObject.AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(shell, scene);
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            camera.scene = scene;
            camera.transform.position = anchor + new Vector3(4f, 5f, -6f);
            camera.transform.LookAt(anchor + Vector3.up * 0.75f);
            camera.orthographic = true;
            camera.orthographicSize = 3f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.gray;
            camera.depthTextureMode = DepthTextureMode.Depth;
            var target = RenderTexture.GetTemporary(512, 512, 24);
            var texture = new Texture2D(512, 512, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            try
            {
                var cullingMask = camera.cullingMask;
                camera.cullingMask = 0;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                texture.Apply();
                var hidden = texture.GetPixels32();
                camera.cullingMask = cullingMask;
                var animator = shell.GetComponentInChildren<Animator>(true);
                animator.Update(0f);
                foreach (var renderer in shell.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh = new Mesh();
                    try
                    {
                        renderer.BakeMesh(mesh);
                        mesh.RecalculateBounds();
                        Assert.Greater(mesh.bounds.size.sqrMagnitude, 0.01f,
                            "The skin must produce noncollapsed geometry.");
                        TestContext.WriteLine($"Baked {renderer.name}: vertices={mesh.vertexCount}, bounds={mesh.bounds}, bones={renderer.bones.Length}, bindposes={renderer.sharedMesh.bindposes.Length}");
                    }
                    finally { Object.DestroyImmediate(mesh); }
                }
                camera.Render();
                texture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                texture.Apply();
                var visible = texture.GetPixels32();
                var pixels = 0;
                for (var i = 0; i < visible.Length; i++)
                    if (!visible[i].Equals(hidden[i])) pixels++;
                var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,
                    "../../local/Logs/" + captureName + ".png"));
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
                TestContext.WriteLine($"Veigar projected model: rendered pixels={pixels}, capture={path}");
                Assert.Greater(pixels, 100, "The projected model must visibly render near its gameplay anchor.");
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(cameraObject);
                SceneManager.MoveGameObjectToScene(shell, originalScene);
                shell.transform.SetParent(originalParent, true);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [TestCase("life/alive/action/idle", 0, "Veigar_Model_Idle")]
        [TestCase("life/alive/action/moving", 0, "Veigar_Model_Idle")]
        [TestCase("life/alive/action/casting", 30040101, "Veigar_Model_AA")]
        [TestCase("life/alive/action/casting", 30040201, "Veigar_Model_SpellQ")]
        [TestCase("life/alive/action/casting", 30040301, "Veigar_Model_SpellW")]
        [TestCase("life/alive/action/casting", 30040401, "Veigar_Model_SpellE")]
        [TestCase("life/alive/action/casting", 30040501, "Veigar_Model_SpellR")]
        public void HfsmView_ConsumesPublishedVeigarAnimatorAction(
            string path, int skillId, string expectedState)
        {
            var prefab = Resources.Load<GameObject>(ModelResource);
            Assert.IsNotNull(prefab, "Veigar model prefab must be available through Resources.");
            var instance = Object.Instantiate(prefab);
            try
            {
                var loader = new ResourcesTextAssetLoader();
                var component = new BattleCharacterHfsmComponent(
                    MobaCharacterHfsmProfile.ResolveDefinition(loader, HeroId),
                    CharacterPresentationActionCatalog.Load(loader), HeroId);
                component.ApplyPresentation(new CharacterActionData(
                    actorId: 3004001,
                    entityVersion: 1,
                    snapshotFrame: 0,
                    path: path,
                    instanceId: ((long)3004001 << 32) | 1u,
                    startFrame: 0,
                    localFrame: 0,
                    castInstanceId: skillId > 0 ? 1 : 0,
                    skillId: skillId));

                var view = instance.GetComponent<MonoCharacterHfsmView>() ??
                           instance.AddComponent<MonoCharacterHfsmView>();
                view.Bind(component, 24);
                var animator = instance.GetComponentInChildren<Animator>(true);
                Assert.IsNotNull(animator, "Veigar model must expose an Animator.");
                Assert.AreEqual(
                    Animator.StringToHash(expectedState),
                    animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                    path + " must select the published Veigar model action.");
                AssertVisiblePixels(instance, instance.transform.position,
                    "veigar-model-" + expectedState + "-" + skillId);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
