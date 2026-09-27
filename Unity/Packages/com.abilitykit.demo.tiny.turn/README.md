# Tiny Turn (optional)

This package adds a two-player turn-based battle without changing the main Tiny package. `Runtime/Logic/TinyTurnBattle.cs` is the single rules source; the .NET Turn.Core project compiles the same file.

Register `TinyTurnGameplayModule` on the server. In a Unity client, provide an authenticated `DemoMultiplayerLaunchRequest` and a return scene to `TinyTurnProjectLaunch.Open(request, returnScene)`. The scene handles room creation or joining, ready/loading, State snapshot projection, attacks, reconnection, and return navigation. A successful submit response means the action was queued; wait for an authoritative snapshot to confirm the turn.

Run `tools/create-tiny-validation-project.ps1 -IncludeTurn` from the repository to generate an isolated test project with this package and scene. `tools/verify-tiny-starter.ps1` runs the Gateway-backed PlayMode test in batch mode; `-SkipUnity` covers the .NET turn client and server path only.

Import `Samples~/10-TurnEntry` through Unity Package Manager for the project-owned lobby entry example. The full headless gate also starts two independent `-Standalone -IncludeTurn` Unity projects against one Gateway and checks both clients' authoritative result and return path. `-FocusChapter 10` runs the shorter .NET and TCP chapter check.
