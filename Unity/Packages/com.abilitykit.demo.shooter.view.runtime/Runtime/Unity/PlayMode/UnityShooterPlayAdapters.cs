#nullable enable

using System.Collections.Generic;
using AbilityKit.Demo.Shooter.View.Hosting;
using Unity.Profiling;
using UnityEngine;

namespace AbilityKit.Demo.Shooter.View.PlayMode
{
    internal sealed class UnityShooterPlayInputSource : IShooterHostInputSource
    {
        public ShooterHostFrameInput ReadInput(int controlledPlayerId)
        {
            var moveX = Input.GetAxisRaw("Horizontal");
            var moveY = Input.GetAxisRaw("Vertical");

            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) moveX -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) moveX += 1f;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) moveY -= 1f;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) moveY += 1f;

            return ShooterPlayInputMapping.CreateFrameInput(
                moveX,
                moveY,
                Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0) || Input.GetKey(KeyCode.J),
                Input.GetKey(KeyCode.K),
                Input.GetKey(KeyCode.L));
        }
    }

    internal sealed class UnityShooterGameObjectViewSink : IUnityShooterViewSink
    {
        private static readonly ProfilerMarker RenderStoreMarker = new ProfilerMarker("AbilityKit.Shooter.View.GameObject.RenderStore");
        private static readonly ProfilerMarker RenderDeltaMarker = new ProfilerMarker("AbilityKit.Shooter.View.GameObject.RenderDelta");

        private const string PlayerViewPrefabName = "ShooterPlayerViewPrefab";
        private const string BulletViewPrefabName = "ShooterBulletViewPrefab";
        private const string EnemyViewPrefabName = "ShooterEnemyViewPrefab";
        private readonly Dictionary<int, GameObject> _playerViews = new();
        private readonly Dictionary<int, GameObject> _bulletViews = new();
        private readonly Dictionary<int, GameObject> _enemyViews = new();
        private readonly Dictionary<int, GameObject> _authorityPlayerViews = new();
        private readonly Dictionary<int, GameObject> _authorityBulletViews = new();
        private readonly Dictionary<int, GameObject> _authorityEnemyViews = new();
        private readonly ShooterSnapshotViewProjection _clientProjection = new();
        private readonly ShooterSnapshotViewProjection _authorityProjection = new();
        private readonly HashSet<int> _seenPlayers = new();
        private readonly HashSet<int> _seenBullets = new();
        private readonly HashSet<int> _seenEnemies = new();
        private readonly Queue<GameObject> _playerPool = new();
        private readonly Queue<GameObject> _bulletPool = new();
        private readonly Queue<GameObject> _enemyPool = new();
        private readonly List<int> _staleViewIds = new();
        private readonly GUIContent[] _hudLines = CreateHudLineCache(13);
        private readonly MaterialPropertyBlock _tintPropertyBlock = new();
        private Transform? _viewRoot;
        private Transform? _clientRoot;
        private Transform? _authorityRoot;
        private Camera? _camera;
        private Light? _light;
        private HudBehaviour? _hudBehaviour;
        private GameObject? _playerPrefab;
        private GameObject? _bulletPrefab;
        private GameObject? _enemyPrefab;
        private int _lastControlledPlayerId;
        private float _lastWorldScale = 1f;
        private int _lastPlayerCount;
        private int _lastBulletCount;
        private int _lastEnemyCount;
        private int _lastControlledHp = -1;
        private bool _hasHudData;
        private bool _hudDirty;
        private bool _hasAuthorityProjection;
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
 
        public ShooterUnityViewRenderBackend Backend => ShooterUnityViewRenderBackend.GameObject;

        public ShooterUnityViewRenderDiagnostics Diagnostics => new ShooterUnityViewRenderDiagnostics(
            Backend,
            usesIndirectRendering: false,
            fullRebuildCount: 0L,
            incrementalBatchCount: 0L,
            indirectUploadPassCount: 0L,
            matrixUploadCallCount: 0L,
            uploadedMatrixCount: 0L,
            fullBufferUploadCount: 0L,
            partialUploadRangeCount: 0L,
            _playerViews.Count,
            _bulletViews.Count,
            _enemyViews.Count,
            _playerViews.ContainsKey(_lastControlledPlayerId));

        public void Render(in ShooterHostPresentationFrame frame)
        {
            EnsureViewRoot();
            var presentationScaleChanged = _lastControlledPlayerId != frame.ControlledPlayerId ||
                !Mathf.Approximately(_lastWorldScale, frame.WorldScale);
            _lastControlledPlayerId = frame.ControlledPlayerId;
            _lastWorldScale = frame.WorldScale;
            CaptureHudData(in frame);

            var clientBatch = frame.ClientBatch;
            var hadClientBatch = _hasAppliedClientBatch;
            var clientBatchChanged = !IsSameClientBatch(in clientBatch);
            if (clientBatchChanged)
            {
                _clientProjection.Apply(in clientBatch);
                CaptureClientBatchKey(in clientBatch);
            }

            if (clientBatchChanged || presentationScaleChanged)
            {
                if (!hadClientBatch || presentationScaleChanged || clientBatch.IsFullSnapshot)
                {
                    RenderStore(
                        _clientProjection.Store,
                        frame.ControlledPlayerId,
                        frame.WorldScale,
                        _playerViews,
                        _bulletViews,
                        _enemyViews,
                        _clientRoot,
                        isAuthority: false);
                }
                else
                {
                    RenderDelta(
                        in clientBatch,
                        _clientProjection.Store,
                        frame.ControlledPlayerId,
                        frame.WorldScale,
                        _playerViews,
                        _bulletViews,
                        _enemyViews,
                        _clientRoot,
                        isAuthority: false);
                }
            }

            if (frame.HasAuthorityBatch)
            {
                var authorityBatch = frame.AuthorityBatch;
                var hadAuthorityBatch = _hasAppliedAuthorityBatch;
                var authorityBatchChanged = !IsSameAuthorityBatch(in authorityBatch);
                if (authorityBatchChanged)
                {
                    _authorityProjection.Apply(in authorityBatch);
                    CaptureAuthorityBatchKey(in authorityBatch);
                }

                _hasAuthorityProjection = true;
                if (authorityBatchChanged || presentationScaleChanged)
                {
                    if (!hadAuthorityBatch || presentationScaleChanged || authorityBatch.IsFullSnapshot)
                    {
                        RenderStore(
                            _authorityProjection.Store,
                            frame.ControlledPlayerId,
                            frame.WorldScale,
                            _authorityPlayerViews,
                            _authorityBulletViews,
                            _authorityEnemyViews,
                            _authorityRoot,
                            isAuthority: true);
                    }
                    else
                    {
                        RenderDelta(
                            in authorityBatch,
                            _authorityProjection.Store,
                            frame.ControlledPlayerId,
                            frame.WorldScale,
                            _authorityPlayerViews,
                            _authorityBulletViews,
                            _authorityEnemyViews,
                            _authorityRoot,
                            isAuthority: true);
                    }
                }
            }
            else if (_hasAuthorityProjection || _hasAppliedAuthorityBatch ||
                _authorityPlayerViews.Count > 0 || _authorityBulletViews.Count > 0 || _authorityEnemyViews.Count > 0)
            {
                _hasAuthorityProjection = false;
                _hasAppliedAuthorityBatch = false;
                _authorityProjection.Clear();
                ClearViews(_authorityPlayerViews, _playerPool);
                ClearViews(_authorityBulletViews, _bulletPool);
                ClearViews(_authorityEnemyViews, _enemyPool);
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

        private void CaptureHudData(in ShooterHostPresentationFrame frame)
        {
            _lastPlayerCount = CountEntities(frame.ClientBatch, ShooterViewEntityKind.Player);
            _lastBulletCount = CountEntities(frame.ClientBatch, ShooterViewEntityKind.Bullet);
            _lastEnemyCount = CountEntities(frame.ClientBatch, ShooterViewEntityKind.Enemy);
            _lastControlledHp = TryGetControlledPlayerHp(frame.ClientBatch, frame.ControlledPlayerId);
            _lastCrossLayerDiagnostics = frame.CrossLayerDiagnostics;
            _hasHudData = true;
            _hudDirty = true;
        }

        private static int CountEntities(in ShooterSnapshotViewBatch batch, ShooterViewEntityKind kind)
        {
            var count = 0;
            foreach (var entity in batch.EntityChanges)
            {
                if (entity.Alive && entity.Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }

        private static int TryGetControlledPlayerHp(in ShooterSnapshotViewBatch batch, int controlledPlayerId)
        {
            if (controlledPlayerId <= 0)
            {
                return -1;
            }

            foreach (var change in batch.HealthChanges)
            {
                if (change.Key.Kind == ShooterViewEntityKind.Player && change.Key.EntityId == controlledPlayerId)
                {
                    return change.Hp;
                }
            }

            return -1;
        }

        public void RebuildAll()
        {
            EnsureViewRoot();
            ClearViews(_playerViews, _playerPool);
            ClearViews(_bulletViews, _bulletPool);
            ClearViews(_enemyViews, _enemyPool);
            ClearViews(_authorityPlayerViews, _playerPool);
            ClearViews(_authorityBulletViews, _bulletPool);
            ClearViews(_authorityEnemyViews, _enemyPool);

            RenderStore(
                _clientProjection.Store,
                _lastControlledPlayerId,
                _lastWorldScale,
                _playerViews,
                _bulletViews,
                _enemyViews,
                _clientRoot,
                isAuthority: false);

            if (_hasAuthorityProjection)
            {
                RenderStore(
                    _authorityProjection.Store,
                    _lastControlledPlayerId,
                    _lastWorldScale,
                    _authorityPlayerViews,
                    _authorityBulletViews,
                    _authorityEnemyViews,
                    _authorityRoot,
                    isAuthority: true);
            }
        }

        public void Clear()
        {
            ClearViews(_playerViews, _playerPool);
            ClearViews(_bulletViews, _bulletPool);
            ClearViews(_enemyViews, _enemyPool);
            ClearViews(_authorityPlayerViews, _playerPool);
            ClearViews(_authorityBulletViews, _bulletPool);
            ClearViews(_authorityEnemyViews, _enemyPool);

            _clientProjection.Clear();
            _authorityProjection.Clear();
            _lastClientSequence = 0ul;
            _lastAuthoritySequence = 0ul;
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
            _hasAuthorityProjection = false;
            _hasHudData = false;
            _hudDirty = false;
            _lastPlayerCount = 0;
            _lastBulletCount = 0;
            _lastEnemyCount = 0;
            _lastControlledHp = -1;
            _lastCrossLayerDiagnostics = default;
 
            if (_viewRoot != null)
            {
                Object.Destroy(_viewRoot.gameObject);
                _viewRoot = null;
                _clientRoot = null;
                _authorityRoot = null;
                _camera = null;
                _light = null;
                _hudBehaviour = null;
            }

            DestroyPrefab(ref _playerPrefab);
            DestroyPrefab(ref _bulletPrefab);
            DestroyPrefab(ref _enemyPrefab);
            _playerPool.Clear();
            _bulletPool.Clear();
            _enemyPool.Clear();
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
            GUI.Box(new Rect(12f, 12f, 360f, 316f), "Shooter HUD");
            GUILayout.BeginArea(new Rect(24f, 40f, 336f, 284f));
            for (var i = 0; i < _hudLines.Length; i++)
            {
                if (i == 6)
                {
                    GUILayout.Space(6f);
                }

                GUILayout.Label(_hudLines[i]);
            }

            GUILayout.EndArea();
        }

        private void RenderStore(
            ShooterViewEntityStore store,
            int controlledPlayerId,
            float worldScale,
            Dictionary<int, GameObject> playerViews,
            Dictionary<int, GameObject> bulletViews,
            Dictionary<int, GameObject> enemyViews,
            Transform? parent,
            bool isAuthority)
        {
            using var renderStoreSample = RenderStoreMarker.Auto();
            _seenPlayers.Clear();
            _seenBullets.Clear();
            _seenEnemies.Clear();

            for (var i = 0; i < store.DenseCount; i++)
            {
                if (!store.TryGetDenseEntityAndTransform(i, out var entity, out var transform) || !entity.Alive)
                {
                    continue;
                }

                if (entity.Kind == ShooterViewEntityKind.Player)
                {
                    _seenPlayers.Add(entity.EntityId);
                    var view = GetOrCreatePlayerView(playerViews, parent, entity.EntityId, controlledPlayerId, isAuthority);
                    view.transform.localPosition = new Vector3(transform.X * worldScale, isAuthority ? 0.15f : 0f, transform.Y * worldScale);
                    ApplyFacing(view.transform, transform.FacingX, transform.FacingY);
                }
                else if (entity.Kind == ShooterViewEntityKind.Bullet)
                {
                    _seenBullets.Add(entity.EntityId);
                    var view = GetOrCreateBulletView(bulletViews, parent, entity.EntityId, isAuthority);
                    view.transform.localPosition = new Vector3(transform.X * worldScale, isAuthority ? 0.15f : 0f, transform.Y * worldScale);
                }
                else if (entity.Kind == ShooterViewEntityKind.Enemy)
                {
                    _seenEnemies.Add(entity.EntityId);
                    var view = GetOrCreateEnemyView(enemyViews, parent, entity.EntityId, isAuthority);
                    view.transform.localPosition = new Vector3(transform.X * worldScale, isAuthority ? 0.15f : 0f, transform.Y * worldScale);
                    ApplyFacing(view.transform, transform.FacingX, transform.FacingY);
                }
            }

            PruneViews(playerViews, _seenPlayers, _playerPool);
            PruneViews(bulletViews, _seenBullets, _bulletPool);
            PruneViews(enemyViews, _seenEnemies, _enemyPool);
        }

        private void RenderDelta(
            in ShooterSnapshotViewBatch batch,
            ShooterViewEntityStore store,
            int controlledPlayerId,
            float worldScale,
            Dictionary<int, GameObject> playerViews,
            Dictionary<int, GameObject> bulletViews,
            Dictionary<int, GameObject> enemyViews,
            Transform? parent,
            bool isAuthority)
        {
            using var renderDeltaSample = RenderDeltaMarker.Auto();
            var removed = batch.RemovedEntities;
            for (var i = 0; i < removed.Count; i++)
            {
                ReleaseView(removed[i], playerViews, bulletViews, enemyViews);
            }

            var entityChanges = batch.EntityChanges;
            for (var i = 0; i < entityChanges.Count; i++)
            {
                var change = entityChanges[i];
                if (!change.Alive)
                {
                    ReleaseView(change.Key, playerViews, bulletViews, enemyViews);
                }
            }

            var transformChanges = batch.TransformChanges;
            for (var i = 0; i < transformChanges.Count; i++)
            {
                var key = transformChanges[i].Key;
                if (!store.TryGetEntity(key, out var entity) ||
                    !entity.Alive ||
                    !store.TryGetTransform(key, out var transform))
                {
                    continue;
                }

                RenderEntityTransform(
                    in entity,
                    in transform,
                    controlledPlayerId,
                    worldScale,
                    playerViews,
                    bulletViews,
                    enemyViews,
                    parent,
                    isAuthority);
            }
        }

        private void RenderEntityTransform(
            in ShooterViewEntityState entity,
            in ShooterViewTransformState transform,
            int controlledPlayerId,
            float worldScale,
            Dictionary<int, GameObject> playerViews,
            Dictionary<int, GameObject> bulletViews,
            Dictionary<int, GameObject> enemyViews,
            Transform? parent,
            bool isAuthority)
        {
            var position = new Vector3(transform.X * worldScale, isAuthority ? 0.15f : 0f, transform.Y * worldScale);
            if (entity.Kind == ShooterViewEntityKind.Player)
            {
                var view = GetOrCreatePlayerView(playerViews, parent, entity.EntityId, controlledPlayerId, isAuthority);
                view.transform.localPosition = position;
                ApplyFacing(view.transform, transform.FacingX, transform.FacingY);
            }
            else if (entity.Kind == ShooterViewEntityKind.Bullet)
            {
                GetOrCreateBulletView(bulletViews, parent, entity.EntityId, isAuthority).transform.localPosition = position;
            }
            else if (entity.Kind == ShooterViewEntityKind.Enemy)
            {
                var view = GetOrCreateEnemyView(enemyViews, parent, entity.EntityId, isAuthority);
                view.transform.localPosition = position;
                ApplyFacing(view.transform, transform.FacingX, transform.FacingY);
            }
        }

        private void ReleaseView(
            ShooterViewEntityKey key,
            Dictionary<int, GameObject> playerViews,
            Dictionary<int, GameObject> bulletViews,
            Dictionary<int, GameObject> enemyViews)
        {
            Dictionary<int, GameObject>? views = null;
            Queue<GameObject>? pool = null;
            if (key.Kind == ShooterViewEntityKind.Player)
            {
                views = playerViews;
                pool = _playerPool;
            }
            else if (key.Kind == ShooterViewEntityKind.Bullet)
            {
                views = bulletViews;
                pool = _bulletPool;
            }
            else if (key.Kind == ShooterViewEntityKind.Enemy)
            {
                views = enemyViews;
                pool = _enemyPool;
            }

            if (views != null && pool != null && views.TryGetValue(key.EntityId, out var view))
            {
                views.Remove(key.EntityId);
                ReleaseView(view, pool);
            }
        }

        private GameObject GetOrCreatePlayerView(
            Dictionary<int, GameObject> views,
            Transform? parent,
            int id,
            int controlledPlayerId,
            bool isAuthority)
        {
            if (views.TryGetValue(id, out var existing) && existing != null)
            {
                return existing;
            }

            var go = GetOrCreatePooledView(_playerPool, GetOrCreatePlayerPrefab(), parent);
            go.name = isAuthority ? $"ShooterAuthorityPlayer_{id}" : $"ShooterPlayer_{id}";
            go.transform.localScale = new Vector3(0.75f, 1.1f, 0.75f);
            TintRenderer(go, isAuthority ? new Color(1f, 0.35f, 0.35f, 0.55f) : id == controlledPlayerId ? Color.green : Color.cyan);
            views[id] = go;
            return go;
        }

        private GameObject GetOrCreateBulletView(Dictionary<int, GameObject> views, Transform? parent, int id, bool isAuthority)
        {
            if (views.TryGetValue(id, out var existing) && existing != null)
            {
                return existing;
            }

            var go = GetOrCreatePooledView(_bulletPool, GetOrCreateBulletPrefab(), parent);
            go.name = isAuthority ? $"ShooterAuthorityBullet_{id}" : $"ShooterBullet_{id}";
            go.transform.localScale = Vector3.one * (isAuthority ? 0.45f : 0.35f);
            TintRenderer(go, isAuthority ? new Color(1f, 0.65f, 0.15f, 0.55f) : Color.yellow);
            views[id] = go;
            return go;
        }

        private GameObject GetOrCreateEnemyView(Dictionary<int, GameObject> views, Transform? parent, int id, bool isAuthority)
        {
            if (views.TryGetValue(id, out var existing) && existing != null)
            {
                return existing;
            }

            var go = GetOrCreatePooledView(_enemyPool, GetOrCreateEnemyPrefab(), parent);
            go.name = isAuthority ? $"ShooterAuthorityEnemy_{id}" : $"ShooterEnemy_{id}";
            go.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);
            TintRenderer(go, isAuthority ? new Color(1f, 0f, 0.65f, 0.55f) : Color.red);
            views[id] = go;
            return go;
        }

        private void EnsureViewRoot()
        {
            if (_viewRoot != null)
            {
                return;
            }

            var root = new GameObject("ShooterPlayModeViews");
            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(root);
            }
            _viewRoot = root.transform;
            _hudBehaviour = root.AddComponent<HudBehaviour>();
            _hudBehaviour.Initialize(this);
            _clientRoot = new GameObject("Client").transform;
            _clientRoot.SetParent(_viewRoot, false);
            _authorityRoot = new GameObject("Authority").transform;
            _authorityRoot.SetParent(_viewRoot, false);

            var cameraObject = new GameObject("ShooterPlayModeCamera");
            cameraObject.transform.SetParent(_viewRoot, false);
            cameraObject.transform.localPosition = new Vector3(4f, 14f, -10f);
            cameraObject.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 14f;
            _camera.clearFlags = CameraClearFlags.Skybox;
            _camera.depth = 10f;

            var lightObject = new GameObject("ShooterPlayModeLight");
            lightObject.transform.SetParent(_viewRoot, false);
            lightObject.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);
            _light = lightObject.AddComponent<Light>();
            _light.type = LightType.Directional;
            _light.intensity = 1.2f;
        }

        private void ClearViews(Dictionary<int, GameObject> views, Queue<GameObject> pool)
        {
            foreach (var kvp in views)
            {
                ReleaseView(kvp.Value, pool);
            }

            views.Clear();
        }

        private GameObject GetOrCreatePlayerPrefab()
        {
            if (_playerPrefab != null)
            {
                return _playerPrefab;
            }

            _playerPrefab = CreateViewPrefab(PlayerViewPrefabName, PrimitiveType.Capsule);
            return _playerPrefab;
        }

        private GameObject GetOrCreateBulletPrefab()
        {
            if (_bulletPrefab != null)
            {
                return _bulletPrefab;
            }

            _bulletPrefab = CreateViewPrefab(BulletViewPrefabName, PrimitiveType.Sphere);
            return _bulletPrefab;
        }

        private GameObject GetOrCreateEnemyPrefab()
        {
            if (_enemyPrefab != null)
            {
                return _enemyPrefab;
            }

            _enemyPrefab = CreateViewPrefab(EnemyViewPrefabName, PrimitiveType.Cube);
            return _enemyPrefab;
        }

        private static GameObject CreateViewPrefab(string name, PrimitiveType primitiveType)
        {
            var prefab = GameObject.CreatePrimitive(primitiveType);
            prefab.name = name;
            prefab.hideFlags = HideFlags.HideAndDontSave;
            prefab.SetActive(false);
            return prefab;
        }

        private void PruneViews(Dictionary<int, GameObject> views, HashSet<int> alive, Queue<GameObject> pool)
        {
            if (views.Count == 0)
            {
                return;
            }

            _staleViewIds.Clear();
            foreach (var kvp in views)
            {
                if (!alive.Contains(kvp.Key))
                {
                    _staleViewIds.Add(kvp.Key);
                }
            }

            for (var i = 0; i < _staleViewIds.Count; i++)
            {
                ReleaseView(views[_staleViewIds[i]], pool);
                views.Remove(_staleViewIds[i]);
            }
        }

        private void UpdateHudLineCache()
        {
            _hudLines[0].text = $"战场 玩家/子弹/怪物: {_lastPlayerCount}/{_lastBulletCount}/{_lastEnemyCount}";
            _hudLines[1].text = _lastControlledHp >= 0 ? $"主控HP: {_lastControlledHp}" : "主控HP: N/A";
            _hudLines[2].text = $"客户端视图 玩家/子弹/怪物: {_playerViews.Count}/{_bulletViews.Count}/{_enemyViews.Count}";
            _hudLines[3].text = $"权威视图 玩家/子弹/怪物: {_authorityPlayerViews.Count}/{_authorityBulletViews.Count}/{_authorityEnemyViews.Count}";
            _hudLines[4].text = $"池化 玩家/子弹/怪物: {_playerPool.Count}/{_bulletPool.Count}/{_enemyPool.Count}";
            _hudLines[5].text = $"权威投影: {(_hasAuthorityProjection ? "开启" : "关闭")}";
            _hudLines[6].text = $"框架包/派发: {_lastCrossLayerDiagnostics.FrameworkPacketCount}/{_lastCrossLayerDiagnostics.FrameworkDispatchedSnapshotCount}";
            _hudLines[7].text = $"快照 Packed/Pure: {_lastCrossLayerDiagnostics.FrameworkPackedSnapshotCount}/{_lastCrossLayerDiagnostics.FrameworkPureStateSnapshotCount}";
            _hudLines[8].text = $"最近框架帧: {_lastCrossLayerDiagnostics.LastFrameworkFrame} op={_lastCrossLayerDiagnostics.LastFrameworkPayloadOpCode}";
            _hudLines[9].text = $"框架世界: {(_lastCrossLayerDiagnostics.HasFrameworkSnapshot ? _lastCrossLayerDiagnostics.LastFrameworkWorldId.ToString() : "N/A")}";
            _hudLines[10].text = _lastCrossLayerDiagnostics.HasSnapshotApplyResult
                ? $"网关应用: {_lastCrossLayerDiagnostics.SnapshotApplyResult}"
                : "网关应用: N/A";
            _hudLines[11].text = _lastCrossLayerDiagnostics.HasRemoteLatencyResult
                ? $"远端延迟/权威差: {_lastCrossLayerDiagnostics.RemoteInputDelayFrames}/{_lastCrossLayerDiagnostics.RemoteAuthoritativeFrameGap}f"
                : "远端延迟/权威差: N/A";
            _hudLines[12].text = $"PureState 帧: apply={_lastCrossLayerDiagnostics.LastPureStateAppliedFrame} resync={_lastCrossLayerDiagnostics.LastPureStateResyncFrame} baseline={(_lastCrossLayerDiagnostics.NeedsPureStateBaselineResync ? "等待" : "正常")}";
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

        private sealed class HudBehaviour : MonoBehaviour
        {
            private UnityShooterGameObjectViewSink? _sink;

            public void Initialize(UnityShooterGameObjectViewSink sink)
            {
                _sink = sink;
            }

            private void OnGUI()
            {
                _sink?.DrawHud();
            }
        }

        private static void ApplyFacing(Transform transform, float facingX, float facingY)
        {
            var direction = new Vector3(facingX, 0f, facingY);
            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        private GameObject GetOrCreatePooledView(Queue<GameObject> pool, GameObject prefab, Transform? parent)
        {
            while (pool.Count > 0)
            {
                var pooled = pool.Dequeue();
                if (pooled == null)
                {
                    continue;
                }

                pooled.transform.SetParent(parent, false);
                pooled.SetActive(true);
                return pooled;
            }

            var go = Object.Instantiate(prefab, parent);
            go.SetActive(true);
            return go;
        }

        private void ReleaseView(GameObject? go, Queue<GameObject> pool)
        {
            if (go == null)
            {
                return;
            }

            go.SetActive(false);
            go.transform.SetParent(_viewRoot, false);
            pool.Enqueue(go);
        }

        private static void DestroyPrefab(ref GameObject? prefab)
        {
            if (prefab != null)
            {
                Object.Destroy(prefab);
                prefab = null;
            }
        }

        private void TintRenderer(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                renderer.GetPropertyBlock(_tintPropertyBlock);
                _tintPropertyBlock.SetColor("_Color", color);
                renderer.SetPropertyBlock(_tintPropertyBlock);
                _tintPropertyBlock.Clear();
            }
        }
    }
}
