# Prediction view instances

`BattleSessionFeature` now exposes an opt-in side view for the remote-driven prediction world:

```csharp
var shown = battleSession.ShowPredictionView("comparison-left", new Vector3(30f, 0f, 0f));
// Later, while the session is active:
battleSession.HidePredictionView("comparison-left");
```

Other worlds can supply their own `IBattleProjectionViewSource` (stable `WorldId` and
`TryGetProjection(out producer, out frame)`) and register a view with
`ShowProjectionView(instanceId, source, worldOffset)`. Each instance reads its own
source; `TryGetProjectionView(instanceId, out info)` returns its role, source world,
offset, and effective capabilities. `GetProjectionViews(buffer)` lists all active instances, and
`RebindProjectionViews()` refreshes their binders. `HideProjectionView` removes either
role. A source that is temporarily
unavailable clears its projected actors while retaining the instance registration.

Projected views default to actor binding only. Pass
`BattleProjectionViewCapabilities` to the `ShowProjectionView` or
`ShowPredictionView` overload to add VFX, area effects, floating text, events, or
camera control. Selecting `Events` also installs the VFX, area, and floating text
handlers required by the event sink. Projected views do not subscribe to the main
battle's snapshot or trigger stream.

A source may implement `IBattleProjectionViewEpochSource` to report a new epoch
when its world is rebuilt, including when the same producer object is reused.
The instance then clears old actors and transient effects before repopulating.
To deliver events, implement `IBattleProjectionViewEventSource.SubscribeEvents`.
The subscription receives the instance ID, world offset, epoch, and current frame;
the source must map positional events into that instance's offset. The returned
subscription is disposed when the source becomes unavailable, its epoch changes,
or the instance closes. Registration performs an initial sync so the subscription
is active before the next presentation tick. Requesting `Events` without an event
source returns `false`.

Call `ShowPredictionView` after the remote-driven world starts. It returns `false` when
client prediction or its actor projection producer is unavailable, or when the ID is
already in use. Distinct IDs can be shown at the same time. The offset is applied to
the side view's projected actor positions; the primary view stays at its normal
coordinates. `PredictionViewCount` reports prediction-role instances;
`ProjectionViewCount` reports all projected instances.

Each side view owns a separate view `EntityWorld`, actor lookup, feature runtime,
resource provider, hierarchy root, and view pools. The feature scheduler may tick
multiple projected views of the same type; the instance registry is the lookup for
all of them. The confirmed feature has its own CLR type, so type-based debug lookup
cannot mistake a projected instance for a confirmed one. Disposing the remote-driven
world during recovery clears predicted actors but retains view registrations; the
views repopulate when their sources become available again. Auxiliary views keep
their actors. Source availability is checked during the presentation tick.
Stopping the battle session disposes every projected view instance.

The projection uses `IActorProjectionProducer.ExtractAll` each presentation
tick and `ExtractSpawn` for newly seen actors. It creates and removes view entities,
updates transform, team, health, and model identity, then lets the existing view
binder render them without interpolation. The existing local-player prediction bridge
still drives the primary view.

Current scope: `ActorProjectionData` does not carry animation or skill cues.
An event-enabled source must supply them through its own event subscription;
actor projection alone displays only actor state. The projection has no actor
generation field, so destruction and reuse of the same actor ID between two
sampled ticks within one world epoch cannot be distinguished by this view alone.
