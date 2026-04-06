using System;
using System.Collections.Generic;
using AbilityKit.World.ECS;
using EC = AbilityKit.World.ECS;
using UnityEngine;

namespace AbilityKit.Game.EntityDebug
{
    [ExecuteAlways]
    public sealed class EntityDebugVisualizer : MonoBehaviour
    {
        private EC.IECWorld _world;
        private readonly Dictionary<EC.IEntityId, GameObject> _views = new Dictionary<EC.IEntityId, GameObject>();
        private GameObject _root;
        private IDisposable _entityCreatedSub;
        private IDisposable _entityDestroyedSub;
        private IDisposable _parentChangedSub;
        private IDisposable _componentSetSub;
        private IDisposable _componentRemovedSub;

        private void OnEnable()
        {
            TryAttach();
        }

        private void OnDisable()
        {
            Detach();
        }

        private void Update()
        {
            if (!IsEnabled())
            {
                Detach();
                return;
            }

            if (_world == null)
            {
                TryAttach();
            }
        }

        private void TryAttach()
        {
            if (!IsEnabled()) return;
            if (!GameEntry.IsInitialized) return;

            var entry = GameEntry.Instance;
            if (entry.World == null) return;

            if (!ReferenceEquals(_world, entry.World))
            {
                Detach();
                _world = entry.World;

                _entityCreatedSub = _world.EntityCreated(OnEntityCreated);
                _entityDestroyedSub = _world.EntityDestroyed(OnEntityDestroyed);
                _parentChangedSub = _world.ParentChanged(OnParentChanged);
                _componentSetSub = _world.ComponentSet(OnComponentChanged);
                _componentRemovedSub = _world.ComponentRemoved(OnComponentRemoved);

                EnsureRootContainer();
                RebuildAll();
            }
        }

        private void Detach()
        {
            _entityCreatedSub?.Dispose();
            _entityDestroyedSub?.Dispose();
            _parentChangedSub?.Dispose();
            _componentSetSub?.Dispose();
            _componentRemovedSub?.Dispose();

            _world = null;

            foreach (var kv in _views)
            {
                if (kv.Value != null)
                {
                    DestroyImmediate(kv.Value);
                }
            }
            _views.Clear();

            if (_root != null)
            {
                DestroyImmediate(_root);
                _root = null;
            }
        }

        private void EnsureRootContainer()
        {
            if (_root != null) return;
            _root = new GameObject("EC_Dbg_Root");
            _root.hideFlags = HideFlags.DontSave;
            _root.transform.SetParent(transform, false);
        }

        private void RebuildAll()
        {
            if (_world == null) return;
            if (!GameEntry.IsInitialized) return;

            var entry = GameEntry.Instance;
            var rootEntity = entry.Root;

            _world.ForEachAlive(e => EnsureView(e.Id));

            foreach (var kv in _views)
            {
                var id = kv.Key;
                if (id == rootEntity.Id)
                {
                    kv.Value.transform.SetParent(_root.transform, false);
                    UpdateViewName(id, default);
                    continue;
                }

                var e = _world.Wrap(id);
                if (e.TryGetParent(out var p) && _views.TryGetValue(p.Id, out var parentGo) && parentGo != null)
                {
                    kv.Value.transform.SetParent(parentGo.transform, false);
                    UpdateViewName(id, p.Id);
                }
                else
                {
                    kv.Value.transform.SetParent(_root.transform, false);
                    UpdateViewName(id, default);
                }
            }
        }

        private void OnEntityCreated(EC.EntityCreated evt)
        {
            var e = evt.Entity;
            EnsureRootContainer();
            EnsureView(e.Id);

            if (GameEntry.IsInitialized && e.Id == GameEntry.Instance.Root.Id)
            {
                _views[e.Id].transform.SetParent(_root.transform, false);
                UpdateViewName(e.Id, default);
                return;
            }

            if (e.TryGetParent(out var parent) && _views.TryGetValue(parent.Id, out var parentGo) && parentGo != null)
            {
                _views[e.Id].transform.SetParent(parentGo.transform, false);
                UpdateViewName(e.Id, parent.Id);
            }
            else
            {
                _views[e.Id].transform.SetParent(_root.transform, false);
                UpdateViewName(e.Id, default);
            }
        }

        private void OnEntityDestroyed(EC.EntityDestroyed evt)
        {
            var id = evt.EntityId;
            if (_views.TryGetValue(id, out var go) && go != null)
            {
                DestroyImmediate(go);
            }
            _views.Remove(id);
        }

        private void OnParentChanged(EC.ParentChanged evt)
        {
            var child = evt.ChildId;
            var newParent = evt.NewParentId;
            if (_views.TryGetValue(child, out var childGo) == false || childGo == null) return;

            if (newParent.Version != 0 && _views.TryGetValue(newParent, out var parentGo) && parentGo != null)
            {
                childGo.transform.SetParent(parentGo.transform, false);
                UpdateViewName(child, newParent);
            }
            else
            {
                EnsureRootContainer();
                childGo.transform.SetParent(_root.transform, false);
                UpdateViewName(child, default);
            }
        }

        private void OnComponentChanged(EC.ComponentSet evt)
        {
            var entity = evt.EntityId;
            if (_views.TryGetValue(entity, out var go) && go != null)
            {
                var view = go.GetComponent<EntityComponentView>();
                if (view != null) view.MarkDirty();
                UpdateViewName(entity, GetCurrentParent(entity));
            }
        }

        private void OnComponentRemoved(EC.ComponentRemoved evt)
        {
            OnComponentChanged(new EC.ComponentSet(evt.EntityId, evt.ComponentTypeId, null));
        }

        private GameObject EnsureView(EC.IEntityId id)
        {
            if (_views.TryGetValue(id, out var go) && go != null) return go;

            var created = new GameObject($"Entity_{id.Index}_{id.Version}");
            created.hideFlags = HideFlags.DontSave;

            var entityView = created.AddComponent<EntityView>();
            entityView.Bind(_world, id);

            var compView = created.AddComponent<EntityComponentView>();
            compView.Bind(entityView);

            _views[id] = created;
            return created;
        }

        private bool IsEnabled()
        {
            return GameEntry.IsInitialized && GameEntry.Instance.DebugEnabled;
        }

        private EC.IEntityId GetCurrentParent(EC.IEntityId entity)
        {
            if (_world == null || !_world.IsAlive(entity)) return default;
            var e = _world.Wrap(entity);
            return e.TryGetParent(out var p) ? p.Id : default;
        }

        private void UpdateViewName(EC.IEntityId entity, EC.IEntityId parent)
        {
            if (!_views.TryGetValue(entity, out var go) || go == null) return;

            var baseName = $"Entity_{entity.Index}_{entity.Version}";
            var friendly = EntityDebugNameUtility.GetEntityName(_world, entity);

            string childIdText = null;
            if (parent.Version != 0 && _world != null && _world.TryGetChildId(parent, entity, out var childId))
            {
                childIdText = $"[{childId}]";
            }

            if (!string.IsNullOrEmpty(friendly))
            {
                go.name = childIdText == null ? $"{baseName} {friendly}" : $"{baseName} {childIdText} {friendly}";
            }
            else
            {
                go.name = childIdText == null ? baseName : $"{baseName} {childIdText}";
            }
        }
    }
}
