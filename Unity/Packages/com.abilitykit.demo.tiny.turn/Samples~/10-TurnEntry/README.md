# 10 Turn Project Entry

Attach `TinyTurnProjectLobbyEntry` to a GameObject in your own lobby scene.
Pass an authenticated `DemoMultiplayerLaunchRequest` to `Enter`, and add both
the lobby and `TinyTurnGameplayScene` to Build Settings. The Turn package owns
the Room, State snapshot and action flow; the project owns login and navigation.

Install `com.abilitykit.demo.tiny.turn` separately. The main Tiny package does
not depend on it.
