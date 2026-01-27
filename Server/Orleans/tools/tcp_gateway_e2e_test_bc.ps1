$ErrorActionPreference = 'Stop'

# This script focuses on B/C extensions: RenewSession/Logout/CloseRoom.

function Write-U32([byte[]]$buf, [int]$off, [UInt32]$v) { [BitConverter]::GetBytes($v).CopyTo($buf, $off) }
function Write-U16([byte[]]$buf, [int]$off, [UInt16]$v) { [BitConverter]::GetBytes($v).CopyTo($buf, $off) }
function Read-Exact([System.IO.Stream]$s, [int]$n) { $b = New-Object byte[] $n; $o = 0; while ($o -lt $n) { $r = $s.Read($b, $o, $n - $o); if ($r -le 0) { throw 'EOF' }; $o += $r }; return $b }
function Send-Frame([System.IO.Stream]$s, [UInt16]$flags, [UInt32]$op, [UInt32]$seq, [byte[]]$payload) { if ($null -eq $payload) { $payload = @() }; $hdrSize = 16; $frameLen = [UInt32]($hdrSize + $payload.Length); $total = 4 + $frameLen; $buf = New-Object byte[] $total; Write-U32 $buf 0 $frameLen; Write-U16 $buf 4 $flags; Write-U16 $buf 6 ([UInt16]$hdrSize); Write-U32 $buf 8 $op; Write-U32 $buf 12 $seq; Write-U32 $buf 16 ([UInt32]$payload.Length); if ($payload.Length -gt 0) { $payload.CopyTo($buf, 20) }; $s.Write($buf, 0, $buf.Length); $s.Flush(); $lenBytes = Read-Exact $s 4; $respFrameLen = [BitConverter]::ToUInt32($lenBytes, 0); $resp = Read-Exact $s ([int]$respFrameLen); $out = New-Object byte[] (4 + $respFrameLen); $lenBytes.CopyTo($out, 0); $resp.CopyTo($out, 4); return $out }
function Parse-Response([byte[]]$frame) { $flags = [BitConverter]::ToUInt16($frame, 4); $op = [BitConverter]::ToUInt32($frame, 8); $seq = [BitConverter]::ToUInt32($frame, 12); $payloadLen = [BitConverter]::ToUInt32($frame, 16); $payload = if ($payloadLen -gt 0) { $frame[20..(19 + $payloadLen)] } else { @() }; $status = if ($payloadLen -ge 4) { [BitConverter]::ToInt32($payload, 0) } else { $null }; $biz = if ($payloadLen -gt 4) { [System.Text.Encoding]::UTF8.GetString($payload, 4, $payloadLen - 4) } else { '' }; return [PSCustomObject]@{ Flags = $flags; OpCode = $op; Seq = $seq; Status = $status; BizJson = $biz } }

$client = New-Object System.Net.Sockets.TcpClient('127.0.0.1', 4000)
$stream = $client.GetStream()
$seq = 100
$REQ = 8
$enc = [System.Text.Encoding]::UTF8

# GuestLogin
$respFrame = Send-Frame $stream $REQ 100 $seq ($enc.GetBytes('{}'))
$r = Parse-Response $respFrame
Write-Host 'GuestLogin:' ($r | ConvertTo-Json -Compress)
$login = $r.BizJson | ConvertFrom-Json
$seq++

# RenewSession (op=120)
$renewObj = @{ sessionToken = $login.sessionToken; extendSeconds = 120 }
$respFrame = Send-Frame $stream $REQ 120 $seq ($enc.GetBytes(($renewObj | ConvertTo-Json -Compress)))
$r = Parse-Response $respFrame
Write-Host 'RenewSession:' ($r | ConvertTo-Json -Compress)
$seq++

# CreateRoom
$createObj = @{ sessionToken = $login.sessionToken; region = 'cn'; serverId = 's1'; roomType = 'test'; title = 'room_close'; isPublic = $true; maxPlayers = 4; tags = @{ } }
$respFrame = Send-Frame $stream $REQ 110 $seq ($enc.GetBytes(($createObj | ConvertTo-Json -Compress)))
$r = Parse-Response $respFrame
Write-Host 'CreateRoom:' ($r | ConvertTo-Json -Compress)
$created = $r.BizJson | ConvertFrom-Json
$roomId = $created.roomId
$seq++

# CloseRoom (op=114)
$closeObj = @{ sessionToken = $login.sessionToken; roomId = $roomId }
$respFrame = Send-Frame $stream $REQ 114 $seq ($enc.GetBytes(($closeObj | ConvertTo-Json -Compress)))
$r = Parse-Response $respFrame
Write-Host 'CloseRoom:' ($r | ConvertTo-Json -Compress)
$seq++

# ListRooms: should not include closed room
$listObj = @{ sessionToken = $login.sessionToken; region = 'cn'; serverId = 's1'; offset = 0; limit = 10; roomType = $null }
$respFrame = Send-Frame $stream $REQ 113 $seq ($enc.GetBytes(($listObj | ConvertTo-Json -Compress)))
$r = Parse-Response $respFrame
Write-Host 'ListRoomsAfterClose:' ($r | ConvertTo-Json -Compress)
$seq++

# Logout (op=121)
$logoutObj = @{ sessionToken = $login.sessionToken }
$respFrame = Send-Frame $stream $REQ 121 $seq ($enc.GetBytes(($logoutObj | ConvertTo-Json -Compress)))
$r = Parse-Response $respFrame
Write-Host 'Logout:' ($r | ConvertTo-Json -Compress)
$seq++

# Validate old token by calling Renew again (should return invalid)
$respFrame = Send-Frame $stream $REQ 120 $seq ($enc.GetBytes(($renewObj | ConvertTo-Json -Compress)))
$r = Parse-Response $respFrame
Write-Host 'RenewAfterLogout:' ($r | ConvertTo-Json -Compress)

$client.Close()
