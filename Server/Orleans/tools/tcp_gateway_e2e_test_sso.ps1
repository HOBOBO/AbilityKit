$ErrorActionPreference = 'Stop'

function Write-U32([byte[]]$buf, [int]$off, [UInt32]$v) { [BitConverter]::GetBytes($v).CopyTo($buf, $off) }
function Write-U16([byte[]]$buf, [int]$off, [UInt16]$v) { [BitConverter]::GetBytes($v).CopyTo($buf, $off) }
function Read-Exact([System.IO.Stream]$s, [int]$n) { $b = New-Object byte[] $n; $o = 0; while ($o -lt $n) { $r = $s.Read($b, $o, $n - $o); if ($r -le 0) { throw 'EOF' }; $o += $r }; return $b }
function Send-Frame([System.IO.Stream]$s, [UInt16]$flags, [UInt32]$op, [UInt32]$seq, [byte[]]$payload) { if ($null -eq $payload) { $payload = @() }; $hdrSize = 16; $frameLen = [UInt32]($hdrSize + $payload.Length); $total = 4 + $frameLen; $buf = New-Object byte[] $total; Write-U32 $buf 0 $frameLen; Write-U16 $buf 4 $flags; Write-U16 $buf 6 ([UInt16]$hdrSize); Write-U32 $buf 8 $op; Write-U32 $buf 12 $seq; Write-U32 $buf 16 ([UInt32]$payload.Length); if ($payload.Length -gt 0) { $payload.CopyTo($buf, 20) }; $s.Write($buf, 0, $buf.Length); $s.Flush(); $lenBytes = Read-Exact $s 4; $respFrameLen = [BitConverter]::ToUInt32($lenBytes, 0); $resp = Read-Exact $s ([int]$respFrameLen); $out = New-Object byte[] (4 + $respFrameLen); $lenBytes.CopyTo($out, 0); $resp.CopyTo($out, 4); return $out }
function Parse-Response([byte[]]$frame) { $flags = [BitConverter]::ToUInt16($frame, 4); $op = [BitConverter]::ToUInt32($frame, 8); $seq = [BitConverter]::ToUInt32($frame, 12); $payloadLen = [BitConverter]::ToUInt32($frame, 16); $payload = if ($payloadLen -gt 0) { $frame[20..(19 + $payloadLen)] } else { @() }; $status = if ($payloadLen -ge 4) { [BitConverter]::ToInt32($payload, 0) } else { $null }; $biz = if ($payloadLen -gt 4) { [System.Text.Encoding]::UTF8.GetString($payload, 4, $payloadLen - 4) } else { '' }; return [PSCustomObject]@{ Flags = $flags; OpCode = $op; Seq = $seq; Status = $status; BizJson = $biz } }

$client = New-Object System.Net.Sockets.TcpClient('127.0.0.1', 4000)
$stream = $client.GetStream()
$seq = 200
$REQ = 8
$enc = [System.Text.Encoding]::UTF8

$accountId = 'user_sso_1'

# Create session first time (op=122)
$req1 = @{ accountId = $accountId; expireSeconds = 600; kickExisting = $false }
$r1 = Parse-Response (Send-Frame $stream $REQ 122 $seq ($enc.GetBytes(($req1 | ConvertTo-Json -Compress))))
Write-Host 'CreateSession#1:' ($r1 | ConvertTo-Json -Compress)
$seq++

# Create session again with kickExisting=false should return same token
$r2 = Parse-Response (Send-Frame $stream $REQ 122 $seq ($enc.GetBytes(($req1 | ConvertTo-Json -Compress))))
Write-Host 'CreateSession#2(no-kick):' ($r2 | ConvertTo-Json -Compress)
$seq++

$t1 = ($r1.BizJson | ConvertFrom-Json).sessionToken
$t2 = ($r2.BizJson | ConvertFrom-Json).sessionToken
if ($t1 -ne $t2) { throw 'Expected same token when kickExisting=false' }

# Create session with kickExisting=true should issue new token and return kicked token
$reqKick = @{ accountId = $accountId; expireSeconds = 600; kickExisting = $true }
$r3 = Parse-Response (Send-Frame $stream $REQ 122 $seq ($enc.GetBytes(($reqKick | ConvertTo-Json -Compress))))
Write-Host 'CreateSession#3(kick):' ($r3 | ConvertTo-Json -Compress)
$seq++

$t3 = ($r3.BizJson | ConvertFrom-Json).sessionToken
$kicked = ($r3.BizJson | ConvertFrom-Json).kickedSessionToken
if ($kicked -ne $t1) { throw 'Expected kickedSessionToken == old token' }
if ($t3 -eq $t1) { throw 'Expected new token when kickExisting=true' }

# Renew old token should now be invalid
$renewOld = @{ sessionToken = $t1; extendSeconds = 60 }
$r4 = Parse-Response (Send-Frame $stream $REQ 120 $seq ($enc.GetBytes(($renewOld | ConvertTo-Json -Compress))))
Write-Host 'RenewOldToken:' ($r4 | ConvertTo-Json -Compress)
$seq++

# Renew new token should be valid
$renewNew = @{ sessionToken = $t3; extendSeconds = 60 }
$r5 = Parse-Response (Send-Frame $stream $REQ 120 $seq ($enc.GetBytes(($renewNew | ConvertTo-Json -Compress))))
Write-Host 'RenewNewToken:' ($r5 | ConvertTo-Json -Compress)
$seq++

$client.Close()
