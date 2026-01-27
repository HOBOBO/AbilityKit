$ErrorActionPreference = 'Stop'

function Write-U32([byte[]]$buf, [int]$off, [UInt32]$v) { [BitConverter]::GetBytes($v).CopyTo($buf, $off) }
function Write-U16([byte[]]$buf, [int]$off, [UInt16]$v) { [BitConverter]::GetBytes($v).CopyTo($buf, $off) }
function Read-Exact([System.IO.Stream]$s, [int]$n) { $b = New-Object byte[] $n; $o = 0; while ($o -lt $n) { $r = $s.Read($b, $o, $n - $o); if ($r -le 0) { throw 'EOF' }; $o += $r }; return $b }
function Try-ReadExact([System.IO.Stream]$s, [int]$n, [int]$timeoutMs) {
  $deadline = [DateTime]::UtcNow.AddMilliseconds($timeoutMs)
  $b = New-Object byte[] $n
  $o = 0
  while ($o -lt $n) {
    if ([DateTime]::UtcNow -gt $deadline) { return $null }
    if ($s.DataAvailable) {
      $r = $s.Read($b, $o, $n - $o)
      if ($r -le 0) { return $null }
      $o += $r
    } else {
      Start-Sleep -Milliseconds 10
    }
  }
  return $b
}
function Send-Frame([System.IO.Stream]$s, [UInt16]$flags, [UInt32]$op, [UInt32]$seq, [byte[]]$payload) { if ($null -eq $payload) { $payload = @() }; $hdrSize = 16; $frameLen = [UInt32]($hdrSize + $payload.Length); $total = 4 + $frameLen; $buf = New-Object byte[] $total; Write-U32 $buf 0 $frameLen; Write-U16 $buf 4 $flags; Write-U16 $buf 6 ([UInt16]$hdrSize); Write-U32 $buf 8 $op; Write-U32 $buf 12 $seq; Write-U32 $buf 16 ([UInt32]$payload.Length); if ($payload.Length -gt 0) { $payload.CopyTo($buf, 20) }; $s.Write($buf, 0, $buf.Length); $s.Flush(); $lenBytes = Read-Exact $s 4; $respFrameLen = [BitConverter]::ToUInt32($lenBytes, 0); $resp = Read-Exact $s ([int]$respFrameLen); $out = New-Object byte[] (4 + $respFrameLen); $lenBytes.CopyTo($out, 0); $resp.CopyTo($out, 4); return $out }
function Parse-Response([byte[]]$frame) { $flags = [BitConverter]::ToUInt16($frame, 4); $op = [BitConverter]::ToUInt32($frame, 8); $seq = [BitConverter]::ToUInt32($frame, 12); $payloadLen = [BitConverter]::ToUInt32($frame, 16); $payload = if ($payloadLen -gt 0) { $frame[20..(19 + $payloadLen)] } else { @() }; $status = if ($payloadLen -ge 4) { [BitConverter]::ToInt32($payload, 0) } else { $null }; $biz = if ($payloadLen -gt 4) { [System.Text.Encoding]::UTF8.GetString($payload, 4, $payloadLen - 4) } else { '' }; return [PSCustomObject]@{ Flags = $flags; OpCode = $op; Seq = $seq; Status = $status; BizJson = $biz } }
function Read-Frame([System.Net.Sockets.NetworkStream]$stream, [int]$timeoutMs) {
  $lenBytes = Try-ReadExact $stream 4 $timeoutMs
  if ($null -eq $lenBytes) { return $null }
  $frameLen = [BitConverter]::ToUInt32($lenBytes, 0)
  $rest = Try-ReadExact $stream ([int]$frameLen) $timeoutMs
  if ($null -eq $rest) { return $null }
  $out = New-Object byte[] (4 + $frameLen)
  $lenBytes.CopyTo($out, 0)
  $rest.CopyTo($out, 4)
  return $out
}
function Parse-Push([byte[]]$frame) {
  $flags = [BitConverter]::ToUInt16($frame, 4)
  $op = [BitConverter]::ToUInt32($frame, 8)
  $seq = [BitConverter]::ToUInt32($frame, 12)
  $payloadLen = [BitConverter]::ToUInt32($frame, 16)
  $payload = if ($payloadLen -gt 0) { $frame[20..(19 + $payloadLen)] } else { @() }
  $biz = if ($payloadLen -gt 0) { [System.Text.Encoding]::UTF8.GetString($payload, 0, $payloadLen) } else { '' }
  return [PSCustomObject]@{ Flags = $flags; OpCode = $op; Seq = $seq; PayloadJson = $biz }
}

$REQ = 8
$enc = [System.Text.Encoding]::UTF8
$accountId = 'user_kick_push_1'

# connection A
$c1 = New-Object System.Net.Sockets.TcpClient('127.0.0.1', 4000)
$s1 = $c1.GetStream()

# connection B
$c2 = New-Object System.Net.Sockets.TcpClient('127.0.0.1', 4000)
$s2 = $c2.GetStream()

$seq = 500

# Create session on A (no kick)
$reqA = @{ accountId = $accountId; expireSeconds = 600; kickExisting = $false }
$rA = Parse-Response (Send-Frame $s1 $REQ 122 $seq ($enc.GetBytes(($reqA | ConvertTo-Json -Compress))))
Write-Host 'ConnA CreateSession:' ($rA | ConvertTo-Json -Compress)
$tokA = ($rA.BizJson | ConvertFrom-Json).sessionToken
$seq++

# Create session on B with kickExisting=true -> should kick A
$reqB = @{ accountId = $accountId; expireSeconds = 600; kickExisting = $true }
$rB = Parse-Response (Send-Frame $s2 $REQ 122 $seq ($enc.GetBytes(($reqB | ConvertTo-Json -Compress))))
Write-Host 'ConnB CreateSession(kick):' ($rB | ConvertTo-Json -Compress)
$seq++

# Wait for server push on A (KickPushOpCode=9000, Flags includes ServerPush)
$pushFrame = Read-Frame $s1 2000
if ($null -eq $pushFrame) { throw 'Expected kick server push on connection A, but none received' }
$p = Parse-Push $pushFrame
Write-Host 'ConnA Push:' ($p | ConvertTo-Json -Compress)

if ($p.OpCode -ne 9000) { throw ('Expected push opcode 9000, got ' + $p.OpCode) }

$c1.Close()
$c2.Close()
