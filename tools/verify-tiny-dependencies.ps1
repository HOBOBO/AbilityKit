[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Assert-References([string]$Project, [string[]]$Expected) {
    [xml]$xml = Get-Content -LiteralPath (Join-Path $root "src/$Project/$Project.csproj") -Raw
    $actual = @($xml.Project.ItemGroup.ProjectReference | ForEach-Object {
        [System.IO.Path]::GetFileNameWithoutExtension($_.Include)
    } | Sort-Object)
    $wanted = @($Expected | Sort-Object)
    if (($actual -join ',') -ne ($wanted -join ',')) {
        throw "$Project dependency drift: actual=[$($actual -join ',')] expected=[$($wanted -join ',')]"
    }
}

Assert-References 'AbilityKit.Demo.Tiny.Logic.Sample' @('AbilityKit.Demo.Tiny.Core')
Assert-References 'AbilityKit.Demo.Tiny.RoomSample' @('AbilityKit.Network.Room')
Assert-References 'AbilityKit.Demo.Tiny.StateSample' @(
    'AbilityKit.Demo.Tiny.Core', 'AbilityKit.Network.Room', 'AbilityKit.Protocol.Room')
Assert-References 'AbilityKit.Demo.Tiny.FrameSample' @('AbilityKit.Demo.Tiny.ClientHarness')
Assert-References 'AbilityKit.Demo.Tiny.HybridSample' @('AbilityKit.Demo.Tiny.ClientHarness')
Assert-References 'AbilityKit.Demo.Tiny.Record.Sample' @(
    'AbilityKit.Demo.Tiny.Core', 'AbilityKit.Record')
Assert-References 'AbilityKit.Demo.Tiny.ProtocolEvolution.Sample' @(
    'AbilityKit.Demo.Tiny.Core', 'AbilityKit.Network.Room',
    'AbilityKit.Network.Runtime', 'AbilityKit.Protocol.Room')
Assert-References 'AbilityKit.Demo.Tiny.BattleStyles.Sample' @(
    'AbilityKit.Demo.Tiny.Core', 'AbilityKit.Demo.Tiny.Turn.Core')
Assert-References 'AbilityKit.Demo.Tiny.Turn.StateSample' @(
    'AbilityKit.Demo.Tiny.Core', 'AbilityKit.Demo.Tiny.Turn.Core',
    'AbilityKit.Network.Room', 'AbilityKit.Protocol.Room')
Assert-References 'AbilityKit.Demo.Tiny.LiveRecord.Sample' @(
    'AbilityKit.Demo.Tiny.Core', 'AbilityKit.Record',
    'AbilityKit.Network.Room', 'AbilityKit.Protocol.Room')

$logic = Get-Content -LiteralPath (Join-Path $root `
    'Unity/Packages/com.abilitykit.demo.tiny.logic/package.json') -Raw | ConvertFrom-Json
$tiny = Get-Content -LiteralPath (Join-Path $root `
    'Unity/Packages/com.abilitykit.demo.tiny/package.json') -Raw | ConvertFrom-Json
$turn = Get-Content -LiteralPath (Join-Path $root `
    'Unity/Packages/com.abilitykit.demo.tiny.turn/package.json') -Raw | ConvertFrom-Json
if (@($logic.dependencies.PSObject.Properties).Count -ne 0) {
    throw 'Tiny Logic package must remain standalone.'
}
$dependencies = @($tiny.dependencies.PSObject.Properties.Name)
if ($dependencies -notcontains 'com.abilitykit.demo.tiny.logic' -or
    $dependencies -notcontains 'com.abilitykit.world.framesync' -or
    $dependencies -contains 'com.abilitykit.record' -or
    @($dependencies | Where-Object { $_ -match 'skill|buff|projectile' }).Count -gt 0) {
    throw 'Tiny main package dependency boundary changed.'
}
$turnDependencies = @($turn.dependencies.PSObject.Properties.Name | Sort-Object)
$expectedTurnDependencies = @(@(
    'com.abilitykit.demo.common', 'com.abilitykit.network.room',
    'com.abilitykit.network.runtime', 'com.abilitykit.network.sdk',
    'com.abilitykit.protocol.room') | Sort-Object)
if (($turnDependencies -join ',') -ne ($expectedTurnDependencies -join ',') -or
    $dependencies -contains 'com.abilitykit.demo.tiny.turn') {
    throw 'Optional Tiny Turn package dependency boundary changed.'
}
Write-Host 'Tiny dependency tiers passed: Logic, Room, State, Frame/Hybrid, optional Turn and samples'
