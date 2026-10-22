using System;
using System.Collections.Generic;
using System.Globalization;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Core.Recording.FrameRecord;

namespace AbilityKit.Demo.Moba.Services.StateSync
{
    /// <summary>
    /// Exports a stable diagnostic view of deterministic MOBA state. This is an offline/debug artifact.
    /// </summary>
    public sealed class MobaCanonicalStateExporter
    {
        public const int SchemaVersion = 1;
        public const int PhaseState = 1;
        public const int ComponentTransform = 1001;
        public const int ComponentResources = 1002;
        public const int FieldTransformPositionX = 1;
        public const int FieldTransformPositionY = 2;
        public const int FieldTransformPositionZ = 3;
        public const int FieldTransformRotationX = 4;
        public const int FieldTransformRotationY = 5;
        public const int FieldTransformRotationZ = 6;
        public const int FieldTransformRotationW = 7;
        public const int FieldTransformScaleX = 8;
        public const int FieldTransformScaleY = 9;
        public const int FieldTransformScaleZ = 10;
        public const int FieldResourceCurrent = 1;
        public const int FieldResourceLastMax = 2;
        public const int FieldResourceMaxAttribute = 3;

        private readonly MobaActorRegistry _registry;
        private readonly string _worldId;
        private readonly string _buildId;
        private readonly string _configHash;
        private readonly List<CanonicalStateEntry> _entries = new List<CanonicalStateEntry>(128);

        public MobaCanonicalStateExporter(MobaActorRegistry registry, string worldId, string buildId = "", string configHash = "")
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _worldId = worldId ?? string.Empty;
            _buildId = buildId ?? string.Empty;
            _configHash = configHash ?? string.Empty;
        }

        public CanonicalStateFile Capture(FrameIndex frame)
        {
            _entries.Clear();
            var actorIds = _registry.CopyActorIdsInOrder();
            for (var i = 0; i < actorIds.Count; i++)
            {
                var actorId = actorIds[i];
                if (!_registry.TryGet(actorId, out var actor) || actor == null) continue;
                if (actor.hasTransform) AddTransform(frame.Value, actorId, actor.transform.Value);
                if (actor.hasResourceContainer && actor.resourceContainer.Value?.Map != null)
                    AddResources(frame.Value, actorId, actor.resourceContainer.Value.Map);
            }

            return new CanonicalStateFile
            {
                SchemaVersion = SchemaVersion,
                WorldId = _worldId,
                BuildId = _buildId,
                ConfigHash = _configHash,
                Entries = new List<CanonicalStateEntry>(_entries),
            };
        }

        private void AddTransform(int frame, int actorId, in Transform3 transform)
        {
            AddFloat(frame, actorId, ComponentTransform, FieldTransformPositionX, "PositionX", transform.Position.X);
            AddFloat(frame, actorId, ComponentTransform, FieldTransformPositionY, "PositionY", transform.Position.Y);
            AddFloat(frame, actorId, ComponentTransform, FieldTransformPositionZ, "PositionZ", transform.Position.Z);
            AddFloat(frame, actorId, ComponentTransform, FieldTransformRotationX, "RotationX", transform.Rotation.X);
            AddFloat(frame, actorId, ComponentTransform, FieldTransformRotationY, "RotationY", transform.Rotation.Y);
            AddFloat(frame, actorId, ComponentTransform, FieldTransformRotationZ, "RotationZ", transform.Rotation.Z);
            AddFloat(frame, actorId, ComponentTransform, FieldTransformRotationW, "RotationW", transform.Rotation.W);
            AddFloat(frame, actorId, ComponentTransform, FieldTransformScaleX, "ScaleX", transform.Scale.X);
            AddFloat(frame, actorId, ComponentTransform, FieldTransformScaleY, "ScaleY", transform.Scale.Y);
            AddFloat(frame, actorId, ComponentTransform, FieldTransformScaleZ, "ScaleZ", transform.Scale.Z);
        }

        private void AddResources(int frame, int actorId, Dictionary<ResourceType, ResourceState> map)
        {
            var resourceTypes = new List<ResourceType>(map.Keys);
            resourceTypes.Sort();
            for (var i = 0; i < resourceTypes.Count; i++)
            {
                var resourceType = resourceTypes[i];
                if (!map.TryGetValue(resourceType, out var resource) || resource == null) continue;
                var componentId = ComponentResources + (int)resourceType;
                AddFixed(frame, actorId, componentId, FieldResourceCurrent, "CurrentRaw", resource.Current.RawValue);
                AddFixed(frame, actorId, componentId, FieldResourceLastMax, "LastMaxRaw", resource.LastMax.RawValue);
                AddSigned(frame, actorId, componentId, FieldResourceMaxAttribute, "MaxAttributeId", resource.MaxAttribute.RawId);
            }
        }

        private void AddFloat(int frame, int actorId, int componentId, int fieldId, string fieldName, float value)
        {
            var bits = unchecked((uint)BitConverter.SingleToInt32Bits(value));
            Add(frame, actorId, componentId, fieldId, fieldName, CanonicalValueKind.Unsigned,
                bits.ToString(CultureInfo.InvariantCulture), value.ToString("R", CultureInfo.InvariantCulture));
        }

        private void AddFixed(int frame, int actorId, int componentId, int fieldId, string fieldName, long raw)
        {
            Add(frame, actorId, componentId, fieldId, fieldName, CanonicalValueKind.FixedRaw,
                raw.ToString(CultureInfo.InvariantCulture), string.Empty);
        }

        private void AddSigned(int frame, int actorId, int componentId, int fieldId, string fieldName, int value)
        {
            Add(frame, actorId, componentId, fieldId, fieldName, CanonicalValueKind.Signed,
                value.ToString(CultureInfo.InvariantCulture), value.ToString(CultureInfo.InvariantCulture));
        }

        private void Add(int frame, int actorId, int componentId, int fieldId, string fieldName,
            CanonicalValueKind valueKind, string rawValue, string displayValue)
        {
            _entries.Add(new CanonicalStateEntry
            {
                Frame = frame, PhaseId = PhaseState, EntityId = actorId, ComponentId = componentId, FieldId = fieldId,
                ComponentName = componentId >= ComponentResources ? "Resource" : "Transform",
                FieldName = fieldName, ValueKind = valueKind, RawValue = rawValue, DisplayValue = displayValue,
            });
        }
    }
}
