$ErrorActionPreference = 'Stop'

function Write-U32([byte[]]$buf, [int]$off, [UInt32]$v) { [BitConverter]::GetBytes($v).CopyTo($buf, $off) }
function Write-U16([byte[]]$buf, [int]$off, [UInt16]$v) { [BitConverter]::GetBytes($v).CopyTo($buf, $off) }
function Read-Exact([System.IO.Stream]$s, [int]$n) { $b = New-Object byte[] $n; $o = 0; while ($o -lt $n) { $r = $s.Read($b, $o, $n - $o); if ($r -le 0) { throw 'EOF' }; $o += $r }; return $b }
function Send-Frame([System.IO.Stream]$s, [UInt16]$flags, [UInt32]$op, [UInt32]$seq, [byte[]]$payload) { if ($null -eq $payload) { $payload = @() }; $hdrSize = 16; $frameLen = [UInt32]($hdrSize + $payload.Length); $total = 4 + $frameLen; $buf = New-Object byte[] $total; Write-U32 $buf 0 $frameLen; Write-U16 $buf 4 $flags; Write-U16 $buf 6 ([UInt16]$hdrSize); Write-U32 $buf 8 $op; Write-U32 $buf 12 $seq; Write-U32 $buf 16 ([UInt32]$payload.Length); if ($payload.Length -gt 0) { $payload.CopyTo($buf, 20) }; $s.Write($buf, 0, $buf.Length); $s.Flush(); $lenBytes = Read-Exact $s 4; $respFrameLen = [BitConverter]::ToUInt32($lenBytes, 0); $resp = Read-Exact $s ([int]$respFrameLen); $out = New-Object byte[] (4 + $respFrameLen); $lenBytes.CopyTo($out, 0); $resp.CopyTo($out, 4); return $out }
function Parse-Response([byte[]]$frame) { $flags = [BitConverter]::ToUInt16($frame, 4); $op = [BitConverter]::ToUInt32($frame, 8); $seq = [BitConverter]::ToUInt32($frame, 12); $payloadLen = [BitConverter]::ToUInt32($frame, 16); $payload = if ($payloadLen -gt 0) { $frame[20..(19 + $payloadLen)] } else { @() }; $status = if ($payloadLen -ge 4) { [BitConverter]::ToInt32($payload, 0) } else { $null }; $biz = if ($payloadLen -gt 4) { [System.Text.Encoding]::UTF8.GetString($payload, 4, $payloadLen - 4) } else { '' }; return [PSCustomObject]@{ Flags = $flags; OpCode = $op; Seq = $seq; Status = $status; BizJson = $biz } }

$client = New-Object System.Net.Sockets.TcpClient('127.0.0.1', 4000)
$stream = $client.GetStream()
$seq = 300
$REQ = 8
$enc = [System.Text.Encoding]::UTF8

# GuestLogin
$loginResp = Parse-Response (Send-Frame $stream $REQ 100 $seq ($enc.GetBytes('{}')))
Write-Host 'GuestLogin:' ($loginResp | ConvertTo-Json -Compress)
$login = $loginResp.BizJson | ConvertFrom-Json
$token = $login.sessionToken
$seq++

# Renew rotateToken=true
$renewRotate = @{ sessionToken = $token; extendSeconds = 600; rotateToken = $true }
$r1 = Parse-Response (Send-Frame $stream $REQ 120 $seq ($enc.GetBytes(($renewRotate | ConvertTo-Json -Compress))))
Write-Host 'RenewRotate:' ($r1 | ConvertTo-Json -Compress)
$seq++

$newToken = ($r1.BizJson | ConvertFrom-Json).sessionToken
if ([string]::IsNullOrWhiteSpace($newToken)) { throw 'Expected sessionToken in RenewSessionResponse when rotateToken=true' }
if ($newToken -eq $token) { throw 'Expected new token when rotateToken=true' }

# Renew old token should be invalid
$renewOld = @{ sessionToken = $token; extendSeconds = 60; rotateToken = $false }
$r2 = Parse-Response (Send-Frame $stream $REQ 120 $seq ($enc.GetBytes(($renewOld | ConvertTo-Json -Compress))))
Write-Host 'RenewOldToken:' ($r2 | ConvertTo-Json -Compress)
$seq++

# Renew new token should be valid
$renewNew = @{ sessionToken = $newToken; extendSeconds = 60; rotateToken = $false }
$r3 = Parse-Response (Send-Frame $stream $REQ 120 $seq ($enc.GetBytes(($renewNew | ConvertTo-Json -Compress))))
Write-Host 'RenewNewToken:' ($r3 | ConvertTo-Json -Compress)

$client.Close()
