$ErrorActionPreference = 'Stop'

function Write-U32([byte[]]$buf, [int]$off, [UInt32]$v) {
    [BitConverter]::GetBytes($v).CopyTo($buf, $off)
}

function Write-U16([byte[]]$buf, [int]$off, [UInt16]$v) {
    [BitConverter]::GetBytes($v).CopyTo($buf, $off)
}

function Read-Exact([System.IO.Stream]$s, [int]$n) {
    $b = New-Object byte[] $n
    $o = 0
    while ($o -lt $n) {
        $r = $s.Read($b, $o, $n - $o)
        if ($r -le 0) { throw 'EOF' }
        $o += $r
    }
    return $b
}

function Send-Frame([System.IO.Stream]$s, [UInt16]$flags, [UInt32]$op, [UInt32]$seq, [byte[]]$payload) {
    if ($null -eq $payload) { $payload = @() }

    $hdrSize = 16
    $frameLen = [UInt32]($hdrSize + $payload.Length)
    $total = 4 + $frameLen

    $buf = New-Object byte[] $total

    # Length = header+payload (without the 4 bytes length field)
    Write-U32 $buf 0 $frameLen

    # Header
    Write-U16 $buf 4 $flags
    Write-U16 $buf 6 ([UInt16]$hdrSize)
    Write-U32 $buf 8 $op
    Write-U32 $buf 12 $seq
    Write-U32 $buf 16 ([UInt32]$payload.Length)

    # Payload
    if ($payload.Length -gt 0) { $payload.CopyTo($buf, 20) }

    $s.Write($buf, 0, $buf.Length)
    $s.Flush()

    $lenBytes = Read-Exact $s 4
    $respFrameLen = [BitConverter]::ToUInt32($lenBytes, 0)
    $resp = Read-Exact $s ([int]$respFrameLen)

    $out = New-Object byte[] (4 + $respFrameLen)
    $lenBytes.CopyTo($out, 0)
    $resp.CopyTo($out, 4)
    return $out
}

function Parse-Response([byte[]]$frame) {
    $flags = [BitConverter]::ToUInt16($frame, 4)
    $op = [BitConverter]::ToUInt32($frame, 8)
    $seq = [BitConverter]::ToUInt32($frame, 12)
    $payloadLen = [BitConverter]::ToUInt32($frame, 16)

    $payload = if ($payloadLen -gt 0) { $frame[20..(19 + $payloadLen)] } else { @() }

    $status = if ($payloadLen -ge 4) { [BitConverter]::ToInt32($payload, 0) } else { $null }
    $biz = if ($payloadLen -gt 4) { [System.Text.Encoding]::UTF8.GetString($payload, 4, $payloadLen - 4) } else { '' }

    return [PSCustomObject]@{ Flags = $flags; OpCode = $op; Seq = $seq; Status = $status; BizJson = $biz }
}

$client = New-Object System.Net.Sockets.TcpClient('127.0.0.1', 4000)
$stream = $client.GetStream()
$seq = 1
$REQ = 8 # NetworkPacketFlags.Request
$enc = [System.Text.Encoding]::UTF8

# 1) GuestLogin (op=100)
$respFrame = Send-Frame $stream $REQ 100 $seq ($enc.GetBytes('{}'))
$r = Parse-Response $respFrame
Write-Host 'GuestLogin:' ($r | ConvertTo-Json -Compress)
$login = $r.BizJson | ConvertFrom-Json
$seq++

# 2) CreateRoom (op=110)
$createObj = @{ sessionToken = $login.sessionToken; region = 'cn'; serverId = 's1'; roomType = 'test'; title = 'room1'; isPublic = $true; maxPlayers = 4; tags = @{ mode = 'quick' } }
$respFrame = Send-Frame $stream $REQ 110 $seq ($enc.GetBytes(($createObj | ConvertTo-Json -Compress)))
$r = Parse-Response $respFrame
Write-Host 'CreateRoom:' ($r | ConvertTo-Json -Compress)
$created = $r.BizJson | ConvertFrom-Json
$roomId = $created.roomId
$seq++

# 3) ListRooms (op=113)
$listObj = @{ sessionToken = $login.sessionToken; region = 'cn'; serverId = 's1'; offset = 0; limit = 10; roomType = $null }
$respFrame = Send-Frame $stream $REQ 113 $seq ($enc.GetBytes(($listObj | ConvertTo-Json -Compress)))
$r = Parse-Response $respFrame
Write-Host 'ListRooms:' ($r | ConvertTo-Json -Compress)
$seq++

# 4) JoinRoom (op=111)
$joinObj = @{ sessionToken = $login.sessionToken; region = 'cn'; serverId = 's1'; roomId = $roomId }
$respFrame = Send-Frame $stream $REQ 111 $seq ($enc.GetBytes(($joinObj | ConvertTo-Json -Compress)))
$r = Parse-Response $respFrame
Write-Host 'JoinRoom:' ($r | ConvertTo-Json -Compress)
$seq++

# 5) LeaveRoom (op=112)
$leaveObj = @{ sessionToken = $login.sessionToken; region = 'cn'; serverId = 's1'; roomId = $roomId }
$respFrame = Send-Frame $stream $REQ 112 $seq ($enc.GetBytes(($leaveObj | ConvertTo-Json -Compress)))
$r = Parse-Response $respFrame
Write-Host 'LeaveRoom:' ($r | ConvertTo-Json -Compress)

$client.Close()
