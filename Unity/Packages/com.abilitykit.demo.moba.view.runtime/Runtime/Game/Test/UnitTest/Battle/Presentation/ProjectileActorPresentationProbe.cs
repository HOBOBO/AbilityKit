using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Flow;
using AbilityKit.Protocol.Moba;
using NUnit.Framework;
using UnityEngine;
using BattleNetId = AbilityKit.Game.Battle.Entity.BattleNetId;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>Acceptance-only observer. Never consumes or changes simulation state.</summary>
    internal sealed class ProjectileActorPresentationProbe : IDisposable
    {
        [Serializable]
        private sealed class Row
        {
            public string kind, resource;
            public int frame, renderFrame, actorId, projectileId, templateId, vfxId;
            public int attachmentActorId, followTargetActorId;
            public bool runtimeFound, actorFound, snapshotFound, projectionFound, visualFound, follows;
            public Vector3 runtime, actor, snapshot, projection, visual, visualScale;
            public float runtimeActorError, snapshotProjectionError, projectionVisualError;
            public double logicTime;
            public bool interpolatedFound, shellFound, targetViewFound;
            public Vector3 interpolated, targetView;
            public bool interpolationEnabled;
            public int tickRate, interpolationFrame;
            public double interpolationTime;
            public Vector3 pending;
            public float anchorVisualError;
        }

        private sealed class Flight
        {
            public int ProjectileId;
            public Vector3? FirstProjection, FirstVisual;
            public int Samples;
            public float ProjectionTravel, VisualTravel, MaxFollowError;
        }

        private readonly BattleContext _context;
        private readonly BattleViewBinder _binder;
        private readonly MobaActorRegistry _registry;
        private readonly IProjectileService _projectiles;
        private readonly int _templateId, _vfxId;
        private readonly string _path;
        private readonly Dictionary<int, Flight> _flights = new Dictionary<int, Flight>();
        private readonly Dictionary<int, Vector3> _snapshots = new Dictionary<int, Vector3>();
        private readonly List<string> _rows = new List<string>();
        private readonly IDisposable _events, _transforms;

        public ProjectileActorPresentationProbe(BattleContext context, MobaActorRegistry registry,
            IProjectileService projectiles, int templateId, int vfxId, string label, BattleViewBinder binder = null)
        {
            _context = context;
            _binder = binder;
            _registry = registry;
            _projectiles = projectiles;
            _templateId = templateId;
            _vfxId = vfxId;
            _path = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../local/Logs/projectile-flight-" + label + ".jsonl"));
            var resources = new BattleViewResourceProvider();
            var projectile = resources.TryGetProjectile(templateId);
            Assert.IsNotNull(projectile);
            Assert.AreEqual(vfxId, projectile.VfxId);
            Assert.IsTrue(resources.GetOrLoadVfxDb().TryGet(vfxId, out var vfx));
            var resource = string.IsNullOrEmpty(vfx.ResourceKey) ? vfx.Resource : vfx.ResourceKey;
            Assert.IsNotNull(Resources.Load<GameObject>(resource), "Configured projectile prefab is missing: " + resource);
            Assert.IsTrue(context.TryGetFrameSnapshots(out var dispatcher));
            _events = dispatcher.Subscribe<ProjectileEventData[]>(MobaOpCodes.Snapshot.ProjectileEvent,
                (_, entries) =>
                {
                    foreach (var entry in entries)
                    {
                        if (entry.TemplateId != _templateId) continue;
                        Assert.Greater(entry.ProjectileActorId, 0, "Projectile event lost its Actor identity.");
                        if (entry.Kind == ProjectilePresentationEventKind.Spawn && !_flights.ContainsKey(entry.ProjectileActorId))
                            _flights.Add(entry.ProjectileActorId, new Flight { ProjectileId = entry.ProjectileId });
                        Write(new Row { kind = entry.Kind.ToString(), actorId = entry.ProjectileActorId,
                            projectileId = entry.ProjectileId, runtime = new Vector3(entry.X, entry.Y, entry.Z) });
                    }
                });
            _transforms = dispatcher.Subscribe<ActorTransformData[]>(MobaOpCodes.Snapshot.ActorTransform,
                (_, entries) =>
                {
                    foreach (var entry in entries)
                        _snapshots[entry.ActorId] = new Vector3(entry.PositionX, entry.PositionY, entry.PositionZ);
                });
            Write(new Row { kind = "config", resource = resource });
        }

        public void Sample()
        {
            foreach (var pair in _flights)
            {
                var row = new Row { kind = "sample", actorId = pair.Key, projectileId = pair.Value.ProjectileId };
                if (_projectiles.TryGetRuntimeState(new ProjectileId(row.projectileId), out var runtime))
                {
                    row.runtimeFound = true;
                    row.runtime = new Vector3(runtime.Position.X, runtime.Position.Y, runtime.Position.Z);
                }
                if (_registry.TryGet(pair.Key, out var actor) && actor.hasTransform)
                {
                    row.actorFound = true;
                    var p = actor.transform.Value.Position;
                    row.actor = new Vector3(p.X, p.Y, p.Z);
                }
                row.snapshotFound = _snapshots.TryGetValue(pair.Key, out row.snapshot);
                if (_context.EntityQuery.TryGetTransform(new BattleNetId(pair.Key), out var transform))
                {
                    row.projectionFound = true;
                    row.projection = transform.Position;
                }
                row.logicTime = _context.LogicTimeSeconds;
                row.tickRate = _context.Plan.World.TickRate;
                row.interpolationEnabled = _binder?.InterpolationEnabled ?? false;
                if (_binder != null)
                {
                    const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    var transforms = typeof(BattleViewBinder).GetField("_transforms", flags).GetValue(_binder);
                    var clock = (BattleViewInterpolationClock)transforms.GetType().GetField("_clock", flags).GetValue(transforms);
                    row.interpolationTime = clock.RenderTime;
                    row.interpolationFrame = (int)typeof(BattleViewInterpolationClock).GetField("_lastFrame", flags).GetValue(clock);
                    var handles = (BattleViewHandleStore)typeof(BattleViewBinder).GetField("_handles", flags).GetValue(_binder);
                    if (_context.EntityQuery.TryResolve(new BattleNetId(pair.Key), out var sampled) && handles.TryGet(sampled.Id, out var handle)) row.pending = handle.PendingPos;
                }
                if (_context.EntityQuery.TryResolve(new BattleNetId(pair.Key), out var target))
                {
                    row.interpolatedFound = _binder != null && _binder.TryGetInterpolatedPos(target.Id, out row.interpolated);
                    row.shellFound = _binder != null && _binder.TryGetShellGameObject(target.Id, out _);
                    if (target.TryGetRef(out BattleViewGameObjectComponent targetView) && targetView?.GameObject != null)
                    { row.targetViewFound = true; row.targetView = targetView.GameObject.transform.position; }
                }
                _context.EntityWorld.ForEachAlive(entity =>
                {
                    if (!entity.TryGetRef(out BattleViewFollowComponent follow) || follow.TargetActorId != pair.Key ||
                        !entity.TryGetRef(out BattleVfxComponent effect) || effect.VfxId != _vfxId ||
                        !entity.TryGetRef(out BattleViewGameObjectComponent view) || view.GameObject == null) return;
                    row.visualFound = true;
                    row.visual = view.GameObject.transform.position;
                    row.visualScale = view.GameObject.transform.lossyScale;
                    row.followTargetActorId = follow.TargetActorId;
                    if (entity.TryGetRef(out BattlePresentationLifetimeComponent lifetime))
                    {
                        row.resource = lifetime.Request.ResourceKey;
                        row.attachmentActorId = lifetime.Request.Attachment.ActorId;
                        row.follows = row.attachmentActorId == pair.Key && lifetime.Request.FollowAttachment;
                    }
                });
                row.runtimeActorError = Vector3.Distance(row.runtime, row.actor);
                row.snapshotProjectionError = Vector3.Distance(row.snapshot, row.projection);
                row.projectionVisualError = Vector3.Distance(row.projection, row.visual);
                row.anchorVisualError = Vector3.Distance(row.interpolatedFound ? row.interpolated : row.projection, row.visual);
                Write(row);
                if (row.runtimeFound && row.projectionFound && row.visualFound)
                {
                    var flight = pair.Value;
                    flight.FirstProjection = flight.FirstProjection ?? row.projection;
                    flight.FirstVisual = flight.FirstVisual ?? row.visual;
                    flight.Samples++;
                    flight.ProjectionTravel = Mathf.Max(flight.ProjectionTravel,
                        Vector3.Distance(flight.FirstProjection.Value, row.projection));
                    flight.VisualTravel = Mathf.Max(flight.VisualTravel,
                        Vector3.Distance(flight.FirstVisual.Value, row.visual));
                    flight.MaxFollowError = Mathf.Max(flight.MaxFollowError, row.anchorVisualError);
                    Assert.IsTrue(row.follows, "Visual is not attached to its projectile Actor. " + _path);
                    Assert.Less(row.runtimeActorError, 0.01f, "Runtime -> Actor transform diverged. " + _path);
                    Assert.IsTrue(row.snapshotFound, "Moving projectile has no transform snapshot. " + _path);
                    Assert.Less(row.snapshotProjectionError, 0.01f, "Snapshot -> projection transform diverged. " + _path);
                }
            }
        }

        public bool HasFollowedFlight
        {
            get
            {
                foreach (var flight in _flights.Values)
                    if (flight.Samples >= 3 && flight.ProjectionTravel > 1f && flight.VisualTravel > 1f &&
                        flight.MaxFollowError < 0.05f) return true;
                return false;
            }
        }

        public void AssertFollowedFlight() => Assert.IsTrue(HasFollowedFlight,
            "No single projectile completed three Actor/VFX samples with >1m travel and <0.05m follow error. " + _path);

        private void Write(Row row)
        {
            row.frame = _context.LastFrame;
            row.renderFrame = Time.frameCount;
            row.templateId = _templateId;
            row.vfxId = _vfxId;
            _rows.Add(JsonUtility.ToJson(row));
        }

        public void Dispose()
        {
            _events.Dispose();
            _transforms.Dispose();
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllLines(_path, _rows);
            TestContext.WriteLine("Projectile Actor presentation samples: " + _path);
        }
    }
}
