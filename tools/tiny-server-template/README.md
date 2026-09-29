# Tiny Server Bundle

This bundle contains published .NET 10 Host and Gateway applications. It has no
runtime dependency on the source checkout. Install the .NET 10 ASP.NET Core runtime on the
target machine, then start the Host before the Gateway from their own directories:

```powershell
cd host
dotnet AbilityKit.Orleans.Host.dll
```

```powershell
cd gateway
dotnet AbilityKit.Orleans.Gateway.dll
```

The Host registers Tiny State, Frame, and Hybrid. It also registers Tiny Turn by
default. Set `AbilityKit__Tiny__EnableTurn=false` to disable Turn without changing
the shared Tiny battle rules. Both applications use the matching rule and protocol
assemblies published in this bundle; do not replace individual DLLs independently.
`bundle.json` records SHA-256 hashes for the Room protocol and both Tiny rule
assemblies. The publisher also checks that Host and Gateway use identical Room
protocol binaries.

The `composition/` directory contains a separately compiled Host project with
an explicit `ConfigureGameplay` registration point for a project's own modules.
It builds against the published DLLs without accessing the source checkout.

The default local Orleans gateway is port 30000, the client TCP Gateway is port
4000, and HTTP is port 5001. Override these with the `AbilityKit__Orleans__*`,
`AbilityKit__Gateway__Tcp__Port`, `TcpGateway__Port`, and
`AbilityKit__Gateway__Http__Port` environment variables. Configure durable
storage and public network exposure before using this outside local development.

The Unity consumer project is generated separately by
`tools/create-tiny-validation-project.ps1 -Standalone -IncludeTurn`. In the source
repository, run `tools/verify-tiny-starter.ps1 -FocusChapter 10 -SkipUnity
-ServerBundlePath <bundle>` to check the published server against the real TCP
Turn client sample.
Add `-UseCompositionHost` to run the same sample against the compiled
`composition/TinyCustomHost` instead of the default Host.
