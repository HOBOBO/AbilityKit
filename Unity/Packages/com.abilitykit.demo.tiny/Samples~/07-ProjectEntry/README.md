# 07 Project Entry

Attach `TinyProjectLobbyEntry` to a GameObject in your own lobby scene. After
your project authenticates a player, call `Enter(authenticatedLaunch)` with a
valid `DemoMultiplayerLaunchRequest`. Add both your lobby scene and
`TinyDemoGameplayScene` to Build Settings. Returning from Tiny loads the lobby
scene supplied by this component.

The sample delegates connection, Room recovery and gameplay to the package's
`TinyProjectLaunch` and `TinyGameplayRoot`; authentication remains project-owned.
