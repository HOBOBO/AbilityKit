# AbilityKit Core Boundary

`AbilityKit.Core` is the cross-runtime foundation shared by Unity, .NET servers,
tests, and command-line tools. Small size is not the goal; stable ownership and
portable contracts are.

## Assembly boundary

- `AbilityKit.Core` has no Unity engine references.
- `AbilityKit.Core.Unity` contains Unity-specific adapters and may depend on the
  foundation assembly.
- The .NET project compiles the same foundation sources and excludes the Unity
  adapter directory.
- Unity Collections, Burst, Jobs, and engine object types must not enter the
  foundation assembly.

Use `Unity.Collections.NativeArray<T>` in Unity/Jobs packages. Shared managed
code can use `PooledBufferOwner<T>` when buffer ownership crosses a method,
asynchronous operation, or storage boundary. Do not create a second type named
`NativeArray` in Core.

## Inclusion rule

A new Core primitive should meet all of these conditions:

1. It has at least three independent framework consumers.
2. Its behavior can be specified without gameplay or platform terminology.
3. Ownership, mutation, ordering, and failure behavior are explicit.
4. It can be tested directly in `AbilityKit.Core.Tests`.
5. It preserves the Unity and .NET compilation boundary.

Core is not a general-purpose utilities package. Prefer a domain package when a
type has only one subsystem consumer, carries gameplay policy, or depends on a
specific scheduler, serializer, transport, engine, or native allocator.

## Public API compatibility

The .NET Core build uses `Microsoft.CodeAnalysis.PublicApiAnalyzers` as the
machine-readable compatibility gate:

- `src/AbilityKit.Core/PublicAPI.Shipped.txt` is the reviewed released surface.
- `src/AbilityKit.Core/PublicAPI.Unshipped.txt` contains additions planned for
  the next release.
- `RS0016` blocks public symbols that have not been intentionally registered.
- `RS0017` blocks accidental removal or signature changes of registered APIs.

When adding an API, first keep the implementation internal until its contract
and consumers are known. Then add the final public signature to Unshipped and
tests for its ownership, ordering, failure, and compatibility behavior. At
release time, move reviewed Unshipped entries to Shipped. Do not regenerate the
entire baseline to make a compatibility failure disappear.

Removing or changing a shipped API requires a deprecation path, consumer
migration, and the package's documented major-version policy. A compatibility
shim is preferred when persisted data, generated code, or independently
versioned packages may still reference the old surface.

## Extracted infrastructure

Debug drawing is diagnostics infrastructure. New consumers use
`AbilityKit.Diagnostics.DebugDraw` and Unity editor adapters use
`AbilityKit.Diagnostics.Editor.DebugDraw`. The old `AbilityKit.Core.Debugging`
surface remains obsolete compatibility API until the next major version.

Disposal policy stays with the resource owner because exception handling,
release order, reentrancy, and logging are lifecycle decisions. Do not add new
consumers of `AbilityKit.Core.Utilities.DisposeUtils`; use an owner-local helper
or an owned lifetime abstraction with an explicit contract.

`MarkerAttribute`, `MarkerScanner`, and the registry types remain compatibility
building blocks while active consumers are migrated. `MarkerSystem` and the
three global bootstrapper base classes are frozen: assembly-wide scanning and
static registration side effects must move to an owner-controlled discovery or
generated/AOT registration package.

The boundary audit rejects new consumers of these migrated entry points and
rejects DebugDraw contract declarations outside `com.abilitykit.diagnostics`.

## Stable identifier hashing

`StableHashV1` is a versioned serialization contract, not a replaceable hash
helper. Its outputs must remain identical across Unity, .NET, platforms, and
future package versions:

- `Fnv1a32Utf16` mixes each UTF-16 code unit and preserves the original Core
  string-ID behavior.
- `Fnv1a32Utf16NonNegative` applies the same algorithm and clears the sign bit;
  it preserves the Triggering identifier domain.
- `Fnv1a32Utf8` hashes the UTF-8 byte sequence and replaces invalid surrogate
  sequences with U+FFFD; it preserves the Record identifier domain.

Do not switch an existing consumer between these methods. ASCII strings happen
to produce the same value, while non-ASCII and supplementary characters can
produce different values. Any future algorithm or semantic change must use a
new explicitly versioned type, include golden vectors, and provide migration or
dual-read rules for persisted and wire-visible identifiers.

`StableStringIdRegistry` also detects collisions within one registry. Hashing a
string alone does not prove global uniqueness; persisted protocols should keep
the original name, schema namespace, or another collision-resolution contract
where collisions cannot be tolerated.

## Stable priority collections

`StablePriorityList<T>` is for small registration pipelines. It guarantees:

- ascending or descending integer priority;
- original registration order for equal priorities;
- binary-search insertion instead of sorting the entire list after each add;
- explicit first-match removal and priority updates.

The collection is not thread-safe. Callers must not add, remove, update, or clear
items while enumerating or invoking directly over the list.

It is not a replacement for domain-specific Top-K selection, snapshot buffers,
or parallel key/item sorting. Those algorithms retain their domain-specific
implementations and tie-break rules.

## Sorted integer indexes

`SortedIntSet` is a narrow allocation-aware index for small integer domains. It
guarantees unique ascending values, binary-search insertion and lookup, explicit
lower/upper bounds, and contiguous range removal. It is intentionally indexed
rather than enumerable so hot-path callers can use a non-allocating `for` loop.

The type owns only integer membership and order. Domain buffers still own their
values, locking, duplicate-value replacement, retention windows, defensive
copies, and inclusive/exclusive range policy. Current consumers are StateSync
snapshot and input buffers, StateManager rollback alignment, entity prediction
snapshots, the FrameSync command buffer, the remote frame buffer, and
RemoteFrameAggregator. They synchronize the index together with their
corresponding value map.

The `core-collections` runtime benchmark retains the previous list-sort/temporary
trim-list workload as a baseline. On the 2026-08-14 local Release smoke run with
128 out-of-order frames, the baseline measured 367.57 ns and 3.375 allocated
bytes per index mutation versus 38.80 ns and zero allocated bytes for
`SortedIntSet`. These measurements are admission evidence, not a performance
budget; compare future runs only under matching workload and environment data.

## Managed pooled buffers

`PooledBufferOwner<T>` is the portable ownership boundary for arrays rented from
`System.Buffers.ArrayPool<T>`. It provides:

- an exact logical `Length` even when the physical rented array is larger;
- logical `Memory`, `Span`, and `ArraySegment` views;
- explicit `OnRent` and `OnReturn` full-array clearing policies;
- idempotent, thread-safe disposal that returns the array at most once;
- use-after-dispose detection when requesting a new view.

The owner is a sealed reference type intentionally. A disposable value type can
be copied and each copy can return the same array, so it cannot provide reliable
single-owner behavior. Previously captured views still reference the rented
array and become invalid as soon as the owner is disposed; they must never be
stored or used after the ownership scope ends.

Use this owner for connection-level scratch buffers, queued work, retained ring
slots, and other lifetimes where ownership needs to be represented explicitly.
For a short synchronous hot path that already has a local `try/finally`, direct
`ArrayPool<T>.Rent/Return` avoids allocating an owner object and remains the
preferred form. Pooling small arrays without profiling evidence is discouraged.

## Monotonic timing

`IMonotonicClock` is the portable contract for elapsed-time measurement and
deadline checks. `StopwatchMonotonicClock` is the process-local default, while
`MonotonicTime` centralizes timestamp reads and overflow-safe conversion:

- timestamps never move backwards for one clock instance;
- a timestamp is meaningful only with the `Frequency` from the same clock;
- `DurationToTimestampTicks` rounds positive durations up so a deadline cannot
  expire earlier than requested;
- `GetMilliseconds` and `ToMilliseconds` represent monotonic elapsed units, not
  UTC, local time, Unix time, or a value suitable for persistence.

Do not compare timestamps produced by different clock instances unless they
explicitly document a shared origin and frequency. Persist wall-clock instants
with a domain-owned UTC contract instead.

Core does not own scheduling, timers, retry/backoff policy, frame progression,
simulation time, replay time, or Unity player-loop integration. `IWorldClock`,
`IFrameClock`, `IReplayClock`, and pipeline time sources therefore remain in
their domain packages. Network Host retains its original `IMonotonicClock` and
`StopwatchMonotonicClock` names as source-compatible adapters; new cross-package
APIs should depend on `AbilityKit.Core.Timing.IMonotonicClock`.

Use `System.Threading.CancellationToken` at asynchronous cancellation boundaries.
A Core wrapper is not justified until multiple packages require the same extra
state transition, ownership, and callback semantics; renaming the platform
token alone would add indirection without adding a contract.

## Guard, result, and diagnostics boundaries

Core does not provide a general `Guard` helper. Standard argument checks and
standard exception types are clearer at the call site unless at least three
packages require the same validation rule, exception type, parameter naming,
and message contract. A helper that only wraps `ArgumentNullException` or
`ArgumentOutOfRangeException` adds indirection without standardizing behavior.

Core also does not provide a general `Result<T>`. Trigger execution, flow state,
rollback, network negotiation, recovery, and wire protocol results have
different state machines, error codes, merge rules, and serialization
constraints. They remain domain types. A result contract may enter Core only
when independent packages share the same failure states, error-code ownership,
and persistence or wire compatibility rules.

Diagnostic and validation records follow the same rule. Severity names such as
error, warning, and information are not enough to establish a shared contract:
some diagnostics are mutable merge outputs, some are immutable startup gates,
and others carry navigation, revision, health, or domain payload. Do not add a
universal diagnostic severity or issue type until producers and consumers share
identity, ordering, localization, transport, and failure-gating semantics.

## Disposable registrations

`DisposableRegistration` adapts a release callback to `IDisposable`. Disposal
atomically claims the callback before invoking it, so concurrent or repeated
calls execute it at most once. A callback exception is propagated to the caller,
but the registration remains released. The state overload avoids an extra
capturing-closure allocation when a consumer already has a small state holder.

The registration owns only callback execution. It does not add locking around
the callback, make its target thread-safe, swallow failures, control release
order, or keep a composite collection. Snapshot routing, World ECS, and
Triggering retain those domain policies while sharing the one-shot mechanism.
Use a domain-owned group when multiple registrations require reverse-order
release, error aggregation, removal, or lifecycle state transitions.

Existing `IEventSubscription` interfaces remain domain-owned. Some expose
`Unsubscribe` instead of `Dispose`, and some also release the registered handler
or notify a lifecycle observer. The shared one-shot mechanism is not evidence
that those public contracts have identical ownership semantics.

## Application settings and reflection ownership

Configuration source, persistence paths, serializer choice, and optional module
installation are application/bootstrap policy. They do not meet the Core
inclusion rule merely because their implementation is small or reusable.

The current MOBA ownership is explicit:

- `AbilityKit.Demo.Moba.View.Settings` is owned by
  `com.abilitykit.demo.moba.view.runtime`. Pure layered settings live under the
  nested game-flow assembly; file and Unity persistence adapters live in the
  outer view runtime.
- `AbilityKit.Demo.Moba.Bootstrap` is owned by
  `com.abilitykit.demo.moba.runtime`. Reflection is exposed only through the
  narrow `ModuleInstallerInvoker.TryInvoke(ModuleInstallerConfig)` operation.

Production packages must not consume `AbilityKit.Core.Configuration` or
`AbilityKit.Core.Reflection`, and non-Core .NET projects must not link those
source directories directly. The old Core types remain obsolete compatibility
APIs until the next major-version removal window. Compatibility tests may still
exercise them so their published signatures do not drift during that window.

## Package namespace ownership

`AbilityKit.Core.*` namespaces are owned by `com.abilitykit.core`. Domain
packages must use package-owned namespaces even when they depend on Core value
types. The boundary audit rejects new declarations outside the Core package.

Five historical areas remain temporarily allowlisted with exact directories
and file-count ceilings: collision mathematics, navigation mathematics, record,
Unity pooling adapters, and snapshot routing. The allowance is a decreasing
migration baseline, not permission to add more files. Their target namespaces
are `AbilityKit.Combat.Collision`, `AbilityKit.Combat.Navigation`,
`AbilityKit.Record`, `AbilityKit.Unity.Pooling`, and
`AbilityKit.World.Snapshot.Routing` respectively.

## Thread-safety contract

Thread safety is explicit and opt-in. A type is not thread-safe unless its API
or this document says otherwise.

| Area | Contract |
| --- | --- |
| Stable hashes and immutable mathematics value types | No shared mutable state; safe to call concurrently. |
| `PooledBufferOwner<T>` | `Dispose` is idempotent and returns at most once. Buffer access must not race with disposal; captured views are invalid after disposal. |
| `StablePriorityList<T>` | Not thread-safe; mutation and enumeration require one owner or external synchronization. |
| `SortedIntSet` | Not thread-safe; keep it under the same lock or owner as the domain values it indexes. |
| `DisposableRegistration` | Concurrent `Dispose` calls are supported and invoke the release callback at most once. The callback and its target retain their own thread-safety requirements. |
| `EventDispatcher` and `StableStringIdRegistry` | Not thread-safe; subscribe/register/publish/unsubscribe from one execution context or guard the whole operation externally. |
| MOBA View flat and layered settings stores | Not thread-safe; build or update on one owner thread, then publish an immutable snapshot to readers. |
| `ObjectPool<T>` | Stack, counters, and duplicate-return tracking are synchronized. User callbacks execute while the pool lock is held, so callbacks must be bounded and must not introduce reverse lock ordering. Pooled objects themselves are not made thread-safe. |
| Global logging, marker, and dispatcher state | Treat registration/configuration as startup work. Runtime mutation requires external synchronization and a documented lifecycle boundary. |

Synchronization wrappers belong near the concurrency owner rather than in Core
unless at least three consumers need exactly the same atomicity and lifecycle
semantics. This prevents a locally locked primitive from implying that a larger
multi-step workflow is atomic.

## Next foundation candidates

Evaluate future additions in this order, and only after concrete consumers and
benchmarks exist:

1. Other small ownership and lifetime primitives where at least three packages
   duplicate the same lease or state-transition contract. Do not promote a
   domain composite merely because each item is disposable.
2. Serialization-independent version ranges or tokens only after wire,
   persistence, and replay consumers share comparison, source, and compatibility
   semantics. Similar `Version`, `Revision`, or `Generation` names are not enough.
3. Guard, result, and diagnostics contracts only if future consumers meet the
   stricter shared-behavior rules above.

Concurrency collections, jobified/native storage, serialization frameworks,
dependency injection, ECS, networking, and gameplay lifecycle policy are not
Core concerns. They should depend on Core through narrow contracts.

## Migration candidates

The following existing areas remain source-compatible for now but should be
reviewed through a deprecation cycle rather than expanded in place:

- `Continuous`: extracted to the dependency-free `com.abilitykit.continuous`
  package and the `AbilityKit.Continuous` namespace. Core retains only the
  obsolete compatibility implementation until the next major-version removal
  window; boundary auditing prevents production consumers from returning to it.
- `Numerics`: deprecated gameplay modifier compatibility surface. The only
  framework consumer has migrated to the MOBA-owned `DamageNumberValue`; keep
  the old API source-compatible until the next major-version removal window.
- Configuration and reflection: MOBA consumers now use owner-package settings
  and bootstrap APIs. Core retains obsolete compatibility types until the next
  major-version removal window; new consumers are blocked by boundary audit.
- marker scanning: candidate host/discovery service.
- collision shapes: owned by collision abstractions and should eventually use a
  collision/geometry namespace instead of `AbilityKit.Core.Mathematics`.
- `AbilityKit.Threading.PooledArray<T>` and `PooledMemory`: copyable disposable
  structs can return one array more than once; deprecate through a compatibility
  cycle in favor of Core ownership or a local zero-allocation `try/finally`.

Public namespace moves require compatibility shims, downstream migration, and a
major-version removal window. They are intentionally separate from correctness
fixes in the foundation.
