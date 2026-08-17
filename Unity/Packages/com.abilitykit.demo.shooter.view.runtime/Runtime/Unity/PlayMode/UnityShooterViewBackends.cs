#nullable enable

using System.Collections.Generic;
using AbilityKit.Demo.Shooter.View.Hosting;
using Unity.Profiling;
using UnityEngine;

namespace AbilityKit.Demo.Shooter.View.PlayMode
{
    public enum ShooterUnityViewRenderBackend
    {
        GameObject = 0,
        GpuInstancedDotsReady = 1,
        EntitiesGraphics = 2
    }

    public readonly struct ShooterUnityViewRenderBackendDescriptor
    {
        public ShooterUnityViewRenderBackendDescriptor(
            ShooterUnityViewRenderBackend backend,
            string displayName,
            string capabilitySummary,
            bool isHighDensity,
            bool requiresDotsPackages,
            bool isAvailable)
        {
            Backend = backend;
            DisplayName = displayName;
            CapabilitySummary = capabilitySummary;
            IsHighDensity = isHighDensity;
            RequiresDotsPackages = requiresDotsPackages;
            IsAvailable = isAvailable;
        }

        public ShooterUnityViewRenderBackend Backend { get; }

        public string DisplayName { get; }

        public string CapabilitySummary { get; }

        public bool IsHighDensity { get; }

        public bool RequiresDotsPackages { get; }

        public bool IsAvailable { get; }
    }

    public static class ShooterUnityViewRenderBackendCatalog
    {
        private static readonly ShooterUnityViewRenderBackendDescriptor[] Backends =
        {
            new ShooterUnityViewRenderBackendDescriptor(
                ShooterUnityViewRenderBackend.GameObject,
                "GameObject",
                "兼容路径，保留调试友好的对象层级和池化表现。",
                isHighDensity: false,
                requiresDotsPackages: false,
                isAvailable: true),
            new ShooterUnityViewRenderBackendDescriptor(
                ShooterUnityViewRenderBackend.GpuInstancedDotsReady,
                "GPU Instanced",
                "高密度演示路径，通过批量实例化承接后续 DOTS/Entities Graphics 后端。",
                isHighDensity: true,
                requiresDotsPackages: false,
                isAvailable: true),
            new ShooterUnityViewRenderBackendDescriptor(
                ShooterUnityViewRenderBackend.EntitiesGraphics,
                "Entities Graphics",
                "真实 DOTS 渲染路径占位；安装 Entities/Entities Graphics 包后在此分支接入。",
                isHighDensity: true,
                requiresDotsPackages: true,
                isAvailable: false)
        };

        private static readonly string[] DisplayNames = CreateDisplayNamesCore();

        public static ShooterUnityViewRenderBackend DefaultBackend => ShooterUnityViewRenderBackend.GpuInstancedDotsReady;

        public static int Count => Backends.Length;

        public static ShooterUnityViewRenderBackendDescriptor Get(int index)
        {
            return Backends[ClampIndex(index)];
        }

        public static ShooterUnityViewRenderBackendDescriptor Get(ShooterUnityViewRenderBackend backend)
        {
            for (var i = 0; i < Backends.Length; i++)
            {
                if (Backends[i].Backend == backend)
                {
                    return Backends[i];
                }
            }

            return Get(DefaultBackend);
        }

        public static string[] GetDisplayNames()
        {
            return DisplayNames;
        }

        private static string[] CreateDisplayNamesCore()
        {
            var names = new string[Backends.Length];
            for (var i = 0; i < names.Length; i++)
            {
                names[i] = Backends[i].IsAvailable ? Backends[i].DisplayName : $"{Backends[i].DisplayName} (planned)";
            }

            return names;
        }

        public static int IndexOf(ShooterUnityViewRenderBackend backend)
        {
            for (var i = 0; i < Backends.Length; i++)
            {
                if (Backends[i].Backend == backend)
                {
                    return i;
                }
            }

            return IndexOf(DefaultBackend);
        }

        public static ShooterUnityViewRenderBackend Normalize(ShooterUnityViewRenderBackend backend)
        {
            var descriptor = Get(backend);
            return descriptor.IsAvailable ? descriptor.Backend : DefaultBackend;
        }

        private static int ClampIndex(int index)
        {
            if (index < 0)
            {
                return 0;
            }

            return index >= Backends.Length ? Backends.Length - 1 : index;
        }
    }

    internal interface IUnityShooterViewSink : IShooterHostViewSink
    {
        ShooterUnityViewRenderBackend Backend { get; }

        void RebuildAll();
    }

    internal sealed class UnityShooterSwitchableViewSink : IUnityShooterViewSink
    {
        private IUnityShooterViewSink _inner;

        public UnityShooterSwitchableViewSink()
            : this(ShooterUnityViewRenderBackendCatalog.DefaultBackend)
        {
        }

        public UnityShooterSwitchableViewSink(ShooterUnityViewRenderBackend backend)
        {
            _inner = Create(ShooterUnityViewRenderBackendCatalog.Normalize(backend));
        }

        public ShooterUnityViewRenderBackend Backend => _inner.Backend;

        public void SetBackend(ShooterUnityViewRenderBackend backend)
        {
            var normalized = ShooterUnityViewRenderBackendCatalog.Normalize(backend);
            if (_inner.Backend == normalized)
            {
                return;
            }

            _inner.Clear();
            _inner = Create(normalized);
        }

        public void Render(in ShooterHostPresentationFrame frame)
        {
            _inner.Render(in frame);
        }

        public void Clear()
        {
            _inner.Clear();
        }

        public void RebuildAll()
        {
            _inner.RebuildAll();
        }

        private static IUnityShooterViewSink Create(ShooterUnityViewRenderBackend backend)
        {
            return backend switch
            {
                ShooterUnityViewRenderBackend.GpuInstancedDotsReady => new UnityShooterGpuInstancedViewSink(),
                _ => new UnityShooterGameObjectViewSink()
            };
        }
    }

    internal sealed class UnityShooterGpuInstancedViewSink : IUnityShooterViewSink
    {
        private static readonly ProfilerMarker RebuildInstanceBufferMarker = new ProfilerMarker("AbilityKit.Shooter.View.GpuInstanced.RebuildInstanceBuffer");
        private static readonly ProfilerMarker UploadIndirectBufferMarker = new ProfilerMarker("AbilityKit.Shooter.View.GpuInstanced.UploadIndirectBuffer");
        private static readonly ProfilerMarker DrawBufferMarker = new ProfilerMarker("AbilityKit.Shooter.View.GpuInstanced.DrawBuffer");

        private const int MaxInstancesPerDraw = 1023;
        private static readonly bool DrawAuthorityOverlay = false;
        private readonly ShooterSnapshotViewProjection _clientProjection = new();
        private readonly ShooterSnapshotViewProjection _authorityProjection = new();
        private readonly Matrix4x4[] _playerMatrices = new Matrix4x4[MaxInstancesPerDraw];
        private readonly Matrix4x4[] _bulletMatrices = new Matrix4x4[MaxInstancesPerDraw];
        private readonly Matrix4x4[] _enemyMatrices = new Matrix4x4[MaxInstancesPerDraw];
        private readonly InstanceBuffer _clientInstances = new();
        private readonly InstanceBuffer _authorityInstances = new();
        private readonly IndirectBufferSet _clientIndirectBuffers = new();
        private readonly IndirectBufferSet _authorityIndirectBuffers = new();
        private readonly MaterialPropertyBlock _properties = new();
        private readonly GUIContent[] _hudLines = CreateHudLineCache(10);
        private Transform? _viewRoot;
        private Camera? _camera;
        private Light? _light;
        private HudBehaviour? _hudBehaviour;
        private Mesh? _playerMesh;
        private Mesh? _bulletMesh;
        private Mesh? _enemyMesh;
        private Material? _playerMaterial;
        private Material? _controlledPlayerMaterial;
        private Material? _bulletMaterial;
        private Material? _enemyMaterial;
        private Material? _authorityMaterial;
        private int _lastControlledPlayerId;
        private float _lastWorldScale = 1f;
        private int _lastBatchPlayerCount;
        private int _lastBatchBulletCount;
        private int _lastBatchEnemyCount;
        private int _lastBatchRemovedEntityCount;
        private int _lastStorePlayerCount;
        private int _lastStoreBulletCount;
        private int _lastStoreEnemyCount;
        private int _lastDrawPlayerCount;
        private int _lastDrawBulletCount;
        private int _lastDrawEnemyCount;
        private int _lastControlledHp = -1;
        private int _lastSkippedPlayerWithoutTransformCount;
        private int _lastSkippedDeadPlayerCount;
        private bool _lastHasControlledPlayerDraw;
        private bool _lastPlayerProbeDrawn;
        private Vector3 _lastFirstPlayerPosition;
        private bool _hasHudData;
        private bool _hudDirty;
        private bool _hasAuthorityProjection;
        private ShooterViewProjectionApplyResult _lastClientApplyResult = ShooterViewProjectionApplyResult.Empty;
        private ShooterCrossLayerDiagnostics _lastCrossLayerDiagnostics;
        private ulong _lastClientSequence;
        private ulong _lastAuthoritySequence;
        private int _lastClientFrame;
        private int _lastAuthorityFrame;
        private float _lastClientSampleFrame;
        private float _lastAuthoritySampleFrame;
        private ShooterViewBatchSource _lastClientSource;
        private ShooterViewBatchSource _lastAuthoritySource;
        private ShooterViewSnapshotKind _lastClientSnapshotKind;
        private ShooterViewSnapshotKind _lastAuthoritySnapshotKind;
        private bool _hasAppliedClientBatch;
        private bool _hasAppliedAuthorityBatch;
        private bool _clientInstancesDirty = true;
        private bool _authorityInstancesDirty = true;
        private bool _useIndirectRendering;

        public ShooterUnityViewRenderBackend Backend => ShooterUnityViewRenderBackend.GpuInstancedDotsReady;

        public void Render(in ShooterHostPresentationFrame frame)
        {
            EnsureResources();
            var viewKeyChanged = frame.ControlledPlayerId != _lastControlledPlayerId || Mathf.Abs(frame.WorldScale - _lastWorldScale) > 0.0001f;
            _lastControlledPlayerId = frame.ControlledPlayerId;
            _lastWorldScale = frame.WorldScale;
            var clientBatch = frame.ClientBatch;
            if (!IsSameClientBatch(in clientBatch))
            {
                _lastClientApplyResult = _clientProjection.Apply(in clientBatch);
                CaptureClientBatchKey(in clientBatch);
                _clientInstancesDirty = true;
            }

            if (_clientInstancesDirty || viewKeyChanged)
            {
                RebuildInstanceBuffer(_clientProjection.Store, _clientInstances, frame.ControlledPlayerId, frame.WorldScale, isAuthority: false);
                UploadIndirectBuffers(_clientInstances, _clientIndirectBuffers);
                _clientInstancesDirty = false;
            }

            var clientDrawCounts = DrawBuffer(_clientInstances, _clientIndirectBuffers, isAuthority: false);
            CaptureHudData(in frame, in clientDrawCounts);

            if (frame.HasAuthorityBatch)
            {
                var authorityBatch = frame.AuthorityBatch;
                if (!IsSameAuthorityBatch(in authorityBatch))
                {
                    _authorityProjection.Apply(in authorityBatch);
                    CaptureAuthorityBatchKey(in authorityBatch);
                    _authorityInstancesDirty = true;
                }

                if (_authorityInstancesDirty || viewKeyChanged)
                {
                    RebuildInstanceBuffer(_authorityProjection.Store, _authorityInstances, frame.ControlledPlayerId, frame.WorldScale, isAuthority: true);
                    UploadIndirectBuffers(_authorityInstances, _authorityIndirectBuffers);
                    _authorityInstancesDirty = false;
                }

                _hasAuthorityProjection = true;
                if (DrawAuthorityOverlay)
                {
                    DrawBuffer(_authorityInstances, _authorityIndirectBuffers, isAuthority: true);
                }
            }
            else
            {
                _authorityProjection.Clear();
                _authorityInstances.Clear();
                _hasAppliedAuthorityBatch = false;
                _authorityInstancesDirty = true;
                _hasAuthorityProjection = false;
            }
        }

        private bool IsSameClientBatch(in ShooterSnapshotViewBatch batch)
        {
            return _hasAppliedClientBatch &&
                batch.Sequence == _lastClientSequence &&
                batch.Frame == _lastClientFrame &&
                batch.SampleFrame.Equals(_lastClientSampleFrame) &&
                batch.Source == _lastClientSource &&
                batch.SnapshotKind == _lastClientSnapshotKind;
        }

        private bool IsSameAuthorityBatch(in ShooterSnapshotViewBatch batch)
        {
            return _hasAppliedAuthorityBatch &&
                batch.Sequence == _lastAuthoritySequence &&
                batch.Frame == _lastAuthorityFrame &&
                batch.SampleFrame.Equals(_lastAuthoritySampleFrame) &&
                batch.Source == _lastAuthoritySource &&
                batch.SnapshotKind == _lastAuthoritySnapshotKind;
        }

        private void CaptureClientBatchKey(in ShooterSnapshotViewBatch batch)
        {
            _lastClientSequence = batch.Sequence;
            _lastClientFrame = batch.Frame;
            _lastClientSampleFrame = batch.SampleFrame;
            _lastClientSource = batch.Source;
            _lastClientSnapshotKind = batch.SnapshotKind;
            _hasAppliedClientBatch = true;
        }

        private void CaptureAuthorityBatchKey(in ShooterSnapshotViewBatch batch)
        {
            _lastAuthoritySequence = batch.Sequence;
            _lastAuthorityFrame = batch.Frame;
            _lastAuthoritySampleFrame = batch.SampleFrame;
            _lastAuthoritySource = batch.Source;
            _lastAuthoritySnapshotKind = batch.SnapshotKind;
            _hasAppliedAuthorityBatch = true;
        }

        public void Clear()
        {
            _clientProjection.Clear();
            _authorityProjection.Clear();
            _clientInstances.Clear();
            _authorityInstances.Clear();
            _clientIndirectBuffers.Dispose();
            _authorityIndirectBuffers.Dispose();
            _hasAuthorityProjection = false;
            _hasHudData = false;
            _hudDirty = false;
            _lastClientSequence = 0UL;
            _lastAuthoritySequence = 0UL;
            _lastClientFrame = 0;
            _lastAuthorityFrame = 0;
            _lastClientSampleFrame = 0f;
            _lastAuthoritySampleFrame = 0f;
            _lastClientSource = default;
            _lastAuthoritySource = default;
            _lastClientSnapshotKind = default;
            _lastAuthoritySnapshotKind = default;
            _hasAppliedClientBatch = false;
            _hasAppliedAuthorityBatch = false;
            _clientInstancesDirty = true;
            _authorityInstancesDirty = true;
            _lastBatchPlayerCount = 0;
            _lastBatchBulletCount = 0;
            _lastBatchEnemyCount = 0;
            _lastBatchRemovedEntityCount = 0;
            _lastStorePlayerCount = 0;
            _lastStoreBulletCount = 0;
            _lastStoreEnemyCount = 0;
            _lastDrawPlayerCount = 0;
            _lastDrawBulletCount = 0;
            _lastDrawEnemyCount = 0;
            _lastControlledHp = -1;
            _lastSkippedPlayerWithoutTransformCount = 0;
            _lastSkippedDeadPlayerCount = 0;
            _lastHasControlledPlayerDraw = false;
            _lastPlayerProbeDrawn = false;
            _lastFirstPlayerPosition = Vector3.zero;
            _lastClientApplyResult = ShooterViewProjectionApplyResult.Empty;
            _lastCrossLayerDiagnostics = default;
            _useIndirectRendering = false;

            if (_viewRoot != null)
            {
                UnityEngine.Object.Destroy(_viewRoot.gameObject);
                _viewRoot = null;
                _camera = null;
                _light = null;
                _hudBehaviour = null;
            }

            DestroyRuntimeObject(ref _playerMesh);
            DestroyRuntimeObject(ref _bulletMesh);
            DestroyRuntimeObject(ref _enemyMesh);
            DestroyRuntimeObject(ref _playerMaterial);
            DestroyRuntimeObject(ref _controlledPlayerMaterial);
            DestroyRuntimeObject(ref _bulletMaterial);
            DestroyRuntimeObject(ref _enemyMaterial);
            DestroyRuntimeObject(ref _authorityMaterial);
        }

        public void RebuildAll()
        {
            EnsureResources();
        }

        private void CaptureHudData(in ShooterHostPresentationFrame frame, in DrawCounts clientDrawCounts)
        {
            CaptureHudCountsAndHealth(frame.ClientBatch, frame.ControlledPlayerId);
            _lastDrawPlayerCount = clientDrawCounts.PlayerCount;
            _lastDrawBulletCount = clientDrawCounts.BulletCount;
            _lastDrawEnemyCount = clientDrawCounts.EnemyCount;
            _lastSkippedDeadPlayerCount = _clientInstances.SkippedDeadPlayerCount;
            _lastSkippedPlayerWithoutTransformCount = _clientInstances.SkippedPlayerWithoutTransformCount;
            _lastHasControlledPlayerDraw = _clientInstances.HasControlledPlayer;
            _lastFirstPlayerPosition = _clientInstances.PlayerCount > 0 ? ExtractTranslation(_clientInstances.Players[0]) : Vector3.zero;
            _lastStorePlayerCount = _clientProjection.Store.PlayerCount;
            _lastStoreBulletCount = _clientProjection.Store.BulletCount;
            _lastStoreEnemyCount = _clientProjection.Store.EnemyCount;
            _lastCrossLayerDiagnostics = frame.CrossLayerDiagnostics;
            _hasHudData = true;
            _hudDirty = true;
        }

        private void RebuildInstanceBuffer(ShooterViewEntityStore store, InstanceBuffer buffer, int controlledPlayerId, float worldScale, bool isAuthority)
        {
            using var rebuildSample = RebuildInstanceBufferMarker.Auto();
            buffer.Clear();
            buffer.EnsureCapacity(store.PlayerCount, store.BulletCount, store.EnemyCount);
            for (var i = 0; i < store.DenseCount; i++)
            {
                if (!store.TryGetDenseEntityAndTransform(i, out var entity, out var transform))
                {
                    if (!isAuthority && entity.Kind == ShooterViewEntityKind.Player)
                    {
                        buffer.SkippedPlayerWithoutTransformCount++;
                    }

                    continue;
                }

                if (!entity.Alive)
                {
                    if (!isAuthority && entity.Kind == ShooterViewEntityKind.Player)
                    {
                        buffer.SkippedDeadPlayerCount++;
                    }

                    continue;
                }

                var kind = entity.Kind;
                var y = isAuthority ? 0.15f : 0f;
                var position = new Vector3(transform.X * worldScale, y, transform.Y * worldScale);
                var rotation = CreateFacingRotation(transform.FacingX, transform.FacingY);
                var matrix = Matrix4x4.TRS(position, rotation, ScaleFor(kind, isAuthority));

                AddCachedMatrix(kind, in matrix, buffer);

                if (!isAuthority && kind == ShooterViewEntityKind.Player && entity.EntityId == controlledPlayerId)
                {
                    buffer.ControlledPlayerMatrix = matrix;
                    buffer.HasControlledPlayer = true;
                }
            }
        }

        private static void AddCachedMatrix(ShooterViewEntityKind kind, in Matrix4x4 matrix, InstanceBuffer buffer)
        {
            switch (kind)
            {
                case ShooterViewEntityKind.Player:
                    buffer.Players.Add(matrix);
                    buffer.PlayerCount++;
                    break;
                case ShooterViewEntityKind.Bullet:
                    buffer.Bullets.Add(matrix);
                    buffer.BulletCount++;
                    break;
                case ShooterViewEntityKind.Enemy:
                    buffer.Enemies.Add(matrix);
                    buffer.EnemyCount++;
                    break;
            }
        }

        private void UploadIndirectBuffers(InstanceBuffer buffer, IndirectBufferSet indirectBuffers)
        {
            if (!_useIndirectRendering)
            {
                return;
            }

            using var uploadSample = UploadIndirectBufferMarker.Auto();
            indirectBuffers.Upload(
                MeshFor(ShooterViewEntityKind.Player),
                buffer.Players,
                MeshFor(ShooterViewEntityKind.Bullet),
                buffer.Bullets,
                MeshFor(ShooterViewEntityKind.Enemy),
                buffer.Enemies,
                buffer.HasControlledPlayer,
                in buffer.ControlledPlayerMatrix);
        }

        private DrawCounts DrawBuffer(InstanceBuffer buffer, IndirectBufferSet indirectBuffers, bool isAuthority)
        {
            using var drawSample = DrawBufferMarker.Auto();
            DrawInstances(ShooterViewEntityKind.Enemy, buffer.Enemies, indirectBuffers, isAuthority);
            DrawInstances(ShooterViewEntityKind.Bullet, buffer.Bullets, indirectBuffers, isAuthority);
            DrawInstances(ShooterViewEntityKind.Player, buffer.Players, indirectBuffers, isAuthority);
            _lastPlayerProbeDrawn = false;

            if (!isAuthority && buffer.HasControlledPlayer)
            {
                if (_useIndirectRendering)
                {
                    indirectBuffers.DrawControlled(_controlledPlayerMaterial ?? _playerMaterial);
                }
                else
                {
                    _playerMatrices[0] = buffer.ControlledPlayerMatrix;
                    Flush(ShooterViewEntityKind.Player, _controlledPlayerMaterial ?? _playerMaterial, _playerMatrices, 1);
                }
            }

            return new DrawCounts(buffer.PlayerCount, buffer.BulletCount, buffer.EnemyCount);
        }

        private void DrawInstances(
            ShooterViewEntityKind kind,
            List<Matrix4x4> matrices,
            IndirectBufferSet indirectBuffers,
            bool isAuthority)
        {
            if (_useIndirectRendering)
            {
                indirectBuffers.Draw(kind, MaterialFor(kind, isAuthority));
                return;
            }

            var sourceOffset = 0;
            var remaining = matrices.Count;
            var drawBuffer = BufferFor(kind);
            while (remaining > 0)
            {
                var drawCount = remaining > MaxInstancesPerDraw ? MaxInstancesPerDraw : remaining;
                matrices.CopyTo(sourceOffset, drawBuffer, 0, drawCount);
                Flush(kind, isAuthority, drawBuffer, drawCount);
                sourceOffset += drawCount;
                remaining -= drawCount;
            }
        }

        private Matrix4x4[] BufferFor(ShooterViewEntityKind kind)
        {
            return kind switch
            {
                ShooterViewEntityKind.Player => _playerMatrices,
                ShooterViewEntityKind.Bullet => _bulletMatrices,
                ShooterViewEntityKind.Enemy => _enemyMatrices,
                _ => _playerMatrices
            };
        }

        private void Flush(ShooterViewEntityKind kind, bool isAuthority, Matrix4x4[] matrices, int count)
        {
            Flush(kind, MaterialFor(kind, isAuthority), matrices, count);
        }

        private void Flush(ShooterViewEntityKind kind, Material? material, Matrix4x4[] matrices, int count)
        {
            var mesh = MeshFor(kind);
            if (mesh == null || material == null)
            {
                return;
            }

            Graphics.DrawMeshInstanced(mesh, 0, material, matrices, count, _properties, UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows: false);
        }

        private Mesh? MeshFor(ShooterViewEntityKind kind)
        {
            return kind switch
            {
                ShooterViewEntityKind.Player => _enemyMesh ?? _playerMesh,
                ShooterViewEntityKind.Bullet => _bulletMesh,
                ShooterViewEntityKind.Enemy => _enemyMesh,
                _ => null
            };
        }

        private Material? MaterialFor(ShooterViewEntityKind kind, bool isAuthority)
        {
            if (isAuthority)
            {
                return _authorityMaterial;
            }

            return kind switch
            {
                ShooterViewEntityKind.Player => _playerMaterial,
                ShooterViewEntityKind.Bullet => _bulletMaterial,
                ShooterViewEntityKind.Enemy => _enemyMaterial,
                _ => null
            };
        }

        private static Vector3 ScaleFor(ShooterViewEntityKind kind, bool isAuthority)
        {
            var authorityScale = isAuthority ? 0.85f : 1f;
            return kind switch
            {
                ShooterViewEntityKind.Player => new Vector3(0.75f, 0.75f, 0.75f) * authorityScale,
                ShooterViewEntityKind.Bullet => Vector3.one * (isAuthority ? 0.45f : 0.35f),
                ShooterViewEntityKind.Enemy => new Vector3(0.75f, 0.75f, 0.75f) * authorityScale,
                _ => Vector3.one
            };
        }

        private void EnsureResources()
        {
            if (_viewRoot != null)
            {
                return;
            }

            var root = new GameObject("ShooterPlayModeGpuInstancedViews");
            UnityEngine.Object.DontDestroyOnLoad(root);
            _viewRoot = root.transform;
            _hudBehaviour = root.AddComponent<HudBehaviour>();
            _hudBehaviour.Initialize(this);

            var cameraObject = new GameObject("ShooterGpuInstancedCamera");
            cameraObject.transform.SetParent(_viewRoot, false);
            cameraObject.transform.localPosition = new Vector3(4f, 18f, -12f);
            cameraObject.transform.localRotation = Quaternion.Euler(58f, 0f, 0f);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 18f;
            _camera.clearFlags = CameraClearFlags.Skybox;
            _camera.depth = 10f;

            var lightObject = new GameObject("ShooterGpuInstancedLight");
            lightObject.transform.SetParent(_viewRoot, false);
            lightObject.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);
            _light = lightObject.AddComponent<Light>();
            _light.type = LightType.Directional;
            _light.intensity = 1.2f;

            _playerMesh = CreatePrimitiveMesh(PrimitiveType.Cube, "ShooterGpuPlayerMesh");
            _bulletMesh = CreatePrimitiveMesh(PrimitiveType.Sphere, "ShooterGpuBulletMesh");
            _enemyMesh = CreatePrimitiveMesh(PrimitiveType.Cube, "ShooterGpuEnemyMesh");
            var indirectShader = Resources.Load<Shader>("ShooterIndirectInstanced");
            _useIndirectRendering = indirectShader != null && SystemInfo.supportsInstancing && SystemInfo.graphicsShaderLevel >= 45;
            _playerMaterial = CreateMaterial("ShooterGpuPlayerMaterial", Color.cyan, _useIndirectRendering ? indirectShader : null);
            _controlledPlayerMaterial = CreateMaterial("ShooterGpuControlledPlayerMaterial", Color.green, _useIndirectRendering ? indirectShader : null);
            _bulletMaterial = CreateMaterial("ShooterGpuBulletMaterial", Color.yellow, _useIndirectRendering ? indirectShader : null);
            _enemyMaterial = CreateMaterial("ShooterGpuEnemyMaterial", Color.red, _useIndirectRendering ? indirectShader : null);
            _authorityMaterial = CreateMaterial("ShooterGpuAuthorityMaterial", new Color(1f, 0.2f, 0.65f, 0.55f), _useIndirectRendering ? indirectShader : null);
        }

        private void DrawHud()
        {
            if (!_hasHudData)
            {
                return;
            }

            if (_hudDirty)
            {
                UpdateHudLineCache();
                _hudDirty = false;
            }

            const float hudWidth = 620f;
            const float hudHeight = 320f;
            var hudX = Mathf.Max(12f, Screen.width - hudWidth - 12f);
            var hudRect = new Rect(hudX, 12f, hudWidth, hudHeight);
            GUI.Box(hudRect, "Shooter GPU Instanced HUD");
            GUILayout.BeginArea(new Rect(hudRect.x + 12f, hudRect.y + 28f, hudRect.width - 24f, hudRect.height - 40f));
            for (var i = 0; i < _hudLines.Length; i++)
            {
                GUILayout.Label(_hudLines[i]);
            }

            GUILayout.EndArea();
        }

        private void UpdateHudLineCache()
        {
            var descriptor = ShooterUnityViewRenderBackendCatalog.Get(Backend);
            _hudLines[0].text = $"Backend: {descriptor.DisplayName}";
            _hudLines[1].text = $"批次 玩家/子弹/怪物: {_lastBatchPlayerCount}/{_lastBatchBulletCount}/{_lastBatchEnemyCount} remove={_lastBatchRemovedEntityCount}";
            _hudLines[2].text = $"投影 玩家/子弹/怪物: {_lastStorePlayerCount}/{_lastStoreBulletCount}/{_lastStoreEnemyCount} total={_clientProjection.Store.Entities.Count}";
            _hudLines[3].text = $"绘制 玩家/子弹/怪物: {_lastDrawPlayerCount}/{_lastDrawBulletCount}/{_lastDrawEnemyCount}";
            _hudLines[4].text = $"投影移除: total={_lastClientApplyResult.RemovedEntities} explicit={_lastClientApplyResult.ExplicitEntityRemovals} dead={_lastClientApplyResult.DeadEntityRemovals}";
            _hudLines[5].text = _lastControlledHp >= 0 ? $"主控HP: {_lastControlledHp}" : "主控HP: N/A";
            _hudLines[6].text = $"玩家实例: gpu={_lastDrawPlayerCount} deadSkip={_lastSkippedDeadPlayerCount} noTransform={_lastSkippedPlayerWithoutTransformCount} first=({_lastFirstPlayerPosition.x:0.00},{_lastFirstPlayerPosition.y:0.00},{_lastFirstPlayerPosition.z:0.00}) controlled={_lastHasControlledPlayerDraw} material=player";
            _hudLines[7].text = $"权威投影: {(_hasAuthorityProjection ? _authorityProjection.Store.Entities.Count.ToString() : "关闭")} draw={(DrawAuthorityOverlay ? "on" : "off")}";
            _hudLines[8].text = $"框架包/派发: {_lastCrossLayerDiagnostics.FrameworkPacketCount}/{_lastCrossLayerDiagnostics.FrameworkDispatchedSnapshotCount}";
            _hudLines[9].text = $"PureState 帧: apply={_lastCrossLayerDiagnostics.LastPureStateAppliedFrame} resync={_lastCrossLayerDiagnostics.LastPureStateResyncFrame}";
        }

        private static GUIContent[] CreateHudLineCache(int count)
        {
            var lines = new GUIContent[count];
            for (var i = 0; i < lines.Length; i++)
            {
                lines[i] = new GUIContent(string.Empty);
            }

            return lines;
        }

        private void CaptureHudCountsAndHealth(in ShooterSnapshotViewBatch batch, int controlledPlayerId)
        {
            var playerCount = 0;
            var bulletCount = 0;
            var enemyCount = 0;
            foreach (var entity in batch.EntityChanges)
            {
                if (!entity.Alive)
                {
                    continue;
                }

                switch (entity.Kind)
                {
                    case ShooterViewEntityKind.Player:
                        playerCount++;
                        break;
                    case ShooterViewEntityKind.Bullet:
                        bulletCount++;
                        break;
                    case ShooterViewEntityKind.Enemy:
                        enemyCount++;
                        break;
                }
            }

            _lastBatchPlayerCount = playerCount;
            _lastBatchBulletCount = bulletCount;
            _lastBatchEnemyCount = enemyCount;
            _lastBatchRemovedEntityCount = batch.RemovedEntityCount;
            _lastControlledHp = -1;
            if (controlledPlayerId <= 0)
            {
                return;
            }

            foreach (var change in batch.HealthChanges)
            {
                if (change.Key.Kind == ShooterViewEntityKind.Player && change.Key.EntityId == controlledPlayerId)
                {
                    _lastControlledHp = change.Hp;
                    return;
                }
            }
        }

        private static Quaternion CreateFacingRotation(float facingX, float facingY)
        {
            var direction = new Vector3(facingX, 0f, facingY);
            return direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : Quaternion.identity;
        }

        private static Vector3 ExtractTranslation(in Matrix4x4 matrix)
        {
            return new Vector3(matrix.m03, matrix.m13, matrix.m23);
        }

        private static Mesh CreatePrimitiveMesh(PrimitiveType primitiveType, string name)
        {
            var primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.hideFlags = HideFlags.HideAndDontSave;
            var source = primitive.GetComponent<MeshFilter>().sharedMesh;
            var mesh = UnityEngine.Object.Instantiate(source);
            mesh.name = name;
            mesh.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.Destroy(primitive);
            return mesh;
        }

        private static Material CreateMaterial(string name, Color color, Shader? preferredShader = null)
        {
            var shader = preferredShader ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader)
            {
                name = name,
                color = color,
                enableInstancing = true,
                hideFlags = HideFlags.HideAndDontSave
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            return material;
        }

        private static void DestroyRuntimeObject<T>(ref T? obj) where T : UnityEngine.Object
        {
            if (obj != null)
            {
                UnityEngine.Object.Destroy(obj);
                obj = null;
            }
        }

        private sealed class InstanceBuffer
        {
            public readonly List<Matrix4x4> Players = new();
            public readonly List<Matrix4x4> Bullets = new();
            public readonly List<Matrix4x4> Enemies = new();

            public bool HasControlledPlayer;
            public Matrix4x4 ControlledPlayerMatrix = Matrix4x4.identity;
            public int PlayerCount;
            public int BulletCount;
            public int EnemyCount;
            public int SkippedDeadPlayerCount;
            public int SkippedPlayerWithoutTransformCount;

            public void EnsureCapacity(int playerCount, int bulletCount, int enemyCount)
            {
                EnsureCapacity(Players, playerCount);
                EnsureCapacity(Bullets, bulletCount);
                EnsureCapacity(Enemies, enemyCount);
            }

            public void Clear()
            {
                Players.Clear();
                Bullets.Clear();
                Enemies.Clear();
                HasControlledPlayer = false;
                ControlledPlayerMatrix = Matrix4x4.identity;
                PlayerCount = 0;
                BulletCount = 0;
                EnemyCount = 0;
                SkippedDeadPlayerCount = 0;
                SkippedPlayerWithoutTransformCount = 0;
            }

            private static void EnsureCapacity(List<Matrix4x4> matrices, int capacity)
            {
                if (capacity > matrices.Capacity)
                {
                    matrices.Capacity = capacity;
                }
            }
        }

        private sealed class IndirectBufferSet : System.IDisposable
        {
            private readonly IndirectKindBuffer _players = new();
            private readonly IndirectKindBuffer _bullets = new();
            private readonly IndirectKindBuffer _enemies = new();
            private readonly IndirectKindBuffer _controlledPlayer = new();
            private readonly Matrix4x4[] _controlledMatrix = new Matrix4x4[1];

            public void Upload(
                Mesh? playerMesh,
                List<Matrix4x4> players,
                Mesh? bulletMesh,
                List<Matrix4x4> bullets,
                Mesh? enemyMesh,
                List<Matrix4x4> enemies,
                bool hasControlledPlayer,
                in Matrix4x4 controlledPlayerMatrix)
            {
                _players.Upload(playerMesh, players);
                _bullets.Upload(bulletMesh, bullets);
                _enemies.Upload(enemyMesh, enemies);
                if (hasControlledPlayer)
                {
                    _controlledMatrix[0] = controlledPlayerMatrix;
                    _controlledPlayer.Upload(playerMesh, _controlledMatrix, 1);
                }
                else
                {
                    _controlledPlayer.ClearCount();
                }
            }

            public void Draw(ShooterViewEntityKind kind, Material? material)
            {
                switch (kind)
                {
                    case ShooterViewEntityKind.Player:
                        _players.Draw(material);
                        break;
                    case ShooterViewEntityKind.Bullet:
                        _bullets.Draw(material);
                        break;
                    case ShooterViewEntityKind.Enemy:
                        _enemies.Draw(material);
                        break;
                }
            }

            public void DrawControlled(Material? material)
            {
                _controlledPlayer.Draw(material);
            }

            public void Dispose()
            {
                _players.Dispose();
                _bullets.Dispose();
                _enemies.Dispose();
                _controlledPlayer.Dispose();
            }
        }

        private sealed class IndirectKindBuffer : System.IDisposable
        {
            private static readonly int MatricesProperty = Shader.PropertyToID("_ShooterMatrices");
            private static readonly Bounds DrawBounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
            private readonly uint[] _arguments = new uint[5];
            private readonly MaterialPropertyBlock _properties = new();
            private ComputeBuffer? _matrices;
            private ComputeBuffer? _args;
            private int _capacity;
            private int _count;
            private Mesh? _mesh;

            public void Upload(Mesh? mesh, List<Matrix4x4> matrices)
            {
                if (mesh == null || matrices.Count == 0)
                {
                    _mesh = mesh;
                    ClearCount();
                    return;
                }

                EnsureCapacity(matrices.Count);
                _matrices!.SetData(matrices);
                UpdateArguments(mesh, matrices.Count);
            }

            public void Upload(Mesh? mesh, Matrix4x4[] matrices, int count)
            {
                if (mesh == null || count <= 0)
                {
                    _mesh = mesh;
                    ClearCount();
                    return;
                }

                EnsureCapacity(count);
                _matrices!.SetData(matrices, 0, 0, count);
                UpdateArguments(mesh, count);
            }

            public void ClearCount()
            {
                _count = 0;
            }

            public void Draw(Material? material)
            {
                if (_count <= 0 || _mesh == null || material == null || _args == null || _matrices == null)
                {
                    return;
                }

                _properties.SetBuffer(MatricesProperty, _matrices);
                Graphics.DrawMeshInstancedIndirect(
                    _mesh,
                    0,
                    material,
                    DrawBounds,
                    _args,
                    0,
                    _properties,
                    UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows: false);
            }

            public void Dispose()
            {
                _matrices?.Dispose();
                _matrices = null;
                _args?.Dispose();
                _args = null;
                _capacity = 0;
                _count = 0;
                _mesh = null;
            }

            private void EnsureCapacity(int count)
            {
                if (_capacity >= count && _matrices != null && _args != null)
                {
                    return;
                }

                var capacity = _capacity == 0 ? 16 : _capacity;
                while (capacity < count)
                {
                    capacity = checked(capacity * 2);
                }

                _matrices?.Dispose();
                _matrices = new ComputeBuffer(capacity, sizeof(float) * 16, ComputeBufferType.Structured);
                if (_args == null)
                {
                    _args = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
                }

                _capacity = capacity;
            }

            private void UpdateArguments(Mesh mesh, int count)
            {
                _mesh = mesh;
                _count = count;
                _arguments[0] = mesh.GetIndexCount(0);
                _arguments[1] = (uint)count;
                _arguments[2] = mesh.GetIndexStart(0);
                _arguments[3] = mesh.GetBaseVertex(0);
                _arguments[4] = 0;
                _args!.SetData(_arguments);
            }
        }

        private readonly struct DrawCounts
        {
            public DrawCounts(int playerCount, int bulletCount, int enemyCount)
            {
                PlayerCount = playerCount;
                BulletCount = bulletCount;
                EnemyCount = enemyCount;
            }

            public int PlayerCount { get; }

            public int BulletCount { get; }

            public int EnemyCount { get; }
        }

        private sealed class HudBehaviour : MonoBehaviour
        {
            private UnityShooterGpuInstancedViewSink? _sink;

            public void Initialize(UnityShooterGpuInstancedViewSink sink)
            {
                _sink = sink;
            }

            private void OnGUI()
            {
                _sink?.DrawHud();
            }
        }
    }
}
