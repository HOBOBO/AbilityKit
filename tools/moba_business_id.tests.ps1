$ErrorActionPreference = 'Stop'
$scriptUnderTest = Join-Path $PSScriptRoot 'moba_business_id.ps1'
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('abilitykit-moba-business-id-' + [guid]::NewGuid().ToString('N'))

function Write-Utf8Json {
    param(
        [string]$Path,
        [object]$Value
    )

    $json = $Value | ConvertTo-Json -Depth 12
    [System.IO.File]::WriteAllText($Path, $json, (New-Object System.Text.UTF8Encoding($false)))
}

function New-Registry {
    param(
        [string]$CaseDirectory,
        [object[]]$Namespaces
    )

    $registryPath = Join-Path $CaseDirectory 'registry.json'
    Write-Utf8Json -Path $registryPath -Value ([ordered]@{
        schemaVersion = 1
        authorityRoot = $CaseDirectory
        legacyIdMax = 99
        namespaces = $Namespaces
    })
    return $registryPath
}

function New-Namespace {
    param(
        [string]$Name,
        [long]$Min,
        [long]$Max,
        [string]$Source
    )

    return [ordered]@{
        name = $Name
        description = $Name
        min = $Min
        max = $Max
        allowSameIdAcrossSources = $false
        sources = @(
            [ordered]@{ path = $Source; shape = 'array'; idProperty = 'Id' }
        )
    }
}

function Invoke-Tool {
    param(
        [string]$RegistryPath,
        [string[]]$Arguments
    )

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $scriptUnderTest @Arguments -RegistryPath $RegistryPath 2>&1 | ForEach-Object { [string]$_ })
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = ($output -join [Environment]::NewLine)
    }
}

function Assert-Equal {
    param([object]$Expected, [object]$Actual, [string]$Message)
    if ($Expected -ne $Actual) {
        throw "$Message Expected='$Expected' Actual='$Actual'."
    }
}

function Assert-Contains {
    param([string]$Value, [string]$Expected, [string]$Message)
    $normalizedValue = $Value -replace '\s+', ''
    $normalizedExpected = $Expected -replace '\s+', ''
    if ($normalizedValue.IndexOf($normalizedExpected, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "$Message Missing='$Expected'. Output: $Value"
    }
}

try {
    $null = New-Item -ItemType Directory -Force -Path $tempRoot

    $allocateCase = Join-Path $tempRoot 'allocate'
    $null = New-Item -ItemType Directory -Force -Path $allocateCase
    Write-Utf8Json -Path (Join-Path $allocateCase 'items.json') -Value @([ordered]@{ Id = 100 })
    $allocateRegistry = New-Registry -CaseDirectory $allocateCase -Namespaces @(
        (New-Namespace -Name 'alpha' -Min 100 -Max 199 -Source 'items.json')
    )
    $allocate = Invoke-Tool -RegistryPath $allocateRegistry -Arguments @('-Mode', 'Allocate', '-Namespace', 'alpha', '-Count', '2')
    Assert-Equal -Expected 0 -Actual $allocate.ExitCode -Message 'Allocator should succeed.'
    Assert-Contains -Value $allocate.Output -Expected "101$([Environment]::NewLine)102" -Message 'Allocator should return the lowest free IDs.'

    $duplicateCase = Join-Path $tempRoot 'duplicate'
    $null = New-Item -ItemType Directory -Force -Path $duplicateCase
    Write-Utf8Json -Path (Join-Path $duplicateCase 'items.json') -Value @([ordered]@{ Id = 100 }, [ordered]@{ Id = 100 })
    $duplicateRegistry = New-Registry -CaseDirectory $duplicateCase -Namespaces @(
        (New-Namespace -Name 'alpha' -Min 100 -Max 199 -Source 'items.json')
    )
    $duplicate = Invoke-Tool -RegistryPath $duplicateRegistry -Arguments @('-Mode', 'Validate')
    Assert-Equal -Expected 1 -Actual $duplicate.ExitCode -Message 'Duplicate validation should fail.'
    Assert-Contains -Value $duplicate.Output -Expected 'Duplicate id in one source' -Message 'Duplicate validation should identify the collision kind.'

    $rangeCase = Join-Path $tempRoot 'range'
    $null = New-Item -ItemType Directory -Force -Path $rangeCase
    Write-Utf8Json -Path (Join-Path $rangeCase 'items.json') -Value @([ordered]@{ Id = 250 })
    $rangeRegistry = New-Registry -CaseDirectory $rangeCase -Namespaces @(
        (New-Namespace -Name 'alpha' -Min 100 -Max 199 -Source 'items.json')
    )
    $range = Invoke-Tool -RegistryPath $rangeRegistry -Arguments @('-Mode', 'Validate')
    Assert-Equal -Expected 1 -Actual $range.ExitCode -Message 'Range validation should fail.'
    Assert-Contains -Value $range.Output -Expected "outside namespace 'alpha' range" -Message 'Range validation should identify the owning namespace.'

    $collisionCase = Join-Path $tempRoot 'cross-namespace'
    $null = New-Item -ItemType Directory -Force -Path $collisionCase
    Write-Utf8Json -Path (Join-Path $collisionCase 'alpha.json') -Value @([ordered]@{ Id = 200 })
    Write-Utf8Json -Path (Join-Path $collisionCase 'beta.json') -Value @([ordered]@{ Id = 200 })
    $collisionRegistry = New-Registry -CaseDirectory $collisionCase -Namespaces @(
        (New-Namespace -Name 'alpha' -Min 100 -Max 199 -Source 'alpha.json'),
        (New-Namespace -Name 'beta' -Min 200 -Max 299 -Source 'beta.json')
    )
    $collision = Invoke-Tool -RegistryPath $collisionRegistry -Arguments @('-Mode', 'Validate')
    Assert-Equal -Expected 1 -Actual $collision.ExitCode -Message 'Cross-namespace collision validation should fail.'
    Assert-Contains -Value $collision.Output -Expected 'Cross-namespace id collision for id 200' -Message 'Collision validation should report the shared formal ID.'

    $aggregateCase = Join-Path $tempRoot 'trigger-aggregate'
    $null = New-Item -ItemType Directory -Force -Path $aggregateCase
    Write-Utf8Json -Path (Join-Path $aggregateCase 'ability_trigger_plans.json') -Value ([ordered]@{
        FormatVersion = 1
        Triggers = @([ordered]@{ TriggerId = 100 }, [ordered]@{ TriggerId = 101 })
    })
    $aggregateNamespace = New-Namespace -Name 'alpha' -Min 100 -Max 199 -Source 'ability_trigger_plans.json'
    $aggregateNamespace.sources = @([ordered]@{
        path = 'ability_trigger_plans.json'
        shape = 'trigger-aggregate'
        idProperty = 'TriggerId'
    })
    $aggregateRegistry = New-Registry -CaseDirectory $aggregateCase -Namespaces @($aggregateNamespace)
    $aggregate = Invoke-Tool -RegistryPath $aggregateRegistry -Arguments @('-Mode', 'Validate')
    Assert-Equal -Expected 0 -Actual $aggregate.ExitCode -Message 'Published trigger aggregate should validate.'
    Assert-Contains -Value $aggregate.Output -Expected 'records=2' -Message 'Both Trigger Authoring records must be counted.'

    Write-Host '[moba-business-id-tests] passed: cases=5'
    exit 0
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
