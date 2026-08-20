[CmdletBinding()]
param(
    [string]$ContractPath = 'tools\moba-content-dependency-contract.json',
    [string]$OutputPath = 'artifacts\moba-content\content-dependency-report.json',
    [switch]$Validate,
    [switch]$IncludeTimestamp
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Resolve-RepoPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

function Read-JsonFile {
    param([string]$Path, [string]$Label)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Label was not found: $Path"
    }

    try {
        return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        throw "$Label is not valid JSON: $Path. $($_.Exception.Message)"
    }
}

function Test-IsCollection {
    param([object]$Value)

    return $null -ne $Value -and
        $Value -is [System.Collections.IEnumerable] -and
        $Value -isnot [string] -and
        $Value -isnot [System.Management.Automation.PSCustomObject] -and
        $Value -isnot [System.Collections.IDictionary]
}

function Get-ChildValues {
    param([object]$Value, [string]$ActualPath)

    if ($Value -is [System.Management.Automation.PSCustomObject]) {
        foreach ($property in $Value.PSObject.Properties) {
            $childPath = if ([string]::IsNullOrEmpty($ActualPath)) { $property.Name } else { "$ActualPath.$($property.Name)" }
            [pscustomobject]@{ Value = $property.Value; ActualPath = $childPath }
        }
    }
    elseif ($Value -is [System.Collections.IDictionary]) {
        foreach ($key in @($Value.Keys | Sort-Object)) {
            $childPath = if ([string]::IsNullOrEmpty($ActualPath)) { [string]$key } else { "$ActualPath.$key" }
            [pscustomobject]@{ Value = $Value[$key]; ActualPath = $childPath }
        }
    }
    elseif (Test-IsCollection $Value) {
        $index = 0
        foreach ($item in $Value) {
            [pscustomobject]@{ Value = $item; ActualPath = "$ActualPath[$index]" }
            $index++
        }
    }
}

function Get-PathValuesInternal {
    param(
        [object]$Value,
        [string[]]$Segments,
        [int]$SegmentIndex,
        [string]$ActualPath
    )

    if ($SegmentIndex -ge $Segments.Count) {
        if (Test-IsCollection $Value) {
            $index = 0
            foreach ($item in $Value) {
                [pscustomobject]@{ Value = $item; ActualPath = "$ActualPath[$index]" }
                $index++
            }
        }
        else {
            [pscustomobject]@{ Value = $Value; ActualPath = $ActualPath }
        }
        return
    }

    $segment = $Segments[$SegmentIndex]
    if ($segment -eq '**') {
        Get-PathValuesInternal -Value $Value -Segments $Segments -SegmentIndex ($SegmentIndex + 1) -ActualPath $ActualPath
        foreach ($child in @(Get-ChildValues -Value $Value -ActualPath $ActualPath)) {
            Get-PathValuesInternal -Value $child.Value -Segments $Segments -SegmentIndex $SegmentIndex -ActualPath $child.ActualPath
        }
        return
    }

    if (Test-IsCollection $Value) {
        $index = 0
        foreach ($item in $Value) {
            Get-PathValuesInternal -Value $item -Segments $Segments -SegmentIndex $SegmentIndex -ActualPath "$ActualPath[$index]"
            $index++
        }
        return
    }

    $property = $null
    if ($Value -is [System.Management.Automation.PSCustomObject]) {
        $property = $Value.PSObject.Properties[$segment]
    }
    elseif ($Value -is [System.Collections.IDictionary] -and $Value.Contains($segment)) {
        $property = [pscustomobject]@{ Value = $Value[$segment] }
    }

    if ($null -ne $property) {
        $nextPath = if ([string]::IsNullOrEmpty($ActualPath)) { $segment } else { "$ActualPath.$segment" }
        Get-PathValuesInternal -Value $property.Value -Segments $Segments -SegmentIndex ($SegmentIndex + 1) -ActualPath $nextPath
    }
}

function Get-PathValues {
    param([object]$Value, [string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return
    }

    $segments = @($Path.Split('.') | Where-Object { $_.Length -gt 0 })
    Get-PathValuesInternal -Value $Value -Segments $segments -SegmentIndex 0 -ActualPath ''
}

function ConvertTo-Id {
    param([object]$Value)

    $parsed = 0L
    if ($null -eq $Value) { return $null }
    if ([long]::TryParse([string]$Value, [ref]$parsed)) { return $parsed }

    $decimalValue = 0D
    if ([decimal]::TryParse(
            [string]$Value,
            [System.Globalization.NumberStyles]::Float,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [ref]$decimalValue) -and
        [decimal]::Truncate($decimalValue) -eq $decimalValue -and
        $decimalValue -ge [long]::MinValue -and
        $decimalValue -le [long]::MaxValue) {
        return [long]$decimalValue
    }

    return $null
}

function New-Issue {
    param(
        [string]$Severity,
        [string]$Code,
        [string]$Message,
        [System.Collections.IDictionary]$Details = ([ordered]@{})
    )

    $issue = [ordered]@{
        severity = $Severity
        code = $Code
        message = $Message
    }
    foreach ($key in $Details.Keys) {
        $issue[$key] = $Details[$key]
    }
    return [pscustomobject]$issue
}

function Get-NodeKey {
    param([string]$Table, [long]$Id)
    return "$Table`:$Id"
}

$resolvedContractPath = Resolve-RepoPath $ContractPath
$contract = Read-JsonFile -Path $resolvedContractPath -Label 'MOBA content dependency contract'
if ([int]$contract.schemaVersion -ne 1 -or [string]$contract.contract -ne 'moba-content-dependency') {
    throw "MOBA content dependency contract must use schemaVersion 1 and contract 'moba-content-dependency'."
}

$issues = New-Object System.Collections.Generic.List[object]
$tableStates = @{}
$tableNames = @{}

foreach ($table in @($contract.tables)) {
    $tableName = [string]$table.name
    if ([string]::IsNullOrWhiteSpace($tableName)) { throw 'Every dependency table must declare a name.' }
    if ($tableNames.ContainsKey($tableName)) { throw "Dependency contract defines table '$tableName' more than once." }
    $tableNames[$tableName] = $true

    $idProperty = [string]$table.idProperty
    if ([string]::IsNullOrWhiteSpace($idProperty)) { throw "Table '$tableName' must declare idProperty." }
    $records = New-Object System.Collections.Generic.List[object]
    $sourcePaths = New-Object System.Collections.Generic.List[string]
    $sources = if ($table.PSObject.Properties['sources']) { @($table.sources) } else { @($table) }
    foreach ($source in $sources) {
        if ($null -eq $source -or [string]::IsNullOrWhiteSpace([string]$source.path)) {
            throw "Table '$tableName' contains a source without a path."
        }
        $sourcePath = [string]$source.path
        $resolvedPath = Resolve-RepoPath $sourcePath
        $data = Read-JsonFile -Path $resolvedPath -Label "MOBA content table '$tableName' source"
        $recordsPath = if ($source.PSObject.Properties['recordsPath']) { [string]$source.recordsPath } elseif ($table.PSObject.Properties['recordsPath']) { [string]$table.recordsPath } else { '' }
        if (-not [string]::IsNullOrWhiteSpace($recordsPath)) {
            $sourceRecords = @(Get-PathValues -Value $data -Path $recordsPath | ForEach-Object { $_.Value })
            if ($sourceRecords.Count -eq 0) {
                throw "Table '$tableName' source '$sourcePath' recordsPath '$recordsPath' did not select any records."
            }
        }
        else {
            $sourceRecords = @($data)
        }

        $sourceIdProperty = if ($source.PSObject.Properties['idProperty']) { [string]$source.idProperty } else { $idProperty }
        foreach ($record in $sourceRecords) {
            if ($sourceIdProperty -ne $idProperty -and $null -eq $record.PSObject.Properties[$idProperty] -and $null -ne $record.PSObject.Properties[$sourceIdProperty]) {
                $record | Add-Member -NotePropertyName $idProperty -NotePropertyValue $record.$sourceIdProperty
            }
            $records.Add($record)
        }
        $sourcePaths.Add($sourcePath)
    }
    $index = @{}
    $duplicateIds = New-Object System.Collections.Generic.List[long]

    foreach ($record in $records) {
        $idValue = if ($null -ne $record.PSObject.Properties[$idProperty]) { $record.$idProperty } else { $null }
        $recordId = ConvertTo-Id $idValue
        if ($null -eq $recordId -or $recordId -le 0) {
            $issues.Add((New-Issue -Severity 'error' -Code 'invalid-record-id' -Message "Table '$tableName' contains a record without a positive integer '$idProperty'." -Details ([ordered]@{ table = $tableName; idProperty = $idProperty; value = $idValue })))
            continue
        }

        $idKey = [string]$recordId
        if ($index.ContainsKey($idKey)) {
            if (-not $duplicateIds.Contains($recordId)) { $duplicateIds.Add($recordId) }
            $issues.Add((New-Issue -Severity 'error' -Code 'duplicate-id' -Message "Table '$tableName' defines ID $recordId more than once." -Details ([ordered]@{ table = $tableName; id = $recordId })))
        }
        else {
            $index[$idKey] = $record
        }
    }

    $tableStates[$tableName] = [pscustomobject]@{
        Contract = $table
        Records = $records.ToArray()
        Index = $index
        DuplicateIds = $duplicateIds
        Path = $(if ($sourcePaths.Count -eq 1) { $sourcePaths[0] } else { '' })
        Paths = $sourcePaths.ToArray()
        IdProperty = $idProperty
    }
}

$edges = New-Object System.Collections.Generic.List[object]
foreach ($table in @($contract.tables)) {
    $sourceTable = [string]$table.name
    $state = $tableStates[$sourceTable]
    foreach ($rule in @($table.references)) {
        if ($null -eq $rule) { continue }
        $targetTable = [string]$rule.targetTable
        if (-not $tableStates.ContainsKey($targetTable)) {
            throw "Reference '$sourceTable.$($rule.path)' targets undeclared table '$targetTable'."
        }
        $severity = if ($rule.PSObject.Properties['severity']) { [string]$rule.severity } else { 'error' }
        if ($severity -notin @('error', 'warning')) {
            throw "Reference '$sourceTable.$($rule.path)' uses unsupported severity '$severity'."
        }
        $optionalZero = $rule.PSObject.Properties['optionalZero'] -and [bool]$rule.optionalZero

        foreach ($record in $state.Records) {
            $sourceId = ConvertTo-Id $record.($state.IdProperty)
            if ($null -eq $sourceId -or $sourceId -le 0) { continue }
            $seenReferences = @{}
            foreach ($match in @(Get-PathValues -Value $record -Path ([string]$rule.path))) {
                if ($null -eq $match.Value -or [string]::IsNullOrWhiteSpace([string]$match.Value)) { continue }
                $referenceKey = "$($match.ActualPath)|$($match.Value)"
                if ($seenReferences.ContainsKey($referenceKey)) { continue }
                $seenReferences[$referenceKey] = $true
                $targetId = ConvertTo-Id $match.Value
                if ($null -eq $targetId) {
                    $issues.Add((New-Issue -Severity $severity -Code 'invalid-reference-id' -Message "Reference '$sourceTable.$($rule.path)' contains a non-integer value." -Details ([ordered]@{ sourceTable = $sourceTable; sourceId = $sourceId; propertyPath = $match.ActualPath; targetTable = $targetTable; value = $match.Value })))
                    continue
                }
                if ($targetId -eq 0 -and $optionalZero) { continue }

                $resolved = $tableStates[$targetTable].Index.ContainsKey([string]$targetId)
                $edge = [pscustomobject][ordered]@{
                    sourceTable = $sourceTable
                    sourceId = $sourceId
                    propertyPath = $match.ActualPath
                    targetTable = $targetTable
                    targetId = $targetId
                    status = $(if ($resolved) { 'resolved' } else { 'missing' })
                }
                $edges.Add($edge)
                if (-not $resolved) {
                    $issues.Add((New-Issue -Severity $severity -Code 'missing-reference' -Message "'$sourceTable' ID $sourceId references missing '$targetTable' ID $targetId through '$($match.ActualPath)'." -Details ([ordered]@{ sourceTable = $sourceTable; sourceId = $sourceId; propertyPath = $match.ActualPath; targetTable = $targetTable; targetId = $targetId })))
                }
            }
        }
    }
}

$sortedEdges = @($edges.ToArray() | Sort-Object sourceTable, sourceId, propertyPath, targetTable, targetId)
$adjacency = @{}
foreach ($edge in $sortedEdges) {
    if ($edge.status -ne 'resolved') { continue }
    $sourceKey = Get-NodeKey -Table $edge.sourceTable -Id $edge.sourceId
    if (-not $adjacency.ContainsKey($sourceKey)) { $adjacency[$sourceKey] = New-Object System.Collections.Generic.List[object] }
    $adjacency[$sourceKey].Add($edge)
}

$rootReports = New-Object System.Collections.Generic.List[object]
$productionReachable = @{}
foreach ($rootContract in @($contract.roots)) {
    $manifestPath = Resolve-RepoPath ([string]$rootContract.manifestPath)
    $manifest = Read-JsonFile -Path $manifestPath -Label "$($rootContract.kind) root manifest"
    $collectionProperty = [string]$rootContract.collectionProperty
    $entries = @($manifest.$collectionProperty)
    foreach ($entry in $entries) {
        $rootId = ConvertTo-Id $entry.([string]$rootContract.idProperty)
        $rootName = [string]$entry.([string]$rootContract.nameProperty)
        $targetTable = [string]$rootContract.targetTable
        $rootIssues = New-Object System.Collections.Generic.List[object]
        $visited = @{}

        if ($null -eq $rootId -or -not $tableStates[$targetTable].Index.ContainsKey([string]$rootId)) {
            $rootIssue = New-Issue -Severity 'error' -Code 'missing-root' -Message "$($rootContract.kind) root '$rootName' references missing '$targetTable' ID $rootId." -Details ([ordered]@{ rootKind = [string]$rootContract.kind; rootName = $rootName; targetTable = $targetTable; targetId = $rootId })
            $issues.Add($rootIssue)
            $rootIssues.Add($rootIssue)
        }
        else {
            $queue = New-Object System.Collections.Generic.Queue[string]
            $queue.Enqueue((Get-NodeKey -Table $targetTable -Id $rootId))
            while ($queue.Count -gt 0) {
                $nodeKey = $queue.Dequeue()
                if ($visited.ContainsKey($nodeKey)) { continue }
                $visited[$nodeKey] = $true
                $productionReachable[$nodeKey] = $true
                if ($adjacency.ContainsKey($nodeKey)) {
                    foreach ($edge in $adjacency[$nodeKey]) {
                        $targetKey = Get-NodeKey -Table $edge.targetTable -Id $edge.targetId
                        if (-not $visited.ContainsKey($targetKey)) { $queue.Enqueue($targetKey) }
                    }
                }
            }
        }

        $rootReports.Add([pscustomobject][ordered]@{
            kind = [string]$rootContract.kind
            id = $rootId
            name = $rootName
            targetTable = $targetTable
            reachableNodeCount = $visited.Count
            reachableNodes = @($visited.Keys | Sort-Object)
            reachableIssueCount = 0
            issues = @($rootIssues.ToArray())
        })
    }
}

$resourceReferences = New-Object System.Collections.Generic.List[object]
foreach ($rule in @($contract.resourceRules)) {
    $tableName = [string]$rule.table
    if (-not $tableStates.ContainsKey($tableName)) { throw "Resource rule targets undeclared table '$tableName'." }
    $state = $tableStates[$tableName]
    foreach ($record in $state.Records) {
        $recordId = ConvertTo-Id $record.($state.IdProperty)
        if ($null -eq $recordId -or $recordId -le 0) { continue }
        $nodeKey = Get-NodeKey -Table $tableName -Id $recordId
        $isProductionReachable = $productionReachable.ContainsKey($nodeKey)
        $severity = if ($isProductionReachable -and $rule.PSObject.Properties['productionReachableSeverity']) { [string]$rule.productionReachableSeverity } else { [string]$rule.severity }
        if ($severity -notin @('error', 'warning')) {
            throw "Resource rule '$tableName.$($rule.path)' uses unsupported severity '$severity'."
        }
        $matches = @(Get-PathValues -Value $record -Path ([string]$rule.path))
        $resourceValue = if ($matches.Count -gt 0) { [string]$matches[0].Value } else { '' }
        $resourceStatus = 'resolved'
        $resourceQuality = 'production'
        $resourceOwner = ''
        $resourceReason = ''
        $logicalCandidates = New-Object System.Collections.Generic.List[string]

        if ([bool]$rule.required -and [string]::IsNullOrWhiteSpace($resourceValue)) {
            $issues.Add((New-Issue -Severity $severity -Code 'missing-resource-value' -Message "Resource field '$tableName.$($rule.path)' is empty for ID $recordId." -Details ([ordered]@{ table = $tableName; id = $recordId; propertyPath = [string]$rule.path; productionReachable = $isProductionReachable })))
            $resourceReferences.Add([pscustomobject][ordered]@{
                table = $tableName
                id = $recordId
                propertyPath = [string]$rule.path
                value = $resourceValue
                status = 'missing-value'
                quality = $resourceQuality
                required = [bool]$rule.required
                productionReachable = $isProductionReachable
                candidateResourcePaths = @()
                owner = $resourceOwner
                reason = $resourceReason
            })
            continue
        }
        foreach ($pattern in @($rule.placeholderPatterns)) {
            if (-not [string]::IsNullOrWhiteSpace($resourceValue) -and $resourceValue -match [string]$pattern) {
                $allowlistEntry = @($rule.placeholderAllowlist | Where-Object { (ConvertTo-Id $_.id) -eq $recordId } | Select-Object -First 1)
                if ($allowlistEntry.Count -gt 0) {
                    $allowed = $allowlistEntry[0]
                    $resourceQuality = 'allowlisted-placeholder'
                    $resourceOwner = [string]$allowed.owner
                    $resourceReason = [string]$allowed.reason
                    $issues.Add((New-Issue -Severity ([string]$rule.severity) -Code 'allowed-placeholder-resource' -Message "Resource field '$tableName.$($rule.path)' uses allowlisted placeholder '$resourceValue' for ID $recordId." -Details ([ordered]@{ table = $tableName; id = $recordId; propertyPath = [string]$rule.path; value = $resourceValue; productionReachable = $isProductionReachable; allowlistOwner = [string]$allowed.owner; allowlistReason = [string]$allowed.reason })))
                }
                else {
                    $resourceQuality = 'placeholder'
                    $issues.Add((New-Issue -Severity $severity -Code 'placeholder-resource' -Message "Resource field '$tableName.$($rule.path)' uses placeholder '$resourceValue' for ID $recordId." -Details ([ordered]@{ table = $tableName; id = $recordId; propertyPath = [string]$rule.path; value = $resourceValue; productionReachable = $isProductionReachable })))
                }
                break
            }
        }

        if (-not [string]::IsNullOrWhiteSpace($resourceValue) -and $rule.PSObject.Properties['resourceRoot']) {
            $resourceRoot = Resolve-RepoPath ([string]$rule.resourceRoot)
            $resourceRootPrefix = [System.IO.Path]::GetFullPath($resourceRoot).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
            $resourceRelativePath = $resourceValue.Replace('/', [System.IO.Path]::DirectorySeparatorChar).Replace('\', [System.IO.Path]::DirectorySeparatorChar)
            $resourceBasePath = [System.IO.Path]::GetFullPath((Join-Path $resourceRoot $resourceRelativePath))
            if (-not ($resourceBasePath + [System.IO.Path]::DirectorySeparatorChar).StartsWith($resourceRootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                $resourceStatus = 'invalid-path'
                $issues.Add((New-Issue -Severity $severity -Code 'invalid-resource-path' -Message "Resource field '$tableName.$($rule.path)' escapes its resource root for ID $recordId." -Details ([ordered]@{ table = $tableName; id = $recordId; propertyPath = [string]$rule.path; value = $resourceValue; resourceRoot = [string]$rule.resourceRoot; productionReachable = $isProductionReachable })))
                $resourceReferences.Add([pscustomobject][ordered]@{
                    table = $tableName
                    id = $recordId
                    propertyPath = [string]$rule.path
                    value = $resourceValue
                    status = $resourceStatus
                    quality = $resourceQuality
                    required = [bool]$rule.required
                    productionReachable = $isProductionReachable
                    candidateResourcePaths = @()
                    owner = $resourceOwner
                    reason = $resourceReason
                })
                continue
            }

            $extensions = @($rule.extensions)
            if ($extensions.Count -eq 0 -or ($extensions.Count -eq 1 -and $null -eq $extensions[0])) { $extensions = @('') }
            $candidatePaths = New-Object System.Collections.Generic.List[string]
            $resourceExists = $false
            foreach ($extension in $extensions) {
                $candidate = $resourceBasePath + [string]$extension
                $candidatePaths.Add($candidate)
                $logicalCandidates.Add(($resourceValue + [string]$extension).Replace('\', '/'))
                if (Test-Path -LiteralPath $candidate -PathType Leaf) { $resourceExists = $true }
            }
            if (-not $resourceExists) {
                $resourceStatus = 'missing-file'
                $issues.Add((New-Issue -Severity $severity -Code 'missing-resource-file' -Message "Resource field '$tableName.$($rule.path)' cannot resolve '$resourceValue' for ID $recordId." -Details ([ordered]@{ table = $tableName; id = $recordId; propertyPath = [string]$rule.path; value = $resourceValue; candidatePaths = @($candidatePaths.ToArray()); productionReachable = $isProductionReachable })))
            }
        }

        $resourceReferences.Add([pscustomobject][ordered]@{
            table = $tableName
            id = $recordId
            propertyPath = [string]$rule.path
            value = $resourceValue
            status = $resourceStatus
            quality = $resourceQuality
            required = [bool]$rule.required
            productionReachable = $isProductionReachable
            candidateResourcePaths = @($logicalCandidates.ToArray())
            owner = $resourceOwner
            reason = $resourceReason
        })
    }
}

$externalReports = New-Object System.Collections.Generic.List[object]
foreach ($external in @($contract.externalReferences)) {
    $sourceTable = [string]$external.sourceTable
    if (-not $tableStates.ContainsKey($sourceTable)) { throw "External reference targets undeclared source table '$sourceTable'." }
    foreach ($path in @($external.paths)) {
        $values = New-Object System.Collections.Generic.List[long]
        foreach ($record in $tableStates[$sourceTable].Records) {
            foreach ($match in @(Get-PathValues -Value $record -Path ([string]$path))) {
                $id = ConvertTo-Id $match.Value
                if ($null -ne $id -and $id -ne 0) { $values.Add($id) }
            }
        }
        $uniqueValues = @($values | Sort-Object -Unique)
        $externalReports.Add([pscustomobject][ordered]@{
            sourceTable = $sourceTable
            propertyPath = [string]$path
            authority = [string]$external.authority
            reason = [string]$external.reason
            referenceCount = $values.Count
            uniqueIds = $uniqueValues
        })
    }
}

$incomingIds = @{}
foreach ($edge in $sortedEdges) {
    if ($edge.status -ne 'resolved') { continue }
    if (-not $incomingIds.ContainsKey($edge.targetTable)) { $incomingIds[$edge.targetTable] = @{} }
    $incomingIds[$edge.targetTable][[string]$edge.targetId] = $true
}

$tableReports = New-Object System.Collections.Generic.List[object]
foreach ($tableName in @($tableStates.Keys | Sort-Object)) {
    $state = $tableStates[$tableName]
    $unreferencedIds = New-Object System.Collections.Generic.List[long]
    foreach ($idKey in @($state.Index.Keys)) {
        if (-not $incomingIds.ContainsKey($tableName) -or -not $incomingIds[$tableName].ContainsKey($idKey)) {
            $unreferencedIds.Add([long]$idKey)
        }
    }
    $tableReports.Add([pscustomobject][ordered]@{
        name = $tableName
        path = $state.Path
        paths = @($state.Paths)
        recordsPath = $(if ($state.Contract.PSObject.Properties['recordsPath']) { [string]$state.Contract.recordsPath } else { '' })
        recordCount = $state.Records.Count
        duplicateIds = @($state.DuplicateIds.ToArray() | Sort-Object)
        unreferencedIds = @($unreferencedIds.ToArray() | Sort-Object)
        unreferencedBasis = 'declared-static-edges'
    })
}

$sortedIssues = @($issues.ToArray() | Sort-Object @{ Expression = { if ($_.severity -eq 'error') { 0 } else { 1 } } }, code, table, sourceTable, sourceId, propertyPath, targetTable, targetId, id)
$sortedRootReports = @($rootReports.ToArray() | Sort-Object kind, id)
foreach ($rootReport in $sortedRootReports) {
    $reachable = @{}
    foreach ($nodeKey in @($rootReport.reachableNodes)) { $reachable[[string]$nodeKey] = $true }
    $reachableIssues = New-Object System.Collections.Generic.List[object]
    foreach ($issue in $sortedIssues) {
        $belongsToRoot = $false
        if ($issue.PSObject.Properties['sourceTable'] -and $issue.PSObject.Properties['sourceId']) {
            $belongsToRoot = $reachable.ContainsKey((Get-NodeKey -Table ([string]$issue.sourceTable) -Id ([long]$issue.sourceId)))
        }
        if (-not $belongsToRoot -and $issue.PSObject.Properties['table'] -and $issue.PSObject.Properties['id']) {
            $belongsToRoot = $reachable.ContainsKey((Get-NodeKey -Table ([string]$issue.table) -Id ([long]$issue.id)))
        }
        if (-not $belongsToRoot -and $issue.PSObject.Properties['rootName']) {
            $belongsToRoot = [string]$issue.rootName -eq [string]$rootReport.name
        }
        if ($belongsToRoot) { $reachableIssues.Add($issue) }
    }
    $rootReport.reachableIssueCount = $reachableIssues.Count
    $rootReport.issues = @($reachableIssues.ToArray())
}
$errorCount = @($sortedIssues | Where-Object severity -eq 'error').Count
$warningCount = @($sortedIssues | Where-Object severity -eq 'warning').Count
$report = [ordered]@{
    schemaVersion = 1
    report = 'moba-content-dependency'
    status = $(if ($errorCount -eq 0) { 'passed' } else { 'failed' })
    contractPath = [string]$ContractPath
}
if ($IncludeTimestamp) { $report.generatedAtUtc = [DateTime]::UtcNow.ToString('o') }
$report.roots = $sortedRootReports
$report.tables = @($tableReports.ToArray())
$report.edges = $sortedEdges
$report.resourceReferences = @($resourceReferences.ToArray() | Sort-Object table, id, propertyPath)
$report.externalReferences = @($externalReports.ToArray() | Sort-Object sourceTable, propertyPath)
$report.issues = $sortedIssues
$report.summary = [ordered]@{
    tables = $tableReports.Count
    records = ($tableReports | Measure-Object recordCount -Sum).Sum
    edges = $sortedEdges.Count
    resolvedEdges = @($sortedEdges | Where-Object status -eq 'resolved').Count
    missingEdges = @($sortedEdges | Where-Object status -eq 'missing').Count
    resources = $resourceReferences.Count
    externalReferenceKinds = $externalReports.Count
    errors = $errorCount
    warnings = $warningCount
}

$resolvedOutputPath = Resolve-RepoPath $OutputPath
$outputDirectory = Split-Path -Parent $resolvedOutputPath
if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {
    $null = New-Item -ItemType Directory -Force -Path $outputDirectory
}
$json = $report | ConvertTo-Json -Depth 32
[System.IO.File]::WriteAllText($resolvedOutputPath, $json + [Environment]::NewLine, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "MOBA content report: status=$($report.status) tables=$($report.summary.tables) edges=$($report.summary.edges) errors=$errorCount warnings=$warningCount output=$OutputPath"
if ($Validate -and $errorCount -gt 0) { exit 1 }
exit 0
