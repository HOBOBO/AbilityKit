# AbilityKit Compute

`AbilityKit.Compute` defines portable batch-kernel, backend-chain, validation, fallback,
and observation contracts. It does not install a global backend and does not replace a
business package's managed semantics.

Platform acceleration is selected explicitly at the application composition root. A
backend may decline small batches or fail, after which the chain tries the next backend.
Business validation runs before accelerated output is accepted.

The optional Unity Jobs implementation lives in `com.abilitykit.compute.unityjobs`.
