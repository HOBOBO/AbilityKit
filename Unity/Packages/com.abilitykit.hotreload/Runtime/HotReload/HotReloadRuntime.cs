#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World;

namespace AbilityKit.Ability.HotReload
{
    /// <summary>Coordinates staged hotfix replacement for Entitas worlds.</summary>
    public static class HotReloadRuntime
    {
        private sealed class WorldState
        {
            public HotfixSystemProxy? Proxy;
            public HotfixServiceOverlay? Overlay;
            public IHotfixEntry? CurrentEntry;
            public global::Entitas.Systems? CurrentFeature;
        }

        private static int _transitionActive;
        private static readonly ConditionalWeakTable<IEntitasWorld, WorldState> States =
            new ConditionalWeakTable<IEntitasWorld, WorldState>();

        /// <summary>Stages and applies an entry without committing a failed candidate feature.</summary>
        public static bool Apply(IEntitasWorld? world, IHotfixEntry? entry, out string? error)
        {
            error = null;
            if (world == null)
            {
                error = "world is null";
                return false;
            }
            if (entry == null)
            {
                error = "entry is null";
                return false;
            }

            if (!TryEnterTransition(out error))
                return false;

            try
            {
                var state = States.GetValue(world, _ => new WorldState());
                return ApplyLocked(world, state, entry, out error);
            }
            finally
            {
                Volatile.Write(ref _transitionActive, 0);
            }
        }

        private static bool ApplyLocked(IEntitasWorld world, WorldState state, IHotfixEntry entry, out string? error)
        {
            error = null;

            try
            {
                HotReloadStaticRegistry.ResetAll();
            }
            catch (Exception e)
            {
                error = FormatError("static reset", e);
                return false;
            }

            try
            {
                EnsureProxy(world, state);
            }
            catch (Exception e)
            {
                error = FormatError("proxy setup", e);
                return false;
            }

            var overlay = new HotfixServiceOverlay(world.Services);
            var feature = new global::Entitas.Systems();
            try
            {
                entry.Install(world.Contexts, feature, overlay);
            }
            catch (Exception e)
            {
                overlay.Clear();
                error = FormatApplyFailure("install", e, feature);
                return false;
            }

            try
            {
                feature.Initialize();
            }
            catch (Exception e)
            {
                overlay.Clear();
                error = FormatApplyFailure("initialize", e, feature);
                return false;
            }

            if (state.CurrentEntry != null)
            {
                try
                {
                    state.CurrentEntry.Uninstall(world.Contexts, world.Systems, state.Overlay!);
                }
                catch (Exception e)
                {
                    overlay.Clear();
                    error = FormatApplyFailure("uninstall previous entry", e, feature);
                    return false;
                }
            }

            if (state.CurrentFeature != null)
            {
                try
                {
                    state.CurrentFeature.TearDown();
                }
                catch (Exception e)
                {
                    overlay.Clear();
                    error = FormatApplyFailure("tear down previous feature", e, feature);
                    return false;
                }
            }

            state.Overlay?.Clear();
            state.Proxy!.SetCurrent(feature);
            state.Overlay = overlay;
            state.CurrentEntry = entry;
            state.CurrentFeature = feature;
            return true;
        }

        /// <summary>Detaches and releases all hotfix state owned by one world.</summary>
        public static bool ReleaseWorld(IEntitasWorld? world, out string? error)
        {
            error = null;
            if (world == null)
            {
                error = "world is null";
                return false;
            }

            if (!TryEnterTransition(out error))
                return false;

            try
            {
                if (!States.TryGetValue(world, out var state) || state == null)
                    return true;

                List<Exception>? errors = null;
                state.Proxy?.SetCurrent(null);

                if (state.CurrentEntry != null)
                {
                    try
                    {
                        state.CurrentEntry.Uninstall(world.Contexts, world.Systems, state.Overlay!);
                    }
                    catch (Exception e)
                    {
                        AddError(ref errors, e);
                    }
                }

                if (state.CurrentFeature != null)
                {
                    try
                    {
                        state.CurrentFeature.TearDown();
                    }
                    catch (Exception e)
                    {
                        AddError(ref errors, e);
                    }
                }

                state.Overlay?.Clear();
                state.CurrentEntry = null;
                state.CurrentFeature = null;
                state.Overlay = null;
                state.Proxy = null;
                States.Remove(world);

                if (errors == null)
                    return true;

                error = FormatError("release world", new AggregateException(errors));
                return false;
            }
            finally
            {
                Volatile.Write(ref _transitionActive, 0);
            }
        }

        private static void EnsureProxy(IEntitasWorld world, WorldState state)
        {
            if (state.Proxy != null)
                return;

            var proxy = new HotfixSystemProxy(() =>
            {
                if (!ReleaseWorld(world, out var releaseError))
                    throw new InvalidOperationException(releaseError);
            });
            world.Systems.Add(proxy);
            state.Proxy = proxy;
        }

        private static bool TryEnterTransition(out string? error)
        {
            if (Interlocked.CompareExchange(ref _transitionActive, 1, 0) == 0)
            {
                error = null;
                return true;
            }

            error = "A concurrent or reentrant hot reload transition is already active.";
            return false;
        }

        private static string FormatApplyFailure(
            string stage,
            Exception primaryError,
            global::Entitas.Systems candidateFeature)
        {
            try
            {
                candidateFeature?.TearDown();
            }
            catch (Exception cleanupError)
            {
                return FormatError(stage, new AggregateException(primaryError, cleanupError));
            }

            return FormatError(stage, primaryError);
        }

        private static void AddError(ref List<Exception>? errors, Exception error)
        {
            if (errors == null)
                errors = new List<Exception>();
            errors.Add(error);
        }

        private static string FormatError(string stage, Exception error)
        {
            return $"Hot reload {stage} failed: {error}";
        }
    }
}
