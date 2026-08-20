[CmdletBinding()]
param(
    [string]$ReportPath = 'artifacts\moba-content\content-dependency-report.json',
    [string]$GraphOutputPath = 'artifacts\moba-content\moba-content-graph.json',
    [string]$DiagnosticsOutputPath = 'artifacts\moba-content\moba-content-diagnostics.json'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Resolve-RepoPath {
    param([string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) { return [System.IO.Path]::GetFullPath($Path) }
    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

function Ensure-ParentDirectory {
    param([string]$Path)
    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        $null = New-Item -ItemType Directory -Force -Path $parent
    }
}

function Get-PortablePath {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $null }
    if (-not [System.IO.Path]::IsPathRooted($Path)) { return $Path.Replace('\', '/') }

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $rootPrefix = $repoRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if ($fullPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath.Substring($rootPrefix.Length).Replace('\', '/')
    }
    return $null
}

function Get-RecordNodeId {
    param([string]$Table, [long]$Id)
    return "table:$Table`:$Id"
}

function Convert-ReportNodeKey {
    param([string]$NodeKey)
    $separator = $NodeKey.IndexOf(':')
    if ($separator -le 0) { return $null }
    $table = $NodeKey.Substring(0, $separator)
    $id = 0L
    if (-not [long]::TryParse($NodeKey.Substring($separator + 1), [ref]$id)) { return $null }
    return Get-RecordNodeId -Table $table -Id $id
}

function Get-StableHash {
    param([string]$Value)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
        $hash = $sha.ComputeHash($bytes)
        return ([System.BitConverter]::ToString($hash).Replace('-', '').ToLowerInvariant()).Substring(0, 20)
    }
    finally {
        $sha.Dispose()
    }
}

function Get-ReferenceEdgeId {
    param([object]$Edge)
    $identity = '{0}|{1}|{2}|{3}|{4}' -f $Edge.sourceTable, $Edge.sourceId, $Edge.propertyPath, $Edge.targetTable, $Edge.targetId
    return 'edge:reference:' + (Get-StableHash $identity)
}

function Ensure-RecordNode {
    param(
        [hashtable]$Map,
        [string]$Table,
        [long]$Id,
        [bool]$Exists = $true
    )
    $nodeId = Get-RecordNodeId -Table $Table -Id $Id
    if (-not $Map.ContainsKey($nodeId)) {
        $Map[$nodeId] = [pscustomobject][ordered]@{
            id = $nodeId
            kind = $(if ($Exists) { 'record' } else { 'missing-record' })
            table = $Table
            recordId = $Id
            exists = $Exists
            unreferenced = $false
            duplicate = $false
        }
    }
    elseif ($Exists -and -not [bool]$Map[$nodeId].exists) {
        $Map[$nodeId].exists = $true
        $Map[$nodeId].kind = 'record'
    }
    return $Map[$nodeId]
}

function Get-SuggestedAction {
    param([string]$Code)
    switch ($Code) {
        'missing-reference' { return 'define-target-or-remove-reference' }
        'invalid-reference-id' { return 'replace-with-integer-reference' }
        'duplicate-id' { return 'assign-unique-record-id' }
        'invalid-record-id' { return 'assign-positive-record-id' }
        'missing-root' { return 'define-root-record-or-update-manifest' }
        'missing-resource-value' { return 'set-required-resource' }
        'missing-resource-file' { return 'add-resource-or-update-path' }
        'invalid-resource-path' { return 'move-resource-under-declared-root' }
        'placeholder-resource' { return 'replace-placeholder-resource' }
        'allowed-placeholder-resource' { return 'resolve-owned-placeholder-debt' }
        default { return 'inspect-diagnostic' }
    }
}

$resolvedReportPath = Resolve-RepoPath $ReportPath
if (-not (Test-Path -LiteralPath $resolvedReportPath -PathType Leaf)) {
    throw "MOBA content dependency report was not found: $ReportPath"
}
try {
    $report = Get-Content -LiteralPath $resolvedReportPath -Raw -Encoding UTF8 | ConvertFrom-Json
}
catch {
    throw "MOBA content dependency report is not valid JSON: $ReportPath. $($_.Exception.Message)"
}
if ([int]$report.schemaVersion -ne 1 -or [string]$report.report -ne 'moba-content-dependency') {
    throw 'MOBA content IR export requires a schemaVersion 1 moba-content-dependency report.'
}

$tableMap = @{}
$nodeMap = @{}
foreach ($table in @($report.tables | Sort-Object name)) {
    $tableName = [string]$table.name
    $tableMap[$tableName] = $table
    $tableNodeId = "table:$tableName"
    $nodeMap[$tableNodeId] = [pscustomobject][ordered]@{
        id = $tableNodeId
        kind = 'table'
        table = $tableName
        recordCount = [int]$table.recordCount
    }
    foreach ($recordId in @($table.unreferencedIds)) {
        $node = Ensure-RecordNode -Map $nodeMap -Table $tableName -Id ([long]$recordId)
        $node.unreferenced = $true
    }
    foreach ($recordId in @($table.duplicateIds)) {
        $node = Ensure-RecordNode -Map $nodeMap -Table $tableName -Id ([long]$recordId)
        $node.duplicate = $true
    }
}

$graphEdges = New-Object System.Collections.Generic.List[object]
foreach ($edge in @($report.edges | Sort-Object sourceTable, sourceId, propertyPath, targetTable, targetId)) {
    $source = Ensure-RecordNode -Map $nodeMap -Table ([string]$edge.sourceTable) -Id ([long]$edge.sourceId)
    $targetExists = [string]$edge.status -eq 'resolved'
    $target = Ensure-RecordNode -Map $nodeMap -Table ([string]$edge.targetTable) -Id ([long]$edge.targetId) -Exists $targetExists
    $graphEdges.Add([pscustomobject][ordered]@{
        id = Get-ReferenceEdgeId $edge
        kind = 'reference'
        source = [string]$source.id
        target = [string]$target.id
        propertyPath = [string]$edge.propertyPath
        status = [string]$edge.status
    })
}

$graphRoots = New-Object System.Collections.Generic.List[object]
foreach ($root in @($report.roots | Sort-Object kind, id)) {
    $rootNodeId = Get-RecordNodeId -Table ([string]$root.targetTable) -Id ([long]$root.id)
    $null = Ensure-RecordNode -Map $nodeMap -Table ([string]$root.targetTable) -Id ([long]$root.id) -Exists ([int]$root.reachableNodeCount -gt 0)
    $reachableNodeIds = @($root.reachableNodes | ForEach-Object { Convert-ReportNodeKey ([string]$_) } | Where-Object { $null -ne $_ } | Sort-Object -Unique)
    foreach ($reachableNodeId in $reachableNodeIds) {
        $parts = $reachableNodeId.Split(':')
        $null = Ensure-RecordNode -Map $nodeMap -Table $parts[1] -Id ([long]$parts[2])
    }
    $graphRoots.Add([pscustomobject][ordered]@{
        id = "root:$([string]$root.kind):$([long]$root.id)"
        kind = [string]$root.kind
        name = [string]$root.name
        nodeId = $rootNodeId
        reachableNodeIds = $reachableNodeIds
    })
}

$authorityEdges = New-Object System.Collections.Generic.List[object]
foreach ($external in @($report.externalReferences | Sort-Object sourceTable, propertyPath)) {
    if ([int]$external.referenceCount -le 0) { continue }
    $authorityId = 'authority:' + [string]$external.authority
    if (-not $nodeMap.ContainsKey($authorityId)) {
        $nodeMap[$authorityId] = [pscustomobject][ordered]@{
            id = $authorityId
            kind = 'authority'
            authority = [string]$external.authority
        }
    }
    $identity = '{0}|{1}|{2}' -f $external.sourceTable, $external.propertyPath, $external.authority
    $authorityEdges.Add([pscustomobject][ordered]@{
        id = 'edge:authority:' + (Get-StableHash $identity)
        kind = 'authority-reference'
        source = 'table:' + [string]$external.sourceTable
        target = $authorityId
        propertyPath = [string]$external.propertyPath
        referenceCount = [int]$external.referenceCount
        uniqueIds = @($external.uniqueIds)
        status = 'external'
    })
}
foreach ($edge in $authorityEdges) { $graphEdges.Add($edge) }

$graphResources = New-Object System.Collections.Generic.List[object]
foreach ($resource in @($report.resourceReferences | Sort-Object table, id, propertyPath)) {
    $node = Ensure-RecordNode -Map $nodeMap -Table ([string]$resource.table) -Id ([long]$resource.id)
    $identity = '{0}|{1}|{2}' -f $resource.table, $resource.id, $resource.propertyPath
    $graphResources.Add([pscustomobject][ordered]@{
        id = 'resource:' + (Get-StableHash $identity)
        nodeId = [string]$node.id
        propertyPath = [string]$resource.propertyPath
        value = [string]$resource.value
        status = [string]$resource.status
        quality = [string]$resource.quality
        required = [bool]$resource.required
        productionReachable = [bool]$resource.productionReachable
        candidateResourcePaths = @($resource.candidateResourcePaths)
        owner = [string]$resource.owner
        reason = [string]$resource.reason
    })
}

$graphNodes = @($nodeMap.Values | Sort-Object kind, table, recordId, id)
$graph = [ordered]@{
    schemaVersion = 1
    graph = 'moba-content-graph'
    status = [string]$report.status
    nodes = $graphNodes
    edges = @($graphEdges.ToArray() | Sort-Object kind, source, target, propertyPath, id)
    roots = @($graphRoots.ToArray())
    resources = @($graphResources.ToArray())
    summary = [ordered]@{
        tables = [int]$report.summary.tables
        records = [int]$report.summary.records
        nodes = $graphNodes.Count
        edges = $graphEdges.Count
        resolvedEdges = [int]$report.summary.resolvedEdges
        missingEdges = [int]$report.summary.missingEdges
        roots = $graphRoots.Count
        authorities = @($nodeMap.Values | Where-Object kind -eq 'authority').Count
        resources = $graphResources.Count
    }
}

$diagnostics = New-Object System.Collections.Generic.List[object]
foreach ($issue in @($report.issues)) {
    $sourceTable = if ($issue.PSObject.Properties['sourceTable']) { [string]$issue.sourceTable } elseif ($issue.PSObject.Properties['table']) { [string]$issue.table } else { '' }
    $sourceId = if ($issue.PSObject.Properties['sourceId']) { [long]$issue.sourceId } elseif ($issue.PSObject.Properties['id']) { [long]$issue.id } else { 0L }
    $nodeId = if (-not [string]::IsNullOrWhiteSpace($sourceTable) -and $sourceId -gt 0) { Get-RecordNodeId -Table $sourceTable -Id $sourceId } elseif (-not [string]::IsNullOrWhiteSpace($sourceTable)) { "table:$sourceTable" } else { $null }
    $edgeId = $null
    if ($issue.PSObject.Properties['sourceTable'] -and $issue.PSObject.Properties['sourceId'] -and $issue.PSObject.Properties['targetTable'] -and $issue.PSObject.Properties['targetId']) {
        $edgeId = Get-ReferenceEdgeId $issue
    }

    $sourcePaths = @()
    if (-not [string]::IsNullOrWhiteSpace($sourceTable) -and $tableMap.ContainsKey($sourceTable)) {
        $sourcePaths = @($tableMap[$sourceTable].paths | ForEach-Object { Get-PortablePath ([string]$_) } | Where-Object { $null -ne $_ } | Sort-Object -Unique)
    }
    $rootIds = @($graphRoots | Where-Object { $null -ne $nodeId -and $_.reachableNodeIds -contains $nodeId } | ForEach-Object id | Sort-Object)
    $owner = if ($issue.PSObject.Properties['allowlistOwner']) { [string]$issue.allowlistOwner } else { '' }
    $reason = if ($issue.PSObject.Properties['allowlistReason']) { [string]$issue.allowlistReason } else { '' }
    $propertyPath = if ($issue.PSObject.Properties['propertyPath']) { [string]$issue.propertyPath } else { '' }
    $targetIdentity = if ($issue.PSObject.Properties['targetTable']) { "$([string]$issue.targetTable):$([string]$issue.targetId)" } else { '' }
    $identity = '{0}|{1}|{2}|{3}|{4}|{5}' -f $issue.severity, $issue.code, $nodeId, $edgeId, $propertyPath, $targetIdentity

    $details = [ordered]@{}
    foreach ($property in $issue.PSObject.Properties) {
        if ($property.Name -in @('severity', 'code', 'message', 'sourceTable', 'sourceId', 'table', 'id', 'propertyPath', 'targetTable', 'targetId', 'candidatePaths', 'allowlistOwner', 'allowlistReason')) { continue }
        $details[$property.Name] = $property.Value
    }
    $diagnostics.Add([pscustomobject][ordered]@{
        id = 'diagnostic:' + (Get-StableHash $identity)
        severity = [string]$issue.severity
        code = [string]$issue.code
        message = [string]$issue.message
        nodeId = $nodeId
        edgeId = $edgeId
        propertyPath = $propertyPath
        sourcePaths = $sourcePaths
        rootIds = $rootIds
        owner = $owner
        reason = $reason
        suggestedAction = Get-SuggestedAction ([string]$issue.code)
        details = [pscustomobject]$details
    })
}

$sortedDiagnostics = @($diagnostics.ToArray() | Sort-Object @{ Expression = { if ($_.severity -eq 'error') { 0 } else { 1 } } }, code, nodeId, propertyPath, id)
$diagnosticsDocument = [ordered]@{
    schemaVersion = 1
    diagnostics = 'moba-content-diagnostics'
    graphSchemaVersion = 1
    status = [string]$report.status
    items = $sortedDiagnostics
    summary = [ordered]@{
        errors = @($sortedDiagnostics | Where-Object severity -eq 'error').Count
        warnings = @($sortedDiagnostics | Where-Object severity -eq 'warning').Count
        total = $sortedDiagnostics.Count
    }
}

$resolvedGraphOutputPath = Resolve-RepoPath $GraphOutputPath
$resolvedDiagnosticsOutputPath = Resolve-RepoPath $DiagnosticsOutputPath
Ensure-ParentDirectory $resolvedGraphOutputPath
Ensure-ParentDirectory $resolvedDiagnosticsOutputPath
$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($resolvedGraphOutputPath, ($graph | ConvertTo-Json -Depth 32) + [Environment]::NewLine, $utf8)
[System.IO.File]::WriteAllText($resolvedDiagnosticsOutputPath, ($diagnosticsDocument | ConvertTo-Json -Depth 32) + [Environment]::NewLine, $utf8)

Write-Host "MOBA content IR: nodes=$($graph.summary.nodes) edges=$($graph.summary.edges) diagnostics=$($diagnosticsDocument.summary.total) graph=$GraphOutputPath diagnosticsOutput=$DiagnosticsOutputPath"
exit 0
