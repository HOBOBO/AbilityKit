# AbilityKit Continuous

`com.abilitykit.continuous` owns the domain model and runtime coordination for
long-lived, owner-scoped processes. It has no dependency on `com.abilitykit.core`.

The package provides lifecycle state and end reasons, continuous/config/manager
contracts, admission policies, lifecycle binders, owner indexes, and the default
manager implementation. Gameplay-specific tags, modifiers, timers, and
presentation bindings remain in their owning packages.

Use the `AbilityKit.Continuous` namespace. The former
`AbilityKit.Core.Continuous` API is retained only as a deprecated compatibility
surface until the next major-version removal window.
