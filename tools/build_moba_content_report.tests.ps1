$ErrorActionPreference = 'Stop'
$scriptUnderTest = Join-Path $PSScriptRoot 'build_moba_content_report.ps1'
$irScriptUnderTest = Join-Path $PSScriptRoot 'export_moba_content_ir.ps1'
$graphScriptUnderTest = Join-Path $PSScriptRoot 'build_moba_content_graph.ps1'
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('abilitykit-moba-content-report-' + [guid]::NewGuid().ToString('N'))

function Write-Utf8Json {
    param([string]$Path, [object]$Value)

    $json = $Value | ConvertTo-Json -Depth 32
    [System.IO.File]::WriteAllText($Path, $json, (New-Object System.Text.UTF8Encoding($false)))
}

function New-Case {
    param(
        [string]$Name,
        [object[]]$Characters,
        [object[]]$Skills,
        [object[]]$Models,
        [switch]$MissingHeroResource
    )

    $caseDirectory = Join-Path $tempRoot $Name
    $null = New-Item -ItemType Directory -Force -Path $caseDirectory
    Write-Utf8Json -Path (Join-Path $caseDirectory 'characters.json') -Value $Characters
    Write-Utf8Json -Path (Join-Path $caseDirectory 'skills.json') -Value @($Skills[0])
    Write-Utf8Json -Path (Join-Path $caseDirectory 'skills-extra.json') -Value @([ordered]@{ skill_id = $Skills[1].Id; FlowId = $Skills[1].FlowId })
    Write-Utf8Json -Path (Join-Path $caseDirectory 'models.json') -Value $Models
    Write-Utf8Json -Path (Join-Path $caseDirectory 'flows.json') -Value ([ordered]@{
        Items = @(
            [ordered]@{
                Id = 100
                Phases = @(
                    [ordered]@{
                        Children = @(
                            [ordered]@{
                                Timeline = [ordered]@{
                                    Events = @([ordered]@{ EffectId = 1000 })
                                }
                            }
                        )
                    }
                )
            }
        )
    })
    Write-Utf8Json -Path (Join-Path $caseDirectory 'effects.json') -Value @([ordered]@{ Id = 1000 })
    Write-Utf8Json -Path (Join-Path $caseDirectory 'hero-manifest.json') -Value ([ordered]@{
        heroes = @(
            [ordered]@{ characterId = 2; heroName = 'HeroTwo' },
            [ordered]@{ characterId = 1; heroName = 'HeroOne' }
        )
    })
    $assetDirectory = Join-Path $caseDirectory 'assets'
    $null = New-Item -ItemType Directory -Force -Path (Join-Path $assetDirectory 'characters')
    $null = New-Item -ItemType Directory -Force -Path (Join-Path $assetDirectory 'moba\placeholders')
    if (-not $MissingHeroResource) {
        [System.IO.File]::WriteAllText((Join-Path $assetDirectory 'characters\hero.prefab'), 'fixture')
    }
    [System.IO.File]::WriteAllText((Join-Path $assetDirectory 'moba\placeholders\preview.prefab'), 'fixture')

    $contract = [ordered]@{
        schemaVersion = 1
        contract = 'moba-content-dependency'
        roots = @(
            [ordered]@{
                kind = 'hero'
                manifestPath = (Join-Path $caseDirectory 'hero-manifest.json')
                collectionProperty = 'heroes'
                idProperty = 'characterId'
                nameProperty = 'heroName'
                targetTable = 'characters'
            }
        )
        tables = @(
            [ordered]@{
                name = 'characters'
                path = (Join-Path $caseDirectory 'characters.json')
                idProperty = 'Id'
                references = @(
                    [ordered]@{ path = 'ModelId'; targetTable = 'models' },
                    [ordered]@{ path = 'SkillIds'; targetTable = 'skills' },
                    [ordered]@{ path = 'OptionalSkillId'; targetTable = 'skills'; optionalZero = $true }
                )
            },
            [ordered]@{
                name = 'skills'
                idProperty = 'Id'
                sources = @(
                    [ordered]@{ path = (Join-Path $caseDirectory 'skills.json') },
                    [ordered]@{ path = (Join-Path $caseDirectory 'skills-extra.json'); idProperty = 'skill_id' }
                )
                references = @([ordered]@{ path = 'FlowId'; targetTable = 'flows' })
            },
            [ordered]@{
                name = 'flows'
                path = (Join-Path $caseDirectory 'flows.json')
                recordsPath = 'Items'
                idProperty = 'Id'
                references = @([ordered]@{ path = 'Phases.**.EffectId'; targetTable = 'effects' })
            },
            [ordered]@{ name = 'effects'; path = (Join-Path $caseDirectory 'effects.json'); idProperty = 'Id' },
            [ordered]@{ name = 'models'; path = (Join-Path $caseDirectory 'models.json'); idProperty = 'Id' }
        )
        resourceRules = @(
            [ordered]@{
                table = 'models'
                path = 'PrefabPath'
                required = $true
                placeholderPatterns = @('(^|[\\/])placeholders?([\\/]|$)')
                severity = 'warning'
                productionReachableSeverity = 'error'
                resourceRoot = $assetDirectory
                extensions = @('.prefab')
            }
        )
        externalReferences = @()
    }
    $contractPath = Join-Path $caseDirectory 'contract.json'
    Write-Utf8Json -Path $contractPath -Value $contract
    return [pscustomobject]@{ Directory = $caseDirectory; ContractPath = $contractPath }
}

function Invoke-Report {
    param([object]$Case, [string]$OutputName = 'report.json')

    $outputPath = Join-Path $Case.Directory $OutputName
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $scriptUnderTest -ContractPath $Case.ContractPath -OutputPath $outputPath -Validate 2>&1 | ForEach-Object { [string]$_ })
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    $report = if (Test-Path -LiteralPath $outputPath) {
        Get-Content -LiteralPath $outputPath -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    else { $null }
    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = ($output -join [Environment]::NewLine)
        OutputPath = $outputPath
        Report = $report
    }
}

function Assert-Equal {
    param([object]$Expected, [object]$Actual, [string]$Message)
    if ($Expected -ne $Actual) { throw "$Message Expected='$Expected' Actual='$Actual'." }
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function New-ValidCharacters {
    return @(
        [ordered]@{ Id = 2; ModelId = 200; SkillIds = @(20); OptionalSkillId = 0 },
        [ordered]@{ Id = 1; ModelId = 200; SkillIds = @(20, 10); OptionalSkillId = 0 }
    )
}

function New-ValidSkills {
    return @(
        [ordered]@{ Id = 20; FlowId = [double]100 },
        [ordered]@{ Id = 10; FlowId = [double]100 }
    )
}

function New-Models {
    return @(
        [ordered]@{ Id = 200; PrefabPath = 'characters/hero' },
        [ordered]@{ Id = 999; PrefabPath = 'moba/placeholders/preview' }
    )
}

try {
    $null = New-Item -ItemType Directory -Force -Path $tempRoot

    $successCase = New-Case -Name 'success' -Characters (New-ValidCharacters) -Skills (New-ValidSkills) -Models (New-Models)
    $success = Invoke-Report -Case $successCase -OutputName 'report-a.json'
    Assert-Equal -Expected 0 -Actual $success.ExitCode -Message "Resolved references should pass validation. Output: $($success.Output)"
    Assert-Equal -Expected 'passed' -Actual $success.Report.status -Message 'Successful report should pass.'
    Assert-Equal -Expected 0 -Actual $success.Report.summary.errors -Message 'Successful report should not contain errors.'
    Assert-Equal -Expected 1 -Actual $success.Report.summary.warnings -Message 'Unreachable placeholder should remain a warning.'
    Assert-Equal -Expected 'placeholder-resource' -Actual $success.Report.issues[0].code -Message 'Placeholder should be reported.'
    Assert-Equal -Expected $false -Actual $success.Report.issues[0].productionReachable -Message 'Unused placeholder must not block production roots.'
    Assert-Equal -Expected 1 -Actual $success.Report.roots[0].id -Message 'Roots should be sorted deterministically.'
    Assert-True -Condition (@($success.Report.edges | Where-Object propertyPath -like 'Phases*EffectId').Count -eq 1) -Message 'Recursive paths should emit one deduplicated edge.'
    Assert-True -Condition (@($success.Report.edges | Where-Object targetId -eq 0).Count -eq 0) -Message 'Optional zero references should not emit edges.'
    Assert-Equal -Expected 2 -Actual (@($success.Report.tables | Where-Object name -eq 'skills')[0].paths.Count) -Message 'Multiple table sources should be preserved in the report.'
    Assert-Equal -Expected 2 -Actual $success.Report.summary.resources -Message 'Resource validation facts should be exported for every governed model.'

    $graphIrA = Join-Path $successCase.Directory 'graph-ir-a.json'
    $diagnosticsA = Join-Path $successCase.Directory 'diagnostics-a.json'
    & powershell -NoProfile -ExecutionPolicy Bypass -File $irScriptUnderTest -ReportPath $success.OutputPath -GraphOutputPath $graphIrA -DiagnosticsOutputPath $diagnosticsA | Out-Null
    Assert-Equal -Expected 0 -Actual $LASTEXITCODE -Message 'IR export should succeed for a valid report.'
    $irGraph = Get-Content -LiteralPath $graphIrA -Raw -Encoding UTF8 | ConvertFrom-Json
    $irDiagnostics = Get-Content -LiteralPath $diagnosticsA -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-Equal -Expected 'moba-content-graph' -Actual $irGraph.graph -Message 'Graph IR should use the portable graph contract.'
    Assert-True -Condition (@($irGraph.nodes | Where-Object id -eq 'table:characters:1').Count -eq 1) -Message 'Graph IR should expose stable record node IDs.'
    Assert-True -Condition (@($irGraph.edges | Where-Object { $_.source -eq 'table:characters:1' -and $_.target -eq 'table:skills:10' }).Count -eq 1) -Message 'Graph IR should expose record-level references.'
    Assert-Equal -Expected 2 -Actual @($irGraph.resources).Count -Message 'Graph IR should preserve portable resource facts.'
    Assert-Equal -Expected 1 -Actual $irDiagnostics.summary.warnings -Message 'Diagnostics should remain separate from graph topology.'

    $graphHtmlA = Join-Path $successCase.Directory 'graph-a.html'
    $graphDotA = Join-Path $successCase.Directory 'graph-a.dot'
    & powershell -NoProfile -ExecutionPolicy Bypass -File $graphScriptUnderTest -GraphPath $graphIrA -DiagnosticsPath $diagnosticsA -HtmlOutputPath $graphHtmlA -DotOutputPath $graphDotA | Out-Null
    Assert-Equal -Expected 0 -Actual $LASTEXITCODE -Message 'Graph generation should succeed for a valid report.'
    Assert-True -Condition (Test-Path -LiteralPath $graphHtmlA -PathType Leaf) -Message 'Graph generator should write HTML.'
    Assert-True -Condition (Test-Path -LiteralPath $graphDotA -PathType Leaf) -Message 'Graph generator should write DOT.'
    Assert-True -Condition ((Get-Content -LiteralPath $graphDotA -Raw -Encoding UTF8).Contains('"characters" -> "skills"')) -Message 'DOT graph should aggregate declared table edges.'

    $secondSuccess = Invoke-Report -Case $successCase -OutputName 'report-b.json'
    $firstJson = Get-Content -LiteralPath $success.OutputPath -Raw -Encoding UTF8
    $secondJson = Get-Content -LiteralPath $secondSuccess.OutputPath -Raw -Encoding UTF8
    Assert-Equal -Expected $firstJson -Actual $secondJson -Message 'Report output should be byte-stable when timestamps are disabled.'
    $graphIrB = Join-Path $successCase.Directory 'graph-ir-b.json'
    $diagnosticsB = Join-Path $successCase.Directory 'diagnostics-b.json'
    & powershell -NoProfile -ExecutionPolicy Bypass -File $irScriptUnderTest -ReportPath $secondSuccess.OutputPath -GraphOutputPath $graphIrB -DiagnosticsOutputPath $diagnosticsB | Out-Null
    Assert-Equal -Expected (Get-Content -LiteralPath $graphIrA -Raw -Encoding UTF8) -Actual (Get-Content -LiteralPath $graphIrB -Raw -Encoding UTF8) -Message 'Graph IR should be deterministic.'
    Assert-Equal -Expected (Get-Content -LiteralPath $diagnosticsA -Raw -Encoding UTF8) -Actual (Get-Content -LiteralPath $diagnosticsB -Raw -Encoding UTF8) -Message 'Diagnostics should be deterministic.'
    $graphHtmlB = Join-Path $successCase.Directory 'graph-b.html'
    $graphDotB = Join-Path $successCase.Directory 'graph-b.dot'
    & powershell -NoProfile -ExecutionPolicy Bypass -File $graphScriptUnderTest -GraphPath $graphIrB -DiagnosticsPath $diagnosticsB -HtmlOutputPath $graphHtmlB -DotOutputPath $graphDotB | Out-Null
    Assert-Equal -Expected (Get-Content -LiteralPath $graphHtmlA -Raw -Encoding UTF8) -Actual (Get-Content -LiteralPath $graphHtmlB -Raw -Encoding UTF8) -Message 'HTML graph should be deterministic.'
    Assert-Equal -Expected (Get-Content -LiteralPath $graphDotA -Raw -Encoding UTF8) -Actual (Get-Content -LiteralPath $graphDotB -Raw -Encoding UTF8) -Message 'DOT graph should be deterministic.'

    $missingCharacters = New-ValidCharacters
    $missingCharacters[0].SkillIds = @(404)
    $missingCase = New-Case -Name 'missing' -Characters $missingCharacters -Skills (New-ValidSkills) -Models (New-Models)
    $missing = Invoke-Report -Case $missingCase
    Assert-Equal -Expected 1 -Actual $missing.ExitCode -Message 'Missing references should fail validation.'
    Assert-Equal -Expected 'failed' -Actual $missing.Report.status -Message 'Missing reference report should fail.'
    Assert-True -Condition (@($missing.Report.issues | Where-Object { $_.code -eq 'missing-reference' -and $_.targetId -eq 404 }).Count -eq 1) -Message 'Missing target should be machine-readable.'
    $missingGraphPath = Join-Path $missingCase.Directory 'graph-ir.json'
    $missingDiagnosticsPath = Join-Path $missingCase.Directory 'diagnostics.json'
    & powershell -NoProfile -ExecutionPolicy Bypass -File $irScriptUnderTest -ReportPath $missing.OutputPath -GraphOutputPath $missingGraphPath -DiagnosticsOutputPath $missingDiagnosticsPath | Out-Null
    $missingGraph = Get-Content -LiteralPath $missingGraphPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $missingDiagnostics = Get-Content -LiteralPath $missingDiagnosticsPath -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-True -Condition (@($missingGraph.nodes | Where-Object { $_.id -eq 'table:skills:404' -and $_.kind -eq 'missing-record' }).Count -eq 1) -Message 'Missing references should point at explicit missing graph nodes.'
    Assert-True -Condition (@($missingDiagnostics.items | Where-Object { $_.code -eq 'missing-reference' -and $_.nodeId -eq 'table:characters:2' -and -not [string]::IsNullOrWhiteSpace($_.edgeId) }).Count -eq 1) -Message 'Diagnostics should link back to stable graph node and edge IDs.'

    $duplicateCharacters = New-ValidCharacters
    $duplicateCharacters += [ordered]@{ Id = 1; ModelId = 200; SkillIds = @(10); OptionalSkillId = 0 }
    $duplicateCase = New-Case -Name 'duplicate' -Characters $duplicateCharacters -Skills (New-ValidSkills) -Models (New-Models)
    $duplicate = Invoke-Report -Case $duplicateCase
    Assert-Equal -Expected 1 -Actual $duplicate.ExitCode -Message 'Duplicate IDs should fail validation.'
    Assert-True -Condition (@($duplicate.Report.issues | Where-Object { $_.code -eq 'duplicate-id' -and $_.table -eq 'characters' -and $_.id -eq 1 }).Count -eq 1) -Message 'Duplicate ID should be reported once.'

    $placeholderCharacters = New-ValidCharacters
    $placeholderCharacters[0].ModelId = 999
    $placeholderCase = New-Case -Name 'reachable-placeholder' -Characters $placeholderCharacters -Skills (New-ValidSkills) -Models (New-Models)
    $placeholder = Invoke-Report -Case $placeholderCase
    Assert-Equal -Expected 1 -Actual $placeholder.ExitCode -Message 'Production-reachable placeholders should fail validation.'
    $placeholderIssue = @($placeholder.Report.issues | Where-Object { $_.code -eq 'placeholder-resource' -and $_.id -eq 999 })[0]
    Assert-Equal -Expected 'error' -Actual $placeholderIssue.severity -Message 'Reachable placeholder should be promoted to an error.'
    Assert-Equal -Expected $true -Actual $placeholderIssue.productionReachable -Message 'Reachability scope should be preserved.'

    $allowlistedCase = New-Case -Name 'allowlisted-placeholder' -Characters $placeholderCharacters -Skills (New-ValidSkills) -Models (New-Models)
    $allowlistedContract = Get-Content -LiteralPath $allowlistedCase.ContractPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $allowlistedContract.resourceRules[0] | Add-Member -NotePropertyName placeholderAllowlist -NotePropertyValue @([ordered]@{ id = 999; owner = 'test'; reason = 'fixture debt' })
    Write-Utf8Json -Path $allowlistedCase.ContractPath -Value $allowlistedContract
    $allowlisted = Invoke-Report -Case $allowlistedCase
    Assert-Equal -Expected 0 -Actual $allowlisted.ExitCode -Message 'Explicit placeholder allowlist should permit migration debt.'
    $allowedIssue = @($allowlisted.Report.issues | Where-Object { $_.code -eq 'allowed-placeholder-resource' -and $_.id -eq 999 })[0]
    Assert-Equal -Expected 'warning' -Actual $allowedIssue.severity -Message 'Allowlisted placeholder should remain visible as a warning.'
    Assert-Equal -Expected 'test' -Actual $allowedIssue.allowlistOwner -Message 'Allowlist ownership should be machine-readable.'

    $missingResourceCase = New-Case -Name 'missing-resource' -Characters (New-ValidCharacters) -Skills (New-ValidSkills) -Models (New-Models) -MissingHeroResource
    $missingResource = Invoke-Report -Case $missingResourceCase
    Assert-Equal -Expected 1 -Actual $missingResource.ExitCode -Message 'Missing production Resources files should fail validation.'
    Assert-True -Condition (@($missingResource.Report.issues | Where-Object { $_.code -eq 'missing-resource-file' -and $_.id -eq 200 }).Count -eq 1) -Message 'Missing resource file should be reported with its record ID.'

    Write-Host '[moba-content-report-tests] passed: cases=6 plus deterministic portable IR and graph output'
    exit 0
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
