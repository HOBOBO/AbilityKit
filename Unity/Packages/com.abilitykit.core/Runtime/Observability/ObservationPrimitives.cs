using System;

namespace AbilityKit.Core.Observability
{
    /// <summary>Identifies one runtime object incarnation without owning its lifecycle.</summary>
    public readonly struct RuntimeObjectKey : IEquatable<RuntimeObjectKey>
    {
        /// <summary>Creates a key from a non-zero runtime identifier and non-negative generation.</summary>
        public RuntimeObjectKey(long runtimeId, int generation = 0)
        {
            RuntimeId = runtimeId;
            Generation = generation;
        }

        /// <summary>Gets the runtime identifier. Zero is reserved for an invalid key.</summary>
        public long RuntimeId { get; }

        /// <summary>Gets the incarnation number used when a runtime identifier is reused.</summary>
        public int Generation { get; }

        /// <summary>Gets whether the key contains a non-zero identifier and non-negative generation.</summary>
        public bool IsValid => RuntimeId != 0L && Generation >= 0;

        /// <inheritdoc />
        public bool Equals(RuntimeObjectKey other)
        {
            return RuntimeId == other.RuntimeId && Generation == other.Generation;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is RuntimeObjectKey other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (RuntimeId.GetHashCode() * 397) ^ Generation;
            }
        }

        /// <summary>Determines whether two keys identify the same runtime object incarnation.</summary>
        public static bool operator ==(RuntimeObjectKey left, RuntimeObjectKey right) => left.Equals(right);

        /// <summary>Determines whether two keys identify different runtime object incarnations.</summary>
        public static bool operator !=(RuntimeObjectKey left, RuntimeObjectKey right) => !left.Equals(right);
    }

    /// <summary>References an immutable domain definition by domain-owned kind and identifier.</summary>
    public readonly struct ObservationDefinitionRef : IEquatable<ObservationDefinitionRef>
    {
        /// <summary>Creates a definition reference.</summary>
        public ObservationDefinitionRef(int kind, long id)
        {
            Kind = kind;
            Id = id;
        }

        /// <summary>Gets the domain-owned definition kind. Zero is reserved as unspecified.</summary>
        public int Kind { get; }

        /// <summary>Gets the domain-owned definition identifier. Zero is reserved as unspecified.</summary>
        public long Id { get; }

        /// <summary>Gets whether both kind and identifier are specified.</summary>
        public bool IsValid => Kind != 0 && Id != 0L;

        /// <inheritdoc />
        public bool Equals(ObservationDefinitionRef other)
        {
            return Kind == other.Kind && Id == other.Id;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is ObservationDefinitionRef other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (Kind * 397) ^ Id.GetHashCode();
            }
        }

        /// <summary>Determines whether two references identify the same definition.</summary>
        public static bool operator ==(ObservationDefinitionRef left, ObservationDefinitionRef right) => left.Equals(right);

        /// <summary>Determines whether two references identify different definitions.</summary>
        public static bool operator !=(ObservationDefinitionRef left, ObservationDefinitionRef right) => !left.Equals(right);
    }

    /// <summary>Provides optional root, context, and parent identifiers for observation correlation.</summary>
    public readonly struct ObservationTraceRef : IEquatable<ObservationTraceRef>
    {
        /// <summary>Creates a trace reference. Root or context must be non-zero for the value to be valid.</summary>
        public ObservationTraceRef(long rootId, long contextId, long parentId = 0L)
        {
            RootId = rootId;
            ContextId = contextId;
            ParentId = parentId;
        }

        /// <summary>Gets the root correlation identifier.</summary>
        public long RootId { get; }

        /// <summary>Gets the current context identifier.</summary>
        public long ContextId { get; }

        /// <summary>Gets the optional parent context identifier.</summary>
        public long ParentId { get; }

        /// <summary>Gets whether a root or current context identifier is present.</summary>
        public bool IsValid => RootId != 0L || ContextId != 0L;

        /// <inheritdoc />
        public bool Equals(ObservationTraceRef other)
        {
            return RootId == other.RootId && ContextId == other.ContextId && ParentId == other.ParentId;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is ObservationTraceRef other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = RootId.GetHashCode();
                hashCode = (hashCode * 397) ^ ContextId.GetHashCode();
                hashCode = (hashCode * 397) ^ ParentId.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>Determines whether two references contain the same correlation identifiers.</summary>
        public static bool operator ==(ObservationTraceRef left, ObservationTraceRef right) => left.Equals(right);

        /// <summary>Determines whether two references contain different correlation identifiers.</summary>
        public static bool operator !=(ObservationTraceRef left, ObservationTraceRef right) => !left.Equals(right);
    }

    /// <summary>Receives value-type observation events synchronously.</summary>
    public interface IObservationSink<TEvent> where TEvent : struct
    {
        /// <summary>Gets whether the sink currently intends to accept events.</summary>
        bool IsEnabled { get; }

        /// <summary>Attempts to write an event and returns whether the sink accepted it.</summary>
        bool TryWrite(in TEvent value);
    }

    /// <summary>Reusable disabled sink that rejects every event without retaining it.</summary>
    public sealed class NullObservationSink<TEvent> : IObservationSink<TEvent> where TEvent : struct
    {
        /// <summary>Gets the singleton disabled sink for this event type.</summary>
        public static NullObservationSink<TEvent> Instance { get; } = new NullObservationSink<TEvent>();

        private NullObservationSink()
        {
        }

        /// <inheritdoc />
        public bool IsEnabled => false;

        /// <inheritdoc />
        public bool TryWrite(in TEvent value) => false;
    }
}
