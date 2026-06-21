Set-Location 'C:\Users\mysho\Downloads\SODAGMTOOL'

$killed = 0
$procs = Get-Process -Name 'SQ_Email_Tools_TW' -ErrorAction SilentlyContinue
if ($procs) {
    foreach ($p in $procs) {
        Write-Host ("Killing PID {0} ({1})..." -f $p.Id, $p.ProcessName)
        $p | Stop-Process -Force
        $killed++
    }
} else {
    Write-Host '無 GMTool 進程在跑'
}
Start-Sleep -Seconds 2

$still = Get-Process -Name 'SQ_Email_Tools_TW' -ErrorAction SilentlyContinue
if ($still) {
    Write-Host '仍有 GMTool 進程未結束，再 kill 一次'
    foreach ($p in $still) {
        Write-Host ("Re-killing PID {0}" -f $p.Id)
        $p | Stop-Process -Force
        $killed++
    }
    Start-Sleep -Seconds 2
}

Write-Host ("=== KILLED_COUNT={0} ===" -f $killed)

$src = 'desktop\Project\bin\Release\net6.0-windows\win-x64'
$dst = 'desktop\GMTool'

try {
    Copy-Item -Force "$src\SQ_Email_Tools_TW.exe" "$dst\SQ_Email_Tools_TW.exe" -ErrorAction Stop
    Write-Host 'COPY_EXE: OK'
} catch {
    Write-Host ('COPY_EXE: FAIL - ' + $_.Exception.Message)
}

try {
    Copy-Item -Force "$src\SQ_Email_Tools_TW.dll" "$dst\SQ_Email_Tools_TW.dll" -ErrorAction Stop
    Write-Host 'COPY_DLL: OK'
} catch {
    Write-Host ('COPY_DLL: FAIL - ' + $_.Exception.Message)
}

if (Test-Path "$src\SQ_Email_Tools_TW.pdb") {
    try {
        Copy-Item -Force "$src\SQ_Email_Tools_TW.pdb" "$dst\SQ_Email_Tools_TW.pdb" -ErrorAction Stop
        Write-Host 'COPY_PDB: OK'
    } catch {
        Write-Host ('COPY_PDB: FAIL - ' + $_.Exception.Message)
    }
} else {
    Write-Host 'COPY_PDB: SOURCE_NOT_FOUND'
}

Write-Host ''
Write-Host '=== DEST (GMTool\) ==='
Get-ChildItem "$dst\SQ_Email_Tools_TW.exe", "$dst\SQ_Email_Tools_TW.dll" |
    ForEach-Object { '{0}  {1,-10}  {2}' -f $_.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'), $_.Length, $_.Name } |
    Write-Host

Write-Host ''
Write-Host '=== SRC (bin\Release) ==='
Get-ChildItem "$src\SQ_Email_Tools_TW.exe", "$src\SQ_Email_Tools_TW.dll" |
    ForEach-Object { '{0}  {1,-10}  {2}' -f $_.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'), $_.Length, $_.Name } |
    Write-Host
