using System;
using AbilityKit.Combat.MotionSystem.Constraints;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Motion;
using Newtonsoft.Json;

namespace AbilityKit.Demo.Moba.Rollback
{
    internal sealed class MobaManagedMotionCheckpoint
    {
        [JsonProperty(Required = Required.Always)] public string SourceType;
        [JsonProperty(Required = Required.Always)] public int ModifierSourceId;
        [JsonProperty(Required = Required.AllowNull)] public MobaFixedDeltaCheckpoint FixedDelta;

        internal static MobaManagedMotionCheckpoint Capture(MobaMotionContinuousRuntime runtime) =>
            new MobaManagedMotionCheckpoint
            {
                SourceType = runtime.LocalMotionSource?.GetType().FullName ?? "null",
                ModifierSourceId = runtime.ModifierSourceId,
                FixedDelta = runtime.LocalMotionSource is FixedDeltaMotionSource source
                    ? MobaFixedDeltaCheckpoint.From(source.CaptureExactState()) : null,
            };

        internal void Validate(MobaContinuousRuntimeBase runtime)
        {
            if (!(runtime is MobaMotionContinuousRuntime motion))
                throw new InvalidOperationException("Motion checkpoint belongs to a non-Motion Continuous.");
            motion.ValidateLocalMotionBinding();
            if (ModifierSourceId <= 0 || ModifierSourceId != motion.ModifierSourceId)
                throw new InvalidOperationException("Managed Motion checkpoint does not identify the same surviving Continuous instance.");
            if (SourceType != typeof(FixedDeltaMotionSource).FullName || FixedDelta == null)
                throw new InvalidOperationException($"Unsupported or missing managed Motion checkpoint: {SourceType}.");
            var state = FixedDelta.ToState();
            if (!state.Active) throw new InvalidOperationException("Managed Motion target source is not active.");
            ((FixedDeltaMotionSource)motion.LocalMotionSource).ValidateExactState(in state);
        }

        internal void Restore(MobaContinuousRuntimeBase runtime)
        {
            Validate(runtime);
            var state = FixedDelta.ToState();
            ((FixedDeltaMotionSource)((MobaMotionContinuousRuntime)runtime).LocalMotionSource).RestoreExactState(in state);
        }
    }

    internal sealed class MobaFixedDeltaCheckpoint
    {
        [JsonProperty(Required = Required.Always)] public long TimeLeftRaw;
        [JsonProperty(Required = Required.Always)] public bool Active;
        [JsonProperty(Required = Required.Always)] public float X;
        [JsonProperty(Required = Required.Always)] public float Y;
        [JsonProperty(Required = Required.Always)] public float Z;
        [JsonProperty(Required = Required.Always)] public int GroupId;
        [JsonProperty(Required = Required.Always)] public int Priority;
        [JsonProperty(Required = Required.Always)] public int Stacking;
        [JsonProperty(Required = Required.Always)] public bool HasCollisionPolicy;
        [JsonProperty(Required = Required.Always)] public MobaMotionCollisionCheckpoint CollisionPolicy;
        [JsonProperty(Required = Required.Always)] public bool HasCompletionCollisionPolicy;
        [JsonProperty(Required = Required.Always)] public MobaMotionCollisionCheckpoint CompletionCollisionPolicy;

        internal static MobaFixedDeltaCheckpoint From(FixedDeltaMotionExactState state) => new MobaFixedDeltaCheckpoint
        {
            TimeLeftRaw = state.TimeLeftRaw, Active = state.Active,
            X = state.Velocity.X, Y = state.Velocity.Y, Z = state.Velocity.Z,
            GroupId = state.GroupId, Priority = state.Priority, Stacking = (int)state.Stacking,
            HasCollisionPolicy = state.HasCollisionPolicy, CollisionPolicy = MobaMotionCollisionCheckpoint.From(state.CollisionPolicy),
            HasCompletionCollisionPolicy = state.HasCompletionCollisionPolicy,
            CompletionCollisionPolicy = MobaMotionCollisionCheckpoint.From(state.CompletionCollisionPolicy),
        };

        internal FixedDeltaMotionExactState ToState() => new FixedDeltaMotionExactState(
            TimeLeftRaw, Active, new Vec3(X, Y, Z), GroupId, Priority, (MotionStacking)Stacking,
            HasCollisionPolicy, CollisionPolicy.ToValue(), HasCompletionCollisionPolicy, CompletionCollisionPolicy.ToValue());
    }

    internal sealed class MobaMotionCollisionCheckpoint
    {
        [JsonProperty(Required = Required.Always)] public bool Enable;
        [JsonProperty(Required = Required.Always)] public bool AllowPassThrough;
        [JsonProperty(Required = Required.Always)] public int EndOverlapPolicy;
        [JsonProperty(Required = Required.Always)] public float Radius;
        [JsonProperty(Required = Required.Always)] public float Skin;
        [JsonProperty(Required = Required.Always)] public int ObstacleMask;
        [JsonProperty(Required = Required.Always)] public int IgnoreMask;
        [JsonProperty(Required = Required.Always)] public int SelectionKey;
        [JsonProperty(Required = Required.Always)] public bool SlideAlongWalls;
        [JsonProperty(Required = Required.Always)] public int MaxSlideIterations;
        [JsonProperty(Required = Required.Always)] public float WallSlideSpeedRecovery;

        internal static MobaMotionCollisionCheckpoint From(MotionCollisionConstraints value) => new MobaMotionCollisionCheckpoint
        {
            Enable = value.Enable, AllowPassThrough = value.AllowPassThrough, EndOverlapPolicy = (int)value.EndOverlapPolicy,
            Radius = value.Radius, Skin = value.Skin, ObstacleMask = value.ObstacleMask, IgnoreMask = value.IgnoreMask,
            SelectionKey = value.SelectionKey, SlideAlongWalls = value.SlideAlongWalls,
            MaxSlideIterations = value.MaxSlideIterations, WallSlideSpeedRecovery = value.WallSlideSpeedRecovery,
        };

        internal MotionCollisionConstraints ToValue()
        {
            if (!Enum.IsDefined(typeof(MotionEndOverlapPolicy), EndOverlapPolicy) ||
                !Finite(Radius) || Radius < 0 || !Finite(Skin) || Skin < 0 ||
                !Finite(WallSlideSpeedRecovery) || WallSlideSpeedRecovery < 0 || WallSlideSpeedRecovery > 1 ||
                MaxSlideIterations < 0 || SlideAlongWalls && MaxSlideIterations == 0)
                throw new InvalidOperationException("Invalid managed Motion collision constraints.");
            // Preserve default(struct), including zero iterations; the constructor normally clamps it.
            if (MaxSlideIterations == 0)
            {
                if (Enable || AllowPassThrough || EndOverlapPolicy != 0 || Radius != 0 || Skin != 0 ||
                    ObstacleMask != 0 || IgnoreMask != 0 || SelectionKey != 0 || SlideAlongWalls || WallSlideSpeedRecovery != 0)
                    throw new InvalidOperationException("Invalid default Motion collision constraints.");
                return default;
            }
            return new MotionCollisionConstraints(Enable, AllowPassThrough, (MotionEndOverlapPolicy)EndOverlapPolicy,
                Radius, Skin, ObstacleMask, IgnoreMask, SlideAlongWalls, MaxSlideIterations, WallSlideSpeedRecovery, SelectionKey);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
