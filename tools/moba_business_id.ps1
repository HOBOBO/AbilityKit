param(
    [ValidateSet("Validate", "Allocate")]
    [string]$Mode = "Validate",

    [string]$Namespace,

    [ValidateRange(1, 1000)]
    [int]$Count = 1,

    [string]$RegistryPath = "tools\moba-business-id-namespaces.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-RepoPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

function Read-Json {
    param([string]$Path)

    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "JSON file not found: $Path"
    }

    try {
        return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        throw "Invalid JSON '$Path': $($_.Exception.Message)"
    }
}

function Get-PropertyValue {
    param(
        [object]$Value,
        [string]$PropertyName,
        [string]$Location
    )

    if ($null -eq $Value -or $Value.PSObject.Properties.Name -notcontains $PropertyName) {
        throw "Missing id property '$PropertyName' at $Location."
    }

    $parsed = 0L
    if (![long]::TryParse([string]$Value.$PropertyName, [ref]$parsed)) {
        throw "Invalid integer id '$($Value.$PropertyName)' at $Location."
    }

    if ($parsed -le 0) {
        throw "Business id must be positive at $Location; actual=$parsed."
    }

    return $parsed
}

function Get-SourceFiles {
    param(
        [string]$AuthorityRoot,
        [object]$Source
    )

    $normalized = ([string]$Source.path).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    $fullPattern = Join-Path $AuthorityRoot $normalized
    if ($normalized.IndexOf('*') -ge 0 -or $normalized.IndexOf('?') -ge 0) {
        $directory = Split-Path -Parent $fullPattern
        $filter = Split-Path -Leaf $fullPattern
        if (!(Test-Path -LiteralPath $directory -PathType Container)) {
            throw "Source directory not found: $directory"
        }

        return @(Get-ChildItem -LiteralPath $directory -Filter $filter -File | Sort-Object FullName)
    }

    if (!(Test-Path -LiteralPath $fullPattern -PathType Leaf)) {
        throw "Source file not found: $fullPattern"
    }

    return @((Get-Item -LiteralPath $fullPattern))
}

function Get-SourceRecords {
    param(
        [string]$AuthorityRoot,
        [object]$Source
    )

    $records = New-Object System.Collections.Generic.List[object]
    $files = Get-SourceFiles -AuthorityRoot $AuthorityRoot -Source $Source
    foreach ($file in $files) {
        $json = Read-Json $file.FullName
        $relative = $file.FullName.Substring($AuthorityRoot.Length).TrimStart('\', '/')
        switch ([string]$Source.shape) {
            "array" {
                $items = @($json)
                for ($index = 0; $index -lt $items.Count; $index++) {
                    $location = "$relative[$index]"
                    $id = Get-PropertyValue -Value $items[$index] -PropertyName ([string]$Source.idProperty) -Location $location
                    [void]$records.Add([pscustomobject]@{ id = $id; location = $location; source = [string]$Source.path })
                }
            }
            "trigger-files" {
                if ($null -eq $json) {
                    throw "Empty trigger JSON at $relative."
                }

                if ($json.PSObject.Properties.Name -contains "triggers") {
                    $items = @($json.triggers)
                    $locationPrefix = "$relative.triggers"
                }
                elseif ($json -is [array]) {
                    $items = @($json)
                    $locationPrefix = $relative
                }
                else {
                    $items = @($json)
                    $locationPrefix = $relative
                }

                for ($index = 0; $index -lt $items.Count; $index++) {
                    $location = "$locationPrefix[$index]"
                    $id = Get-PropertyValue -Value $items[$index] -PropertyName ([string]$Source.idProperty) -Location $location
                    [void]$records.Add([pscustomobject]@{ id = $id; location = $location; source = [string]$Source.path })
                }
            }
            "trigger-aggregate" {
                if ($null -eq $json -or $json.PSObject.Properties.Name -notcontains 'Triggers') {
                    throw "Trigger aggregate at $relative must contain Triggers."
                }

                $items = @($json.Triggers)
                for ($index = 0; $index -lt $items.Count; $index++) {
                    $location = "$relative.Triggers[$index]"
                    $id = Get-PropertyValue -Value $items[$index] -PropertyName ([string]$Source.idProperty) -Location $location
                    [void]$records.Add([pscustomobject]@{ id = $id; location = $location; source = [string]$Source.path })
                }
            }
            default {
                throw "Unsupported source shape '$($Source.shape)' for '$($Source.path)'."
            }
        }
    }

    return $records.ToArray()
}

function Get-NamespaceRanges {
    param(
        [object]$Namespace,
        [string]$Name
    )

    if ($Namespace.PSObject.Properties.Name -contains 'ranges') {
        $configured = @($Namespace.ranges)
        if ($configured.Count -eq 0) {
            throw "Namespace '$Name' must define at least one range."
        }

        return @($configured | ForEach-Object {
            [pscustomobject]@{ min = [long]$_.min; max = [long]$_.max }
        })
    }

    if ($Namespace.PSObject.Properties.Name -notcontains 'min' -or
        $Namespace.PSObject.Properties.Name -notcontains 'max') {
        throw "Namespace '$Name' must define either ranges or min/max."
    }

    return @([pscustomobject]@{ min = [long]$Namespace.min; max = [long]$Namespace.max })
}

$resolvedRegistryPath = Resolve-RepoPath $RegistryPath
$registry = Read-Json $resolvedRegistryPath
if ([int]$registry.schemaVersion -ne 1) {
    throw "Unsupported registry schemaVersion '$($registry.schemaVersion)'."
}

$authorityRoot = Resolve-RepoPath ([string]$registry.authorityRoot)
if (!(Test-Path -LiteralPath $authorityRoot -PathType Container)) {
    throw "Authority root not found: $authorityRoot"
}

$legacyIdMax = [long]$registry.legacyIdMax
$namespaces = @($registry.namespaces)
if ($namespaces.Count -eq 0) {
    throw "Registry contains no namespaces."
}

$errors = New-Object System.Collections.Generic.List[string]
$allRecords = New-Object System.Collections.Generic.List[object]
$nameSet = @{}
$namespaceByName = @{}
foreach ($registeredNamespace in $namespaces) {
    $namespaceByName[[string]$registeredNamespace.name] = $registeredNamespace
}

function Test-CrossNamespaceReuseAllowed {
    param([string]$LeftName, [string]$RightName)

    if (!$namespaceByName.ContainsKey($LeftName) -or !$namespaceByName.ContainsKey($RightName)) {
        return $false
    }

    $left = $namespaceByName[$LeftName]
    $right = $namespaceByName[$RightName]
    $leftAllows = @($left.allowCrossNamespaceIdReuseWith | ForEach-Object { [string]$_ }) -contains $RightName
    $rightAllows = @($right.allowCrossNamespaceIdReuseWith | ForEach-Object { [string]$_ }) -contains $LeftName
    return $leftAllows -and $rightAllows
}

for ($i = 0; $i -lt $namespaces.Count; $i++) {
    $current = $namespaces[$i]
    $name = [string]$current.name
    try {
        $ranges = @(Get-NamespaceRanges -Namespace $current -Name $name)
    }
    catch {
        [void]$errors.Add($_.Exception.Message)
        $ranges = @()
    }
    if ([string]::IsNullOrWhiteSpace($name)) { [void]$errors.Add("Namespace at index $i has an empty name."); continue }
    if ($nameSet.ContainsKey($name)) { [void]$errors.Add("Duplicate namespace name '$name'.") } else { $nameSet[$name] = $true }
    for ($rangeIndex = 0; $rangeIndex -lt $ranges.Count; $rangeIndex++) {
        $range = $ranges[$rangeIndex]
        if ($range.min -le $legacyIdMax -or $range.max -lt $range.min) {
            [void]$errors.Add("Namespace '$name' has invalid range [$($range.min), $($range.max)].")
        }
        for ($j = 0; $j -lt $i; $j++) {
            $other = $namespaces[$j]
            try { $otherRanges = @(Get-NamespaceRanges -Namespace $other -Name ([string]$other.name)) }
            catch { continue }
            foreach ($otherRange in $otherRanges) {
                if ($range.min -le $otherRange.max -and $range.max -ge $otherRange.min -and
                    !(Test-CrossNamespaceReuseAllowed -LeftName $name -RightName ([string]$other.name))) {
                    [void]$errors.Add("Namespace ranges overlap: '$name' [$($range.min), $($range.max)] and '$($other.name)' [$($otherRange.min), $($otherRange.max)].")
                }
            }
        }
    }

    $namespaceRecords = New-Object System.Collections.Generic.List[object]
    foreach ($source in @($current.sources)) {
        try {
            foreach ($record in @(Get-SourceRecords -AuthorityRoot $authorityRoot -Source $source)) {
                $decorated = [pscustomobject]@{
                    id = [long]$record.id
                    location = [string]$record.location
                    source = [string]$record.source
                    namespace = $name
                }
                [void]$namespaceRecords.Add($decorated)
                [void]$allRecords.Add($decorated)
            }
        }
        catch {
            [void]$errors.Add("Failed to read source '$($source.path)' in namespace '$name': $($_.Exception.Message)")
        }
    }

    foreach ($group in @($namespaceRecords | Group-Object source, id | Where-Object Count -gt 1)) {
        $locations = @($group.Group | ForEach-Object { $_.location }) -join ", "
        [void]$errors.Add("Duplicate id in one source for namespace '$name': $locations.")
    }

    if (-not [bool]$current.allowSameIdAcrossSources) {
        foreach ($group in @($namespaceRecords | Group-Object id | Where-Object Count -gt 1)) {
            $locations = @($group.Group | ForEach-Object { $_.location }) -join ", "
            [void]$errors.Add("Cross-source id collision in namespace '$name' for id $($group.Name): $locations.")
        }
    }

    foreach ($record in $namespaceRecords) {
        $inRange = $false
        foreach ($range in $ranges) {
            if ($record.id -ge $range.min -and $record.id -le $range.max) {
                $inRange = $true
                break
            }
        }
        if ($record.id -gt $legacyIdMax -and !$inRange) {
            $rangeText = (@($ranges | ForEach-Object { "[$($_.min), $($_.max)]" }) -join ', ')
            [void]$errors.Add("Id $($record.id) at $($record.location) is outside namespace '$name' ranges $rangeText.")
        }
    }
}

foreach ($group in @($allRecords | Where-Object { $_.id -gt $legacyIdMax } | Group-Object id)) {
    $namespaceNames = @($group.Group | Select-Object -ExpandProperty namespace -Unique)
    for ($i = 0; $i -lt $namespaceNames.Count; $i++) {
        for ($j = $i + 1; $j -lt $namespaceNames.Count; $j++) {
            if (!(Test-CrossNamespaceReuseAllowed -LeftName ([string]$namespaceNames[$i]) -RightName ([string]$namespaceNames[$j]))) {
                $locations = @($group.Group | ForEach-Object { "$($_.namespace):$($_.location)" }) -join ", "
                [void]$errors.Add("Cross-namespace id collision for id $($group.Name): $locations.")
                break
            }
        }
        if ($errors.Count -gt 0 -and $errors[$errors.Count - 1] -like "Cross-namespace id collision for id $($group.Name):*") { break }
    }
}

if ($errors.Count -gt 0) {
    foreach ($message in $errors) { Write-Error "[moba-business-id] $message" -ErrorAction Continue }
    Write-Host "[moba-business-id] validation failed: errors=$($errors.Count)"
    exit 1
}

$formalCount = @($allRecords | Where-Object { $_.id -gt $legacyIdMax }).Count
$legacyCount = $allRecords.Count - $formalCount
Write-Host "[moba-business-id] validation passed: namespaces=$($namespaces.Count), records=$($allRecords.Count), formal=$formalCount, legacy=$legacyCount"

if ($Mode -eq "Validate") {
    exit 0
}

if ([string]::IsNullOrWhiteSpace($Namespace)) {
    throw "Namespace is required in Allocate mode."
}

$selected = @($namespaces | Where-Object { [string]$_.name -eq $Namespace })
if ($selected.Count -ne 1) {
    throw "Unknown namespace '$Namespace'. Valid values: $((@($namespaces | ForEach-Object { $_.name }) -join ', '))."
}

$definition = $selected[0]
$occupied = @{}
foreach ($record in @($allRecords | Where-Object { $_.namespace -eq $Namespace })) {
    $occupied[[long]$record.id] = $true
}

$allocated = New-Object System.Collections.Generic.List[long]
$allocationRanges = @(Get-NamespaceRanges -Namespace $definition -Name $Namespace)
foreach ($range in $allocationRanges) {
    for ($candidate = [long]$range.min; $candidate -le [long]$range.max -and $allocated.Count -lt $Count; $candidate++) {
        if (!$occupied.ContainsKey($candidate)) { [void]$allocated.Add($candidate) }
    }
    if ($allocated.Count -ge $Count) { break }
}

if ($allocated.Count -ne $Count) {
    throw "Namespace '$Namespace' has insufficient free IDs for requested count $Count."
}

Write-Output ($allocated -join [Environment]::NewLine)
