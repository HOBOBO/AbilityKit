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
    Assert-Equal -Expected 0 -Actual $allocate.ExitCode -Message "Allocator should succeed. Output: $($allocate.Output)"
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

    $reuseCase = Join-Path $tempRoot 'cross-namespace-reuse'
    $null = New-Item -ItemType Directory -Force -Path $reuseCase
    Write-Utf8Json -Path (Join-Path $reuseCase 'skill.json') -Value @([ordered]@{ Id = 30020101 })
    Write-Utf8Json -Path (Join-Path $reuseCase 'projectile.json') -Value @([ordered]@{ Id = 30020101 })
    $skillNamespace = New-Namespace -Name 'skill-family' -Min 30020000 -Max 30020999 -Source 'skill.json'
    $projectileNamespace = New-Namespace -Name 'projectile' -Min 30020000 -Max 30999999 -Source 'projectile.json'
    $skillNamespace.allowCrossNamespaceIdReuseWith = @('projectile')
    $projectileNamespace.allowCrossNamespaceIdReuseWith = @('skill-family')
    $reuseRegistry = New-Registry -CaseDirectory $reuseCase -Namespaces @($skillNamespace, $projectileNamespace)
    $reuse = Invoke-Tool -RegistryPath $reuseRegistry -Arguments @('-Mode', 'Validate')
    Assert-Equal -Expected 0 -Actual $reuse.ExitCode -Message 'Explicit symmetric cross-namespace reuse should validate.'
    Assert-Contains -Value $reuse.Output -Expected 'records=2' -Message 'Both cross-namespace records must remain independently counted.'

    $asymmetricCase = Join-Path $tempRoot 'cross-namespace-reuse-asymmetric'
    $null = New-Item -ItemType Directory -Force -Path $asymmetricCase
    Write-Utf8Json -Path (Join-Path $asymmetricCase 'skill.json') -Value @([ordered]@{ Id = 30020101 })
    Write-Utf8Json -Path (Join-Path $asymmetricCase 'projectile.json') -Value @([ordered]@{ Id = 30020101 })
    $skillNamespace = New-Namespace -Name 'skill-family' -Min 30020000 -Max 30020999 -Source 'skill.json'
    $projectileNamespace = New-Namespace -Name 'projectile' -Min 30020000 -Max 30999999 -Source 'projectile.json'
    $skillNamespace.allowCrossNamespaceIdReuseWith = @('projectile')
    $asymmetricRegistry = New-Registry -CaseDirectory $asymmetricCase -Namespaces @($skillNamespace, $projectileNamespace)
    $asymmetric = Invoke-Tool -RegistryPath $asymmetricRegistry -Arguments @('-Mode', 'Validate')
    Assert-Equal -Expected 1 -Actual $asymmetric.ExitCode -Message 'Cross-namespace reuse must be declared symmetrically.'
    Assert-Contains -Value $asymmetric.Output -Expected 'Cross-namespace id collision for id 30020101' -Message 'Asymmetric reuse must retain collision protection.'

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

    $rangesCase = Join-Path $tempRoot 'ranges'
    $null = New-Item -ItemType Directory -Force -Path $rangesCase
    Write-Utf8Json -Path (Join-Path $rangesCase 'items.json') -Value @(
        [ordered]@{ Id = 100 },
        [ordered]@{ Id = 200 }
    )
    $rangesRegistry = Join-Path $rangesCase 'registry.json'
    Write-Utf8Json -Path $rangesRegistry -Value ([ordered]@{
        schemaVersion = 1
        authorityRoot = $rangesCase
        legacyIdMax = 99
        namespaces = @([ordered]@{
            name = 'alpha'
            description = 'alpha'
            ranges = @(
                [ordered]@{ min = 100; max = 149 },
                [ordered]@{ min = 200; max = 249 }
            )
            allowSameIdAcrossSources = $false
            sources = @([ordered]@{ path = 'items.json'; shape = 'array'; idProperty = 'Id' })
        })
    })
    $ranges = Invoke-Tool -RegistryPath $rangesRegistry -Arguments @('-Mode', 'Allocate', '-Namespace', 'alpha', '-Count', '2')
    Assert-Equal -Expected 0 -Actual $ranges.ExitCode -Message 'Multi-range allocation should succeed.'
    Assert-Contains -Value $ranges.Output -Expected "101$([Environment]::NewLine)102" -Message 'Multi-range allocator should use the first range before the next range.'

    Write-Host '[moba-business-id-tests] passed: cases=8'
    exit 0
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
