param(
    [string]$ConfigPath = "tools\feishu-design-sync.local.json",
    [string]$ExportDir = "",
    [string]$SyncStatePath = "",
    [ValidateSet("native", "board")]
    [string]$MermaidMode = "board",
    [string]$Source = "",
    [Alias("DryRun")]
    [switch]$Preview,
    [switch]$ListDocuments,
    [switch]$SkipMermaidValidation,
    [switch]$SkipExport,
    [switch]$RegenerateExport,
    [switch]$Force,
    [switch]$AllowDuplicateReimport,
    [switch]$Login,
    [switch]$VerifyBoard
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$OutputEncoding = [Console]::OutputEncoding

function Resolve-RepoPath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) { return [System.IO.Path]::GetFullPath($Path) }
    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
}

function Read-JsonFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "File not found: $Path"
    }

    return Get-Content -Raw -LiteralPath $Path -Encoding UTF8 | ConvertFrom-Json
}

function Write-JsonFile([string]$Path, [object]$Value) {
    $parent = [System.IO.Path]::GetDirectoryName((Resolve-RepoPath $Path))
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }

    $json = $Value | ConvertTo-Json -Depth 20
    Set-Content -LiteralPath $Path -Value $json -Encoding UTF8
}

function Read-JsonArrayFile([string]$Path) {
    $value = Read-JsonFile $Path
    if ($value -is [System.Array]) { return @($value) }
    if ($null -ne $value.PSObject.Properties["Count"] -and $null -ne $value.PSObject.Properties["Length"]) {
        return @($value)
    }

    return @($value)
}

function Test-Placeholder([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $true }
    return $Value -like "replace-with-*" -or $Value -like "cli_xxxxxxxxx*" -or $Value -like "optional-*"
}

function Get-ConfigValue([object]$Config, [string]$Name, [object]$DefaultValue) {
    $property = $Config.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value -or [string]::IsNullOrWhiteSpace([string]$property.Value)) {
        return $DefaultValue
    }

    return $property.Value
}

function Set-ConfigValue([object]$Config, [string]$Name, [object]$Value) {
    $property = $Config.PSObject.Properties[$Name]
    if ($null -eq $property) {
        $Config | Add-Member -NotePropertyName $Name -NotePropertyValue $Value
    }
    else {
        $property.Value = $Value
    }
}

function Get-EnvironmentOverride([string]$Name, [string]$Fallback) {
    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrWhiteSpace($value)) { return $Fallback }
    return $value
}

function Ensure-ObjectProperty([object]$Object, [string]$Name, [object]$DefaultValue) {
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        $Object | Add-Member -NotePropertyName $Name -NotePropertyValue $DefaultValue
        return $DefaultValue
    }
    if ($null -eq $property.Value) {
        $property.Value = $DefaultValue
    }
    return $property.Value
}

function Join-FeishuUrl([string]$BaseUrl, [string]$Path) {
    return $BaseUrl.TrimEnd('/') + '/' + $Path.TrimStart('/')
}

function Initialize-LocalConfig([string]$ConfigPath) {
    $fullPath = Resolve-RepoPath $ConfigPath
    if (Test-Path -LiteralPath $fullPath) { return $false }

    $templatePath = Resolve-RepoPath "tools\feishu-design-sync.template.json"
    if (-not (Test-Path -LiteralPath $templatePath)) {
        throw "Config not found: $ConfigPath, and template is missing: tools\feishu-design-sync.template.json"
    }

    $parent = [System.IO.Path]::GetDirectoryName($fullPath)
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }

    Copy-Item -LiteralPath $templatePath -Destination $fullPath -Force
    return $true
}

function Invoke-MermaidValidation([object]$Config) {
    $validator = Resolve-RepoPath "tools\validate_design_mermaid.mjs"
    if (-not (Test-Path -LiteralPath $validator)) {
        throw "Mermaid validator not found: tools\validate_design_mermaid.mjs"
    }

    $mermaidEntry = Resolve-RepoPath "artifacts\mermaid-validation\node_modules\mermaid\dist\mermaid.esm.mjs"
    if (-not (Test-Path -LiteralPath $mermaidEntry)) {
        Write-Host "Installing isolated Mermaid validation dependencies..."
        & npm.cmd install --prefix (Resolve-RepoPath "artifacts\mermaid-validation") --no-audit --no-fund mermaid@11 jsdom@26
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to install Mermaid validation dependencies. Ensure Node.js and npm are available."
        }
    }

    Write-Host "Validating Mermaid diagrams..."
    & node $validator $Config.sourceDir
    if ($LASTEXITCODE -ne 0) {
        throw "Mermaid validation failed. Review artifacts/mermaid-validation/report.md."
    }
}

function Invoke-ExportPackage([object]$Config) {
    $exportScript = Resolve-RepoPath "tools\export_design_docs_for_feishu.ps1"
    if (-not (Test-Path -LiteralPath $exportScript)) {
        throw "Export script not found: tools\export_design_docs_for_feishu.ps1"
    }

    & powershell -NoProfile -ExecutionPolicy Bypass -File $exportScript -SourceDir $Config.sourceDir -OutputDir $Config.exportDir -MermaidMode $Config.mermaidMode -Clean
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to generate the Feishu export package."
    }
}

function Get-ManifestSummary([object[]]$Entries) {
    $codeBlocks = 0
    $mermaidBlocks = 0
    $tableRows = 0
    foreach ($entry in $Entries) {
        if ($null -ne $entry.stats) {
            $codeBlocks += [int]$entry.stats.codeBlocks
            $mermaidBlocks += [int]$entry.stats.mermaidBlocks
            $tableRows += [int]$entry.stats.tableRows
        }
    }

    return [pscustomobject]@{
        codeBlocks = $codeBlocks
        mermaidBlocks = $mermaidBlocks
        tableRows = $tableRows
    }
}

function Invoke-FeishuJson([string]$Method, [string]$Url, [object]$Body, [hashtable]$Headers, [int]$TimeoutSeconds) {
    $params = @{
        Method = $Method
        Uri = $Url
        Headers = $Headers
        TimeoutSec = $TimeoutSeconds
    }

    if ($null -ne $Body) {
        $params.ContentType = "application/json; charset=utf-8"
        $params.Body = ($Body | ConvertTo-Json -Depth 20)
    }

    $response = Invoke-RestMethod @params
    if ($null -ne $response.code -and [int]$response.code -ne 0) {
        $message = $response.msg
        if ([string]::IsNullOrWhiteSpace($message)) { $message = ($response | ConvertTo-Json -Depth 10) }
        throw "Feishu API error $($response.code): $message"
    }

    return $response
}

function Get-TenantAccessToken([object]$Config) {
    $endpointPath = $Config.import.endpointPaths.tenantAccessToken
    $url = Join-FeishuUrl $Config.apiBaseUrl $endpointPath
    $body = @{
        app_id = $Config.appId
        app_secret = $Config.appSecret
    }

    $response = Invoke-FeishuJson "POST" $url $body @{} ([int]$Config.requestTimeoutSeconds)
    if ([string]::IsNullOrWhiteSpace($response.tenant_access_token)) {
        throw "tenant_access_token missing in Feishu auth response."
    }

    return $response.tenant_access_token
}

function ConvertTo-UrlEncoded([string]$Value) {
    return [Uri]::EscapeDataString($Value)
}

function Get-OAuthTokenCache([object]$Config) {
    $cachePath = Resolve-RepoPath $Config.oauth.tokenCachePath
    if (-not (Test-Path -LiteralPath $cachePath)) { return $null }
    return Read-JsonFile $cachePath
}

function Test-OAuthScope([string]$GrantedScope, [string]$RequiredScope) {
    if ([string]::IsNullOrWhiteSpace($RequiredScope)) { return $true }
    if ([string]::IsNullOrWhiteSpace($GrantedScope)) { return $false }

    $granted = @{}
    foreach ($scope in ($GrantedScope -split '[\s,]+')) {
        if (-not [string]::IsNullOrWhiteSpace($scope)) { $granted[$scope] = $true }
    }
    foreach ($scope in ($RequiredScope -split '[\s,]+')) {
        if (-not [string]::IsNullOrWhiteSpace($scope) -and -not $granted.ContainsKey($scope)) {
            return $false
        }
    }
    return $true
}

function Save-OAuthTokenCache([object]$Config, [object]$TokenResponse, [string]$FallbackRefreshToken = "") {
    $expiresIn = [int]$TokenResponse.expires_in
    if ($expiresIn -le 0) { $expiresIn = 7200 }
    $refreshToken = [string]$TokenResponse.refresh_token
    if ([string]::IsNullOrWhiteSpace($refreshToken)) { $refreshToken = $FallbackRefreshToken }
    $cache = [ordered]@{
        accessToken = [string]$TokenResponse.access_token
        refreshToken = $refreshToken
        expiresAt = (Get-Date).ToUniversalTime().AddSeconds($expiresIn).ToString("o")
        updatedAt = (Get-Date).ToUniversalTime().ToString("o")
        scope = [string]$TokenResponse.scope
    }
    Write-JsonFile (Resolve-RepoPath $Config.oauth.tokenCachePath) $cache
    return $cache
}

function Request-OAuthToken([object]$Config, [hashtable]$Body) {
    $url = Join-FeishuUrl $Config.apiBaseUrl $Config.oauth.tokenEndpointPath
    $response = Invoke-FeishuJson "POST" $url $Body @{} ([int]$Config.requestTimeoutSeconds)
    if ([string]::IsNullOrWhiteSpace([string]$response.access_token)) {
        throw "user_access_token missing in Feishu OAuth response."
    }
    return $response
}

function Refresh-UserAccessToken([object]$Config, [string]$RefreshToken) {
    Write-Host "Refreshing Feishu user authorization..."
    $response = Request-OAuthToken $Config @{
        grant_type = "refresh_token"
        client_id = $Config.appId
        client_secret = $Config.appSecret
        refresh_token = $RefreshToken
    }
    return Save-OAuthTokenCache $Config $response $RefreshToken
}

function Receive-OAuthAuthorizationCode([object]$Config, [string]$State) {
    $redirectUri = [Uri]$Config.oauth.redirectUri
    if ($redirectUri.Scheme -ne "http" -or ($redirectUri.Host -ne "127.0.0.1" -and $redirectUri.Host -ne "localhost")) {
        throw "OAuth redirectUri must use local HTTP host 127.0.0.1 or localhost."
    }

    $listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, $redirectUri.Port)
    $listener.Start()
    try {
        $authorizeUrl = $Config.oauth.authorizeUrl +
            "?app_id=" + (ConvertTo-UrlEncoded $Config.appId) +
            "&redirect_uri=" + (ConvertTo-UrlEncoded $Config.oauth.redirectUri) +
            "&state=" + (ConvertTo-UrlEncoded $State)
        if (-not [string]::IsNullOrWhiteSpace([string]$Config.oauth.scope)) {
            $authorizeUrl += "&scope=" + (ConvertTo-UrlEncoded $Config.oauth.scope)
        }

        Write-Host "Opening Feishu authorization in the default browser..."
        Write-Host "If the browser does not open, visit: $authorizeUrl"
        Start-Process $authorizeUrl

        $deadline = (Get-Date).AddSeconds([int]$Config.oauth.loginTimeoutSeconds)
        while (-not $listener.Pending()) {
            if ((Get-Date) -ge $deadline) { throw "Timed out waiting for Feishu OAuth callback." }
            Start-Sleep -Milliseconds 200
        }

        $client = $listener.AcceptTcpClient()
        try {
            $stream = $client.GetStream()
            $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::ASCII, $false, 4096, $true)
            $requestLine = $reader.ReadLine()
            if ([string]::IsNullOrWhiteSpace($requestLine)) { throw "Empty OAuth callback request." }
            $requestTarget = ($requestLine -split ' ')[1]
            $callback = [Uri]("http://127.0.0.1:$($redirectUri.Port)$requestTarget")
            $query = [System.Web.HttpUtility]::ParseQueryString($callback.Query)
            $code = $query.Get("code")
            $returnedState = $query.Get("state")
            $errorValue = $query.Get("error")
            $success = -not [string]::IsNullOrWhiteSpace($code) -and $returnedState -eq $State
            $message = if ($success) { "Feishu authorization succeeded. You can close this page." } else { "Feishu authorization failed. Return to the terminal for details." }
            $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($message)
            $headers = "HTTP/1.1 200 OK`r`nContent-Type: text/plain; charset=utf-8`r`nContent-Length: $($bodyBytes.Length)`r`nConnection: close`r`n`r`n"
            $headerBytes = [System.Text.Encoding]::ASCII.GetBytes($headers)
            $stream.Write($headerBytes, 0, $headerBytes.Length)
            $stream.Write($bodyBytes, 0, $bodyBytes.Length)
            $stream.Flush()

            if (-not [string]::IsNullOrWhiteSpace($errorValue)) { throw "Feishu OAuth denied: $errorValue" }
            if ($returnedState -ne $State) { throw "Feishu OAuth state mismatch." }
            if ([string]::IsNullOrWhiteSpace($code)) { throw "Authorization code missing in Feishu OAuth callback." }
            return $code
        }
        finally {
            $client.Dispose()
        }
    }
    finally {
        $listener.Stop()
    }
}

function Start-UserOAuthLogin([object]$Config) {
    Add-Type -AssemblyName System.Web
    $state = [Guid]::NewGuid().ToString("N")
    $code = Receive-OAuthAuthorizationCode $Config $state
    $response = Request-OAuthToken $Config @{
        grant_type = "authorization_code"
        client_id = $Config.appId
        client_secret = $Config.appSecret
        code = $code
        redirect_uri = $Config.oauth.redirectUri
    }
    return Save-OAuthTokenCache $Config $response
}

function Get-UserAccessToken([object]$Config, [bool]$ForceLogin) {
    $cache = if ($ForceLogin) { $null } else { Get-OAuthTokenCache $Config }
    if ($null -ne $cache -and -not (Test-OAuthScope ([string]$cache.scope) ([string]$Config.oauth.scope))) {
        Write-Host "Stored Feishu authorization lacks required scopes; interactive login is required."
        $cache = $null
    }
    if ($null -ne $cache -and -not [string]::IsNullOrWhiteSpace([string]$cache.accessToken)) {
        $expiresAt = [DateTimeOffset]::MinValue
        if ([DateTimeOffset]::TryParse([string]$cache.expiresAt, [ref]$expiresAt) -and $expiresAt -gt [DateTimeOffset]::UtcNow.AddMinutes(5)) {
            return [string]$cache.accessToken
        }
        if (-not [string]::IsNullOrWhiteSpace([string]$cache.refreshToken)) {
            try {
                $cache = Refresh-UserAccessToken $Config ([string]$cache.refreshToken)
                return [string]$cache.accessToken
            }
            catch {
                Write-Warning "Stored Feishu authorization could not be refreshed; interactive login is required. $($_.Exception.Message)"
            }
        }
    }

    $cache = Start-UserOAuthLogin $Config
    return [string]$cache.accessToken
}

function Get-FeishuAccessToken([object]$Config, [bool]$ForceLogin) {
    if ([string]$Config.authMode -eq "tenant-app") {
        return Get-TenantAccessToken $Config
    }
    return Get-UserAccessToken $Config $ForceLogin
}

function Get-FeishuDisplayTitle([object]$Entry) {
    if (-not [string]::IsNullOrWhiteSpace([string]$Entry.feishuTitle)) { return [string]$Entry.feishuTitle }
    if (-not [string]::IsNullOrWhiteSpace([string]$Entry.suggestedFeishuTitle)) { return [string]$Entry.suggestedFeishuTitle }
    return [string]$Entry.title
}

function Get-SafeUploadFileName([string]$Title, [string]$Extension) {
    $safeTitle = $Title
    foreach ($invalidChar in [System.IO.Path]::GetInvalidFileNameChars()) {
        $safeTitle = $safeTitle.Replace([string]$invalidChar, ' ')
    }
    $safeTitle = ([regex]::Replace($safeTitle, '\s+', ' ')).Trim().TrimEnd('.')
    if ([string]::IsNullOrWhiteSpace($safeTitle)) { $safeTitle = 'Untitled design document' }
    return $safeTitle + '.' + $Extension.TrimStart('.')
}

function Send-FeishuMultipartFile([object]$Config, [string]$Token, [string]$FilePath, [object]$Entry) {
    $endpointPath = $Config.import.endpointPaths.uploadAll
    $url = Join-FeishuUrl $Config.apiBaseUrl $endpointPath
    $timeout = [int]$Config.requestTimeoutSeconds
    $fileName = Get-SafeUploadFileName (Get-FeishuDisplayTitle $Entry) $Config.import.fileExtension

    Add-Type -AssemblyName System.Net.Http
    $client = New-Object System.Net.Http.HttpClient
    $client.Timeout = [TimeSpan]::FromSeconds($timeout)
    $client.DefaultRequestHeaders.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", $Token)

    $content = New-Object System.Net.Http.MultipartFormDataContent
    $content.Add((New-Object System.Net.Http.StringContent($fileName)), "file_name")
    $content.Add((New-Object System.Net.Http.StringContent([string]$Config.import.uploadParentType)), "parent_type")
    if (-not [string]::IsNullOrWhiteSpace([string]$Config.import.uploadParentNode)) {
        $content.Add((New-Object System.Net.Http.StringContent([string]$Config.import.uploadParentNode)), "parent_node")
    }
    $uploadExtra = @{
        obj_type = [string]$Config.import.targetFormat
        file_extension = [string]$Config.import.fileExtension
    } | ConvertTo-Json -Compress
    $content.Add((New-Object System.Net.Http.StringContent($uploadExtra)), "extra")
    $content.Add((New-Object System.Net.Http.StringContent(([System.IO.FileInfo]$FilePath).Length.ToString())), "size")

    $stream = [System.IO.File]::OpenRead($FilePath)
    try {
        $fileContent = New-Object System.Net.Http.StreamContent($stream)
        $fileContent.Headers.ContentType = New-Object System.Net.Http.Headers.MediaTypeHeaderValue("text/markdown")
        $content.Add($fileContent, "file", $fileName)
        $result = $client.PostAsync($url, $content).GetAwaiter().GetResult()
        $raw = $result.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if (-not $result.IsSuccessStatusCode) {
            $hint = ""
            if ([int]$result.StatusCode -eq 403) {
                $hint = " Ensure the authorizing user can edit the target resource and the app has published drive:drive or docs:document.media:upload permission."
            }
            throw "Feishu upload failed: HTTP $([int]$result.StatusCode) $raw$hint"
        }

        $response = $raw | ConvertFrom-Json
        if ($null -ne $response.code -and [int]$response.code -ne 0) {
            throw "Feishu upload error $($response.code): $($response.msg)"
        }

        $fileToken = $response.data.file_token
        if ([string]::IsNullOrWhiteSpace($fileToken)) {
            $fileToken = $response.data.fileToken
        }
        if ([string]::IsNullOrWhiteSpace($fileToken)) {
            throw "file_token missing in Feishu upload response for $($Entry.source)."
        }

        return $fileToken
    }
    finally {
        $stream.Dispose()
        $content.Dispose()
        $client.Dispose()
    }
}

function Convert-MermaidToFeishuBoard([string]$SourcePath) {
    $converter = Resolve-RepoPath "tools\convert_mermaid_to_feishu_board.mjs"
    if (-not (Test-Path -LiteralPath $converter)) {
        throw "Mermaid Board converter not found: tools\convert_mermaid_to_feishu_board.mjs"
    }

    $outputPath = Join-Path ([System.IO.Path]::GetTempPath()) ("abilitykit-feishu-board-" + [Guid]::NewGuid().ToString("N") + ".json")
    try {
        & node $converter $SourcePath $outputPath
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $outputPath)) {
            throw "Mermaid Board conversion failed: $SourcePath"
        }
        return Read-JsonFile $outputPath
    }
    finally {
        if (Test-Path -LiteralPath $outputPath) {
            Remove-Item -LiteralPath $outputPath -Force
        }
    }
}

function Get-FeishuDocumentBlocks([object]$Config, [string]$Token, [string]$DocumentToken) {
    $headers = @{ Authorization = "Bearer $Token" }
    $items = New-Object System.Collections.Generic.List[object]
    $pageToken = ""
    do {
        $path = "/docx/v1/documents/$([Uri]::EscapeDataString($DocumentToken))/blocks?page_size=500"
        if (-not [string]::IsNullOrWhiteSpace($pageToken)) {
            $path += "&page_token=$([Uri]::EscapeDataString($pageToken))"
        }
        $response = Invoke-FeishuJson "GET" (Join-FeishuUrl $Config.apiBaseUrl $path) $null $headers ([int]$Config.requestTimeoutSeconds)
        foreach ($item in @($response.data.items)) { $items.Add($item) }
        $hasMore = [bool]$response.data.has_more
        $pageToken = [string]$response.data.page_token
    } while ($hasMore -and -not [string]::IsNullOrWhiteSpace($pageToken))
    return $items.ToArray()
}

function Find-FeishuMarkerBlock([object[]]$Blocks, [string]$Marker) {
    foreach ($block in $Blocks) {
        $json = $block | ConvertTo-Json -Depth 20 -Compress
        if ($json.IndexOf($Marker, [StringComparison]::Ordinal) -ge 0) { return $block }
    }
    return $null
}

function New-FeishuBoardBlock([object]$Config, [string]$Token, [string]$DocumentToken, [string]$ParentBlockId, [int]$Index, [object]$BoardData) {
    $path = "/docx/v1/documents/$([Uri]::EscapeDataString($DocumentToken))/blocks/$([Uri]::EscapeDataString($ParentBlockId))/children"
    $displayWidth = [Math]::Min(1200, [Math]::Max(600, [int]$BoardData.width))
    $displayHeight = [Math]::Min(800, [Math]::Max(360, [int]$BoardData.height))
    $response = Invoke-FeishuJson "POST" (Join-FeishuUrl $Config.apiBaseUrl $path) @{
        index = $Index
        children = @(@{
            block_type = 43
            board = @{
                align = 2
                width = $displayWidth
                height = $displayHeight
            }
        })
    } @{ Authorization = "Bearer $Token" } ([int]$Config.requestTimeoutSeconds)

    $block = $response.data.children[0]
    $blockId = [string]$block.block_id
    $whiteboardId = [string]$block.board.token
    if ([string]::IsNullOrWhiteSpace($blockId)) {
        throw "Feishu Board block creation returned no block_id."
    }
    if ([string]::IsNullOrWhiteSpace($whiteboardId)) {
        $blocks = Get-FeishuDocumentBlocks $Config $Token $DocumentToken
        $createdBlock = $blocks | Where-Object { [string]$_.block_id -eq $blockId } | Select-Object -First 1
        $whiteboardId = [string]$createdBlock.board.token
    }
    if ([string]::IsNullOrWhiteSpace($whiteboardId)) {
        throw "Feishu Board block creation returned no board.token for block $blockId."
    }
    return [pscustomobject]@{ blockId = $blockId; whiteboardId = $whiteboardId }
}

function Send-FeishuBoardNodes([object]$Config, [string]$Token, [string]$WhiteboardId, [object[]]$Nodes) {
    if ($Nodes.Count -eq 0) { throw "Editable Feishu Board contains no nodes." }
    $path = "/board/v1/whiteboards/$([Uri]::EscapeDataString($WhiteboardId))/nodes"
    $response = Invoke-FeishuJson "POST" (Join-FeishuUrl $Config.apiBaseUrl $path) @{
        nodes = $Nodes
        overwrite = $true
    } @{ Authorization = "Bearer $Token" } ([int]$Config.requestTimeoutSeconds)
    $createdIds = @($response.data.ids)
    if ($createdIds.Count -ne $Nodes.Count) {
        throw "Feishu Board created $($createdIds.Count) of $($Nodes.Count) nodes."
    }
}

function Get-FeishuBoardNodes([object]$Config, [string]$Token, [string]$WhiteboardId) {
    $path = "/board/v1/whiteboards/$([Uri]::EscapeDataString($WhiteboardId))/nodes"
    $response = Invoke-FeishuJson "GET" (Join-FeishuUrl $Config.apiBaseUrl $path) $null @{
        Authorization = "Bearer $Token"
    } ([int]$Config.requestTimeoutSeconds)
    if ($null -eq $response.data -or $null -eq $response.data.PSObject.Properties["nodes"]) {
        throw "Feishu Board node response contains no data.nodes for whiteboard $WhiteboardId."
    }
    return @($response.data.nodes)
}

function Test-FeishuDocumentBoards([object]$Config, [string]$Token, [string]$DocumentToken, [object]$Entry, [string]$ExportRoot) {
    $expectedCounts = New-Object System.Collections.Generic.List[int]
    foreach ($diagram in @($Entry.diagrams)) {
        $sourcePath = Join-Path $ExportRoot ([string]$diagram.source)
        if (-not (Test-Path -LiteralPath $sourcePath)) {
            throw "Mermaid source not found while verifying Board nodes: $sourcePath"
        }
        $boardData = Convert-MermaidToFeishuBoard $sourcePath
        $expectedCounts.Add(@($boardData.nodes).Count)
    }

    $blocks = Get-FeishuDocumentBlocks $Config $Token $DocumentToken
    $boardBlocks = @($blocks | Where-Object {
        [int]$_.block_type -eq 43 -and
        -not [string]::IsNullOrWhiteSpace([string]$_.board.token)
    })
    if ($boardBlocks.Count -ne $expectedCounts.Count) {
        throw "Feishu Board verification failed for $($Entry.source): expected $($expectedCounts.Count) Board blocks, found $($boardBlocks.Count)."
    }

    $actualCounts = New-Object System.Collections.Generic.List[int]
    for ($index = 0; $index -lt $boardBlocks.Count; $index++) {
        $nodes = @(Get-FeishuBoardNodes $Config $Token ([string]$boardBlocks[$index].board.token))
        $actualCounts.Add($nodes.Count)
        if ($nodes.Count -ne $expectedCounts[$index]) {
            throw "Feishu Board verification failed for $($Entry.source), Board $($index + 1): expected $($expectedCounts[$index]) nodes, found $($nodes.Count)."
        }
    }

    $totalNodes = ($actualCounts | Measure-Object -Sum).Sum
    Write-Host "Board verification succeeded: $($Entry.source)"
    Write-Host "BoardBlocks: $($boardBlocks.Count)"
    Write-Host "NodeCounts: $($actualCounts -join ',')"
    Write-Host "TotalNodes: $totalNodes"
}

function Remove-FeishuChildBlock([object]$Config, [string]$Token, [string]$DocumentToken, [string]$ParentBlockId, [int]$Index) {
    $path = "/docx/v1/documents/$([Uri]::EscapeDataString($DocumentToken))/blocks/$([Uri]::EscapeDataString($ParentBlockId))/children/batch_delete"
    Invoke-FeishuJson "DELETE" (Join-FeishuUrl $Config.apiBaseUrl $path) @{
        start_index = $Index
        end_index = $Index + 1
    } @{ Authorization = "Bearer $Token" } ([int]$Config.requestTimeoutSeconds) | Out-Null
}

function Convert-FeishuMermaidBlocks([object]$Config, [string]$Token, [string]$DocumentToken, [object]$Entry, [string]$ExportRoot, [bool]$IsResume) {
    foreach ($diagram in @($Entry.diagrams)) {
        $sourcePath = Join-Path $ExportRoot ([string]$diagram.source)
        if (-not (Test-Path -LiteralPath $sourcePath)) {
            throw "Mermaid source not found: $sourcePath"
        }
        $boardData = Convert-MermaidToFeishuBoard $sourcePath

        $blocks = Get-FeishuDocumentBlocks $Config $Token $DocumentToken
        $markerBlock = Find-FeishuMarkerBlock $blocks ([string]$diagram.marker)
        if ($null -eq $markerBlock) {
            if ($IsResume) {
                Write-Host "  Mermaid placeholder already replaced: $($diagram.marker)"
                continue
            }
            throw "Mermaid placeholder not found in imported document: $($diagram.marker)"
        }
        $parentId = [string]$markerBlock.parent_id
        $parent = $blocks | Where-Object { [string]$_.block_id -eq $parentId } | Select-Object -First 1
        if ($null -eq $parent) { throw "Parent block not found for Mermaid placeholder: $($diagram.marker)" }
        $children = @($parent.children)
        $markerIndex = [Array]::IndexOf($children, [string]$markerBlock.block_id)
        if ($markerIndex -lt 0) { throw "Mermaid placeholder index not found: $($diagram.marker)" }

        $boardBlock = $null
        try {
            $boardBlock = New-FeishuBoardBlock $Config $Token $DocumentToken $parentId $markerIndex $boardData
            Send-FeishuBoardNodes $Config $Token ([string]$boardBlock.whiteboardId) @($boardData.nodes)
            Remove-FeishuChildBlock $Config $Token $DocumentToken $parentId ($markerIndex + 1)
            Write-Host "  Editable Mermaid Board inserted: $($diagram.marker) ($(@($boardData.nodes).Count) nodes)"
        }
        catch {
            if ($null -ne $boardBlock) {
                try { Remove-FeishuChildBlock $Config $Token $DocumentToken $parentId $markerIndex }
                catch { Write-Warning "Failed to roll back incomplete Board block $($boardBlock.blockId): $($_.Exception.Message)" }
            }
            throw
        }
    }
}

function Get-EntryFolderPath([object]$Entry, [bool]$PreserveSourceHierarchy) {
    if (-not $PreserveSourceHierarchy) { return "" }

    $source = ([string]$Entry.source).Replace('\', '/').Trim('/')
    $lastSlash = $source.LastIndexOf('/')
    if ($lastSlash -lt 0) { return "" }
    return $source.Substring(0, $lastSlash)
}

function Get-StateFolder([object]$State, [string]$FolderPath, [string]$RootToken) {
    if ($null -eq $State -or $null -eq $State.folders) { return $null }

    $folder = $null
    if ($State.folders -is [System.Collections.IDictionary]) {
        if (-not $State.folders.Contains($FolderPath)) { return $null }
        $folder = $State.folders[$FolderPath]
    }
    else {
        $property = $State.folders.PSObject.Properties[$FolderPath]
        if ($null -eq $property) { return $null }
        $folder = $property.Value
    }
    if ([string]$folder.rootToken -ne $RootToken) { return $null }
    return $folder
}

function Get-FeishuChildFolders([object]$Config, [string]$Token, [string]$ParentToken) {
    $folders = New-Object System.Collections.Generic.List[object]
    $pageToken = ""
    do {
        $path = "/drive/v1/files?folder_token=$([Uri]::EscapeDataString($ParentToken))&page_size=200"
        if (-not [string]::IsNullOrWhiteSpace($pageToken)) {
            $path += "&page_token=$([Uri]::EscapeDataString($pageToken))"
        }
        $response = Invoke-FeishuJson "GET" (Join-FeishuUrl $Config.apiBaseUrl $path) $null @{
            Authorization = "Bearer $Token"
        } ([int]$Config.requestTimeoutSeconds)
        foreach ($file in @($response.data.files)) {
            if ([string]$file.type -eq "folder") { $folders.Add($file) }
        }
        $hasMore = [bool]$response.data.has_more
        $pageToken = [string]$response.data.next_page_token
    } while ($hasMore -and -not [string]::IsNullOrWhiteSpace($pageToken))

    return @($folders)
}

function Find-FeishuChildFolder([object]$Config, [string]$Token, [string]$ParentToken, [string]$Name) {
    $matches = @(Get-FeishuChildFolders $Config $Token $ParentToken | Where-Object { [string]$_.name -eq $Name })
    if ($matches.Count -gt 1) {
        throw "Multiple Feishu folders named '$Name' exist under parent token $ParentToken; cannot select one safely."
    }
    if ($matches.Count -eq 1) { return $matches[0] }
    return $null
}

function New-FeishuFolder([object]$Config, [string]$Token, [string]$ParentToken, [string]$Name) {
    $response = Invoke-FeishuJson "POST" (Join-FeishuUrl $Config.apiBaseUrl "/drive/v1/files/create_folder") @{
        name = $Name
        folder_token = $ParentToken
    } @{ Authorization = "Bearer $Token" } ([int]$Config.requestTimeoutSeconds)
    $folderToken = [string]$response.data.token
    if ([string]::IsNullOrWhiteSpace($folderToken)) {
        throw "Feishu folder creation returned no token for '$Name'."
    }
    return [pscustomobject]@{ name = $Name; token = $folderToken; type = "folder" }
}

function Resolve-FeishuFolderToken([object]$Config, [string]$Token, [object]$State, [string]$FolderPath) {
    $rootToken = [string]$Config.target.rootToken
    if ([string]::IsNullOrWhiteSpace($FolderPath)) { return $rootToken }

    $parentToken = $rootToken
    $currentPath = ""
    foreach ($segment in @($FolderPath -split '/')) {
        if ([string]::IsNullOrWhiteSpace($segment)) { continue }
        $currentPath = if ([string]::IsNullOrWhiteSpace($currentPath)) { $segment } else { "$currentPath/$segment" }
        $cached = Get-StateFolder $State $currentPath $rootToken
        if ($null -ne $cached -and -not [string]::IsNullOrWhiteSpace([string]$cached.token)) {
            $parentToken = [string]$cached.token
            continue
        }

        $folder = Find-FeishuChildFolder $Config $Token $parentToken $segment
        if ($null -eq $folder) {
            try {
                $folder = New-FeishuFolder $Config $Token $parentToken $segment
                Write-Host "  Feishu folder created: $currentPath"
            }
            catch {
                $folder = Find-FeishuChildFolder $Config $Token $parentToken $segment
                if ($null -eq $folder) { throw }
                Write-Host "  Feishu folder recovered after concurrent creation: $currentPath"
            }
        }
        else {
            Write-Host "  Feishu folder reused: $currentPath"
        }

        $folderToken = [string]$folder.token
        if ([string]::IsNullOrWhiteSpace($folderToken)) {
            throw "Feishu folder '$currentPath' has no token."
        }
        $State.folders[$currentPath] = [ordered]@{
            path = $currentPath
            name = $segment
            token = $folderToken
            parentToken = $parentToken
            rootToken = $rootToken
            resolvedAt = (Get-Date).ToString("o")
        }
        Save-SyncCheckpoint $Config $State
        $parentToken = $folderToken
    }

    return $parentToken
}

function Start-FeishuImportTask([object]$Config, [string]$Token, [string]$FileToken, [object]$Entry, [string]$MountKey) {
    $endpointPath = $Config.import.endpointPaths.createImportTask
    $url = Join-FeishuUrl $Config.apiBaseUrl $endpointPath
    $headers = @{ Authorization = "Bearer $Token" }
    $body = @{
        file_extension = $Config.import.fileExtension
        file_token = $FileToken
        type = $Config.import.targetFormat
        file_name = (Get-FeishuDisplayTitle $Entry)
        point = @{
            mount_type = $Config.target.rootType
            mount_key = $MountKey
        }
    }

    $response = Invoke-FeishuJson "POST" $url $body $headers ([int]$Config.requestTimeoutSeconds)
    $ticket = $response.data.ticket
    if ([string]::IsNullOrWhiteSpace($ticket)) {
        $ticket = $response.data.job_ticket
    }
    if ([string]::IsNullOrWhiteSpace($ticket)) {
        throw "Import task ticket missing for $($Entry.source)."
    }

    return $ticket
}

function Wait-FeishuImportTask([object]$Config, [string]$Token, [string]$Ticket) {
    $headers = @{ Authorization = "Bearer $Token" }
    $pollInterval = [int]$Config.pollIntervalSeconds
    $maxPoll = [int]$Config.maxPollSeconds
    $deadline = (Get-Date).AddSeconds($maxPoll)
    $lastData = $null

    while ((Get-Date) -lt $deadline) {
        $endpointPath = $Config.import.endpointPaths.getImportTask.Replace('{ticket}', [Uri]::EscapeDataString($Ticket))
        $url = Join-FeishuUrl $Config.apiBaseUrl $endpointPath
        $response = Invoke-FeishuJson "GET" $url $null $headers ([int]$Config.requestTimeoutSeconds)
        $data = $response.data
        if ($null -ne $data.result) { $data = $data.result }
        $lastData = $data

        $status = [string]$data.status
        $jobStatus = if ($null -ne $data.PSObject.Properties["job_status"]) { [int]$data.job_status } else { -1 }
        $jobErrorMessage = [string]$data.job_error_msg
        $resultToken = [string]$data.token
        $resultUrl = [string]$data.url
        if ($status -eq "success" -or $status -eq "succeeded" -or $status -eq "done" -or
            $jobStatus -eq 0 -or -not [string]::IsNullOrWhiteSpace($resultToken) -or
            -not [string]::IsNullOrWhiteSpace($resultUrl) -or $jobErrorMessage -eq "success") {
            return $data
        }
        if ($status -eq "failed" -or $status -eq "error" -or $jobStatus -eq 3 -or
            (-not [string]::IsNullOrWhiteSpace($jobErrorMessage) -and $jobErrorMessage -ne "success")) {
            throw "Feishu import task failed: $($data | ConvertTo-Json -Depth 10 -Compress)"
        }

        Start-Sleep -Seconds $pollInterval
    }

    $lastResult = if ($null -ne $lastData) { $lastData | ConvertTo-Json -Depth 10 -Compress } else { "no response data" }
    throw "Feishu import task timed out: $Ticket. Last result: $lastResult"
}

function Get-RemoteDocInfo([object]$TaskData) {
    $token = $TaskData.token
    if ([string]::IsNullOrWhiteSpace($token)) { $token = $TaskData.obj_token }
    if ([string]::IsNullOrWhiteSpace($token)) { $token = $TaskData.doc_token }
    if ([string]::IsNullOrWhiteSpace($token)) { $token = $TaskData.document_token }

    $url = $TaskData.url
    if ([string]::IsNullOrWhiteSpace($url)) { $url = $TaskData.document_url }
    if ([string]::IsNullOrWhiteSpace($url)) { $url = $TaskData.obj_url }

    return [pscustomobject]@{
        token = $token
        url = $url
    }
}

function Get-FileSha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-DocumentContentSha256([string]$MarkdownPath, [object]$Entry, [string]$ExportRoot) {
    $parts = New-Object System.Collections.Generic.List[string]
    $parts.Add('markdown:' + (Get-FileSha256 $MarkdownPath))
    $parts.Add('mermaidMode:' + [string]$Entry.mermaidMode)

    $diagrams = @($Entry.diagrams)
    if ($diagrams.Count -gt 0) {
        $parts.Add('boardRendererContract:feishu-board-v1')
        foreach ($diagram in $diagrams) {
            $sourcePath = Join-Path $ExportRoot ([string]$diagram.source)
            if (-not (Test-Path -LiteralPath $sourcePath)) {
                throw "Mermaid source missing for $($Entry.source): $sourcePath"
            }
            $parts.Add('diagram:' + [string]$diagram.marker + ':' + [string]$diagram.source + ':' + (Get-FileSha256 $sourcePath))
        }
    }

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes(($parts -join "`n"))
        return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Read-SyncState([string]$Path) {
    $fullPath = Resolve-RepoPath $Path
    if (-not (Test-Path -LiteralPath $fullPath)) { return $null }
    return Read-JsonFile $fullPath
}

function Get-StateDocument([object]$State, [string]$Source) {
    if ($null -eq $State -or $null -eq $State.documents) { return $null }
    return $State.documents.PSObject.Properties[$Source].Value
}

function New-SyncState([object]$PreviousState) {
    $documents = [ordered]@{}
    if ($null -ne $PreviousState -and $null -ne $PreviousState.documents) {
        foreach ($property in $PreviousState.documents.PSObject.Properties) {
            $documents[$property.Name] = $property.Value
        }
    }

    $folders = [ordered]@{}
    if ($null -ne $PreviousState -and $null -ne $PreviousState.folders) {
        foreach ($property in $PreviousState.folders.PSObject.Properties) {
            $folders[$property.Name] = $property.Value
        }
    }

    return [ordered]@{
        schemaVersion = 4
        generatedAt = (Get-Date).ToString("o")
        folders = $folders
        documents = $documents
    }
}

function Save-SyncCheckpoint([object]$Config, [object]$State) {
    $State.generatedAt = (Get-Date).ToString("o")
    Write-JsonFile (Resolve-RepoPath $Config.syncStatePath) $State
}

$configFullPath = Resolve-RepoPath $ConfigPath
$configInitialized = Initialize-LocalConfig $ConfigPath
if ($configInitialized) {
    Write-Host "Created local config from template: $ConfigPath"
    Write-Host "Fill appId, appSecret, and target.rootToken for personal OAuth, or set FEISHU_APP_ID, FEISHU_APP_SECRET, and FEISHU_ROOT_TOKEN."
}

$config = Read-JsonFile $configFullPath
Set-ConfigValue $config "apiBaseUrl" ([string](Get-ConfigValue $config "apiBaseUrl" "https://open.feishu.cn/open-apis"))
Set-ConfigValue $config "authMode" ([string](Get-ConfigValue $config "authMode" "user-oauth"))
if ([string]$config.authMode -ne "user-oauth" -and [string]$config.authMode -ne "tenant-app") {
    throw "Unsupported authMode '$($config.authMode)'. Use 'user-oauth' or 'tenant-app'."
}
Set-ConfigValue $config "exportDir" ([string](Get-ConfigValue $config "exportDir" "artifacts/feishu-design-export"))
Set-ConfigValue $config "sourceDir" ([string](Get-ConfigValue $config "sourceDir" "Docs/design"))
Set-ConfigValue $config "syncStatePath" ([string](Get-ConfigValue $config "syncStatePath" "artifacts/feishu-design-export/feishu-sync-state.local.json"))
Set-ConfigValue $config "mermaidMode" $MermaidMode
Set-ConfigValue $config "requestTimeoutSeconds" ([int](Get-ConfigValue $config "requestTimeoutSeconds" 120))
Set-ConfigValue $config "pollIntervalSeconds" ([int](Get-ConfigValue $config "pollIntervalSeconds" 3))
Set-ConfigValue $config "maxPollSeconds" ([int](Get-ConfigValue $config "maxPollSeconds" 180))
Set-ConfigValue $config "changedDocumentMode" ([string](Get-ConfigValue $config "changedDocumentMode" "block"))
Set-ConfigValue $config "appId" (Get-EnvironmentOverride "FEISHU_APP_ID" ([string]$config.appId))
Set-ConfigValue $config "appSecret" (Get-EnvironmentOverride "FEISHU_APP_SECRET" ([string]$config.appSecret))

$target = Ensure-ObjectProperty $config "target" ([pscustomobject]@{})
Ensure-ObjectProperty $target "rootType" 1 | Out-Null
Ensure-ObjectProperty $target "rootToken" "replace-with-writable-feishu-folder-token" | Out-Null
Ensure-ObjectProperty $target "preserveSourceHierarchy" $true | Out-Null
$target.rootToken = Get-EnvironmentOverride "FEISHU_ROOT_TOKEN" ([string]$target.rootToken)

$oauth = Ensure-ObjectProperty $config "oauth" ([pscustomobject]@{})
Ensure-ObjectProperty $oauth "authorizeUrl" "https://accounts.feishu.cn/open-apis/authen/v1/authorize" | Out-Null
Ensure-ObjectProperty $oauth "tokenEndpointPath" "/authen/v2/oauth/token" | Out-Null
Ensure-ObjectProperty $oauth "redirectUri" "http://127.0.0.1:8765/feishu/oauth/callback" | Out-Null

$managedUserOAuthScopes = @(
    "drive:drive",
    "docs:document.media:upload",
    "docx:document",
    "docx:document.block:convert",
    "board:whiteboard:node:create",
    "board:whiteboard:node:read"
)
$requiredUserOAuthScopes = New-Object System.Collections.Generic.List[string]
$requiredUserOAuthScopes.Add("drive:drive")
$requiredUserOAuthScopes.Add("docs:document.media:upload")
$requiredUserOAuthScopes.Add("docx:document")
if ([string]$config.mermaidMode -eq "board") {
    $requiredUserOAuthScopes.Add("board:whiteboard:node:create")
    $requiredUserOAuthScopes.Add("board:whiteboard:node:read")
}
elseif ($VerifyBoard) {
    $requiredUserOAuthScopes.Add("board:whiteboard:node:read")
}

$currentUserOAuthScope = [string](Get-ConfigValue $oauth "scope" "")
foreach ($scope in ($currentUserOAuthScope -split '[\s,]+')) {
    if (-not [string]::IsNullOrWhiteSpace($scope) -and
        $managedUserOAuthScopes -notcontains $scope -and
        -not $requiredUserOAuthScopes.Contains($scope)) {
        $requiredUserOAuthScopes.Add($scope)
    }
}
$requiredUserOAuthScope = $requiredUserOAuthScopes -join " "
Ensure-ObjectProperty $oauth "scope" $requiredUserOAuthScope | Out-Null
$oauth.scope = $requiredUserOAuthScope
Ensure-ObjectProperty $oauth "tokenCachePath" "artifacts/feishu-design-export/feishu-user-token.local.json" | Out-Null
Ensure-ObjectProperty $oauth "loginTimeoutSeconds" 180 | Out-Null

$import = Ensure-ObjectProperty $config "import" ([pscustomobject]@{})
Ensure-ObjectProperty $import "fileExtension" "md" | Out-Null
Ensure-ObjectProperty $import "targetFormat" "docx" | Out-Null
Ensure-ObjectProperty $import "uploadParentType" "ccm_import_open" | Out-Null
Ensure-ObjectProperty $import "uploadParentNode" "" | Out-Null
$endpointPaths = Ensure-ObjectProperty $import "endpointPaths" ([pscustomobject]@{})
Ensure-ObjectProperty $endpointPaths "tenantAccessToken" "/auth/v3/tenant_access_token/internal" | Out-Null
Ensure-ObjectProperty $endpointPaths "uploadAll" "/drive/v1/medias/upload_all" | Out-Null
Ensure-ObjectProperty $endpointPaths "createImportTask" "/drive/v1/import_tasks" | Out-Null
Ensure-ObjectProperty $endpointPaths "getImportTask" "/drive/v1/import_tasks/{ticket}" | Out-Null

if (-not [string]::IsNullOrWhiteSpace($ExportDir)) {
    $config.exportDir = $ExportDir
}
if (-not [string]::IsNullOrWhiteSpace($SyncStatePath)) {
    $config.syncStatePath = $SyncStatePath
}

if (-not $SkipMermaidValidation) {
    Invoke-MermaidValidation $config
}

$exportFull = Resolve-RepoPath $config.exportDir
$manifestPath = Join-Path $exportFull "manifest.json"
if (-not $SkipExport) {
    Invoke-ExportPackage $config
}
elseif (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Manifest not found while -SkipExport is active: $manifestPath"
}

$exportFull = Resolve-RepoPath $config.exportDir
$manifestPath = Join-Path $exportFull "manifest.json"
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Manifest not found after export: $manifestPath"
}

$entries = Read-JsonArrayFile $manifestPath
if ($entries.Count -eq 0) {
    throw "Manifest contains no documents: $manifestPath"
}
if (-not [string]::IsNullOrWhiteSpace($Source)) {
    $normalizedSource = $Source.Replace('\', '/')
    $entries = @($entries | Where-Object { [string]$_.source -eq $normalizedSource })
    if ($entries.Count -ne 1) {
        throw "Expected exactly one manifest document for -Source '$normalizedSource', found $($entries.Count)."
    }
}
if ($VerifyBoard -and [string]::IsNullOrWhiteSpace($Source)) {
    throw "-VerifyBoard requires -Source to select exactly one synchronized document."
}
if ($VerifyBoard -and $Preview) {
    throw "-VerifyBoard performs live read-only API checks and cannot be combined with -Preview."
}

$configLooksSecret = -not (Test-Placeholder ([string]$config.appId)) -and -not (Test-Placeholder ([string]$config.appSecret)) -and -not (Test-Placeholder ([string]$config.target.rootToken))
$effectiveDryRun = $Preview
if (-not $configLooksSecret) {
    if ($VerifyBoard) {
        throw "-VerifyBoard requires configured appId, appSecret, and target.rootToken."
    }
    $effectiveDryRun = $true
}
$allowVersionedReimport = $AllowDuplicateReimport -or ([string]$config.changedDocumentMode -eq "versioned-reimport")

$summary = Get-ManifestSummary $entries
Write-Host "Feishu design sync"
Write-Host "Config: $ConfigPath"
Write-Host "AuthMode: $($config.authMode)"
Write-Host "ExportDir: $($config.exportDir)"
Write-Host "MermaidMode: $($config.mermaidMode)"
if (-not [string]::IsNullOrWhiteSpace($Source)) { Write-Host "Source: $normalizedSource" }
Write-Host "Documents: $($entries.Count), CodeBlocks: $($summary.codeBlocks), Mermaid: $($summary.mermaidBlocks), TableRows: $($summary.tableRows)"
Write-Host "DryRun: $effectiveDryRun"

$previousState = Read-SyncState $config.syncStatePath
$state = New-SyncState $previousState
$planLines = New-Object System.Collections.Generic.List[string]
$planLines.Add("# Feishu design sync plan")
$planLines.Add("")
$planLines.Add("GeneratedAt: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')")
$planLines.Add("DryRun: $effectiveDryRun")
$planLines.Add("ChangedDocumentMode: $($config.changedDocumentMode)")
$planLines.Add("PreserveSourceHierarchy: $([bool]$config.target.preserveSourceHierarchy)")
$planLines.Add("UpdateModel: import-task; changed documents are blocked unless duplicate reimport is explicitly enabled.")
$planLines.Add("")
$planLines.Add("| Order | Action | FeishuTitle | TargetFolder | Source | Markdown | RemoteToken | RemoteUrl |")
$planLines.Add("|-------|--------|-------------|--------------|--------|----------|-------------|-----------|")

$token = $null
if (-not $effectiveDryRun) {
    $token = Get-FeishuAccessToken $config $Login
}

if ($VerifyBoard) {
    $verifyEntry = $entries[0]
    $verifyDocument = Get-StateDocument $previousState $verifyEntry.source
    $verifyDocumentToken = if ($null -ne $verifyDocument) { [string]$verifyDocument.feishuNodeToken } else { "" }
    if ([string]::IsNullOrWhiteSpace($verifyDocumentToken)) {
        throw "No synchronized Feishu document token found for $($verifyEntry.source)."
    }
    Test-FeishuDocumentBoards $config $token $verifyDocumentToken $verifyEntry $exportFull
    return
}

foreach ($entry in $entries) {
    $markdownPath = Join-Path $exportFull $entry.markdown
    if (-not (Test-Path -LiteralPath $markdownPath)) {
        throw "Markdown export missing for $($entry.source): $markdownPath"
    }

    $contentHash = Get-DocumentContentSha256 $markdownPath $entry $exportFull
    $feishuTitle = Get-FeishuDisplayTitle $entry
    $folderPath = Get-EntryFolderPath $entry ([bool]$config.target.preserveSourceHierarchy)
    $folderDisplay = if ([string]::IsNullOrWhiteSpace($folderPath)) { "/" } else { "/$folderPath" }
    $previousDocument = Get-StateDocument $previousState $entry.source
    $actualFolderPath = if ($null -ne $previousDocument -and $null -ne $previousDocument.PSObject.Properties["targetFolderPath"]) { [string]$previousDocument.targetFolderPath } else { $null }
    $actualFolderToken = if ($null -ne $previousDocument -and $null -ne $previousDocument.PSObject.Properties["targetFolderToken"]) { [string]$previousDocument.targetFolderToken } else { $null }
    $syncedAt = $null
    $ticket = if ($null -ne $previousDocument) { [string]$previousDocument.lastImportTicket } else { $null }
    $remoteToken = if ($null -ne $previousDocument) { $previousDocument.feishuNodeToken } else { $entry.feishuNodeToken }
    $remoteUrl = if ($null -ne $previousDocument) { $previousDocument.feishuDocumentUrl } else { $entry.feishuDocumentUrl }
    $action = "create"
    if ($null -ne $previousDocument -and -not [string]::IsNullOrWhiteSpace([string]$previousDocument.feishuNodeToken)) {
        if ($Force) {
            $action = "force-reimport"
        }
        elseif ($previousDocument.contentSha256 -eq $contentHash) {
            $action = "skip-unchanged"
        }
        else {
            $action = "changed-needs-update"
        }
    }
    elseif ($null -ne $previousDocument -and
            $previousDocument.contentSha256 -eq $contentHash -and
            -not [string]::IsNullOrWhiteSpace($ticket)) {
        $action = "resume-import"
    }

    if ($effectiveDryRun) {
        if ($ListDocuments) {
            Write-Host ("[dry-run] [{0}] {1}. {2} => {3} <= {4}" -f $action, $entry.order, $entry.title, $folderDisplay, $entry.markdown)
        }
    }
    elseif ($action -eq "create" -or $action -eq "resume-import" -or (($action -eq "force-reimport" -or $action -eq "changed-needs-update") -and $allowVersionedReimport)) {
        if ($action -eq "force-reimport") {
            Write-Warning "Force-reimporting document as a new Feishu page because duplicate reimport is explicitly enabled: $($entry.source)"
        }
        elseif ($action -eq "changed-needs-update") {
            Write-Warning "Reimporting changed document as a new Feishu page because duplicate reimport is explicitly enabled: $($entry.source)"
            $action = "duplicate-reimport"
        }
        Write-Host ("[sync] [{0}] {1}. {2} => {3}" -f $action, $entry.order, $entry.title, $folderDisplay)
        if ($action -ne "resume-import") {
            $mountKey = Resolve-FeishuFolderToken $config $token $state $folderPath
            $actualFolderPath = $folderPath
            $actualFolderToken = $mountKey
            $fileToken = Send-FeishuMultipartFile $config $token $markdownPath $entry
            $ticket = Start-FeishuImportTask $config $token $fileToken $entry $mountKey
            $state.documents[$entry.source] = [ordered]@{
                source = $entry.source
                title = $entry.title
                feishuTitle = $feishuTitle
                slug = $entry.slug
                targetFolderPath = $folderPath
                targetFolderToken = $mountKey
                contentSha256 = $contentHash
                feishuNodeToken = $null
                feishuDocumentUrl = $null
                syncedAt = $null
                lastImportTicket = $ticket
            }
            Save-SyncCheckpoint $config $state
        }
        $taskData = Wait-FeishuImportTask $config $token $ticket
        $remote = Get-RemoteDocInfo $taskData
        $remoteToken = $remote.token
        $remoteUrl = $remote.url
        $syncedAt = (Get-Date).ToString("o")
        if ([string]::IsNullOrWhiteSpace([string]$remoteToken)) {
            throw "Feishu import task succeeded but returned no document token: $ticket. Result: $($taskData | ConvertTo-Json -Depth 10 -Compress)"
        }
        if ([string]$entry.mermaidMode -eq "board" -and @($entry.diagrams).Count -gt 0) {
            Convert-FeishuMermaidBlocks $config $token $remoteToken $entry $exportFull ($action -eq "resume-import")
        }
    }
    elseif ($action -eq "changed-needs-update" -or $action -eq "force-reimport") {
        throw "Document already has a Feishu mapping and reimport would create a duplicate: $($entry.source). Review the sync plan, then pass -AllowDuplicateReimport only when a replacement page is intentional."
    }

    $state.documents[$entry.source] = [ordered]@{
        source = $entry.source
        title = $entry.title
        feishuTitle = $feishuTitle
        slug = $entry.slug
        targetFolderPath = $actualFolderPath
        targetFolderToken = $actualFolderToken
        contentSha256 = $contentHash
        feishuNodeToken = $remoteToken
        feishuDocumentUrl = $remoteUrl
        syncedAt = if ($null -ne $syncedAt) { $syncedAt } elseif ($null -ne $previousDocument) { $previousDocument.syncedAt } else { $null }
        lastImportTicket = if ($null -ne $ticket) { $ticket } elseif ($null -ne $previousDocument) { $previousDocument.lastImportTicket } else { $null }
    }
    if (-not $effectiveDryRun) { Save-SyncCheckpoint $config $state }
    $planLines.Add("| $($entry.order) | $action | $feishuTitle | $folderDisplay | $($entry.source) | $($entry.markdown) | $remoteToken | $remoteUrl |")
}

if ([string]::IsNullOrWhiteSpace($Source) -and $null -ne $previousState -and $null -ne $previousState.documents) {
    foreach ($property in $previousState.documents.PSObject.Properties) {
        if ($null -eq ($entries | Where-Object { $_.source -eq $property.Name } | Select-Object -First 1)) {
            $deletedFolderPath = if ($null -ne $property.Value.PSObject.Properties["targetFolderPath"] -and -not [string]::IsNullOrWhiteSpace([string]$property.Value.targetFolderPath)) { "/$($property.Value.targetFolderPath)" } else { "/" }
            $planLines.Add("| - | local-deleted | $($property.Value.title) | $deletedFolderPath | $($property.Name) | - | $($property.Value.feishuNodeToken) | $($property.Value.feishuDocumentUrl) |")
        }
    }
}

$planPath = Join-Path $exportFull "feishu-sync-plan.md"
Set-Content -LiteralPath $planPath -Value ($planLines -join [Environment]::NewLine) -Encoding UTF8
if (-not $effectiveDryRun) {
    Write-JsonFile (Resolve-RepoPath $config.syncStatePath) $state
}

Write-Host "Sync plan: $planPath"
if ($effectiveDryRun) {
    Write-Host "Sync state: unchanged (preview mode)"
}
else {
    Write-Host "Sync state: $($config.syncStatePath)"
}
if ($effectiveDryRun -and -not $ListDocuments) {
    Write-Host "Document details are in the sync plan. Pass -ListDocuments to print every document in the terminal."
}
if ($effectiveDryRun -and -not $configLooksSecret) {
    Write-Host "App credentials or root token are missing; preview mode was forced."
}
if ($effectiveDryRun -and $configLooksSecret) {
    Write-Host "Preview completed. Run without -Preview to authorize and synchronize."
}
if (-not $effectiveDryRun -and $allowVersionedReimport) {
    Write-Host "Changed documents use versioned reimport; their Feishu URL may change."
}
