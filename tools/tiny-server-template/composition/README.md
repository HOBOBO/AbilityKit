# Custom Host composition

This project compiles against the published `../host/*.dll` files. It does not
reference the AbilityKit source checkout. Install the .NET 10 SDK to edit and
build it, then start the published `../gateway/AbilityKit.Orleans.Gateway.dll`
against the same Orleans configuration.

```powershell
dotnet build TinyCustomHost.csproj
dotnet run --project TinyCustomHost.csproj
```

From the source repository, `tools/verify-tiny-starter.ps1 -FocusChapter 10
-SkipUnity -ServerBundlePath <bundle> -UseCompositionHost` verifies this Host
with the published Gateway and a real Tiny Turn TCP client.

Add your own gameplay assembly reference to `TinyCustomHost.csproj`, then call
`catalog.WithModule(MyGameplayModule.Create())` in `ConfigureGameplay`. The
module owns its room descriptor, battle runtime adapter, world blueprint and
sync profile. Tiny and optional Turn stay registered as working examples.
The published binaries are a versioned unit: replace Host, Gateway, protocol,
and gameplay assemblies together when upgrading.
