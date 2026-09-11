# AbilityKit Compute Unity Jobs

This package is optional infrastructure. Installing it does not change any AbilityKit
business package's default implementation.

- `UnityJobPlan` tracks reusable dependency graphs without forcing completion, so business
  extensions can keep multi-stage pipelines in native memory. Business extensions define
  concrete `[BurstCompile]` job structs and keep their concrete `.Schedule(...)` calls in the
  business assembly so Unity Jobs/Burst can generate reflection and Player AOT data.
- `UnityComputeBuffer<T>` gives those extensions explicit persistent-buffer ownership.

Business code owns semantic validation and its managed fallback. Complete every plan using a
buffer before resizing or disposing that buffer.

There is deliberately no generic `IJobParallelFor` or schedule-delegate bridge. Unity
Collections and Burst cannot reliably generate Player reflection/AOT data for jobs hidden
behind generic methods or delegate dispatch.
