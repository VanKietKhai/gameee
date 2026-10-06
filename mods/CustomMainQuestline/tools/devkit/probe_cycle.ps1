# One differential-probe cycle on staging (2.2.3 / CL-378132). Fail-closed; never kills the server itself
# (the harness owns stop/force-stop). Usage: probe_cycle.ps1 -Name MQ14ProbeA -Pak <path> -Batch mq14-probeA-1
param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][string]$Pak, [Parameter(Mandatory)][string]$Batch)
$ErrorActionPreference = 'Stop'
$env:CSC_LIVE_TESTS = '1'; $env:CSC_LIVE_ROOT = 'E:\CSC-M3-Live'
$env:CSC_SERVER_DIR = 'D:\steamnew\steamapps\common\Conan Exiles Dedicated Server'
$env:CSC_CLIENT_ROOT = 'D:\steamnew\steamapps\common\Conan Exiles'
$exe = 'E:\github\gameee\tests\ConanServerControl.LiveHarness\bin\Release\net8.0\ConanServerControl.LiveHarness.exe'
$mods = "$env:CSC_SERVER_DIR\ConanSandbox\Mods"
$bundle = 'E:\CSC-M3-Live\client-bundles\ConanClientModBundle-20261005-041519\Mods'
$out = "E:\CSC-M3-Live\live-test\mq14-probe\$Batch"
if (Test-Path $out) { throw "output $out exists" }
New-Item -ItemType Directory -Force $out | Out-Null
function Say($m) { $l = "[$(Get-Date -Format HH:mm:ss)] $m"; Write-Host $l; Add-Content "$out\cycle.log" $l }
function Offline { if (@(Get-Process | ? { $_.ProcessName -match 'ConanSandbox|LiveHarness' }).Count) { throw 'Conan/harness process running' } }
function Exact13 {
  $a = (Get-Content "$mods\modlist.txt" | ? { $_.Trim() }) -join '|'; $b = (Get-Content "$bundle\modlist.txt" | ? { $_.Trim() }) -join '|'
  if ($a -cne $b) { throw "modlist differs from the 13-mod baseline: $a" }
  foreach ($f in Get-ChildItem "$bundle\*.pak") { if ((Get-FileHash "$mods\$($f.Name)").Hash -ne (Get-FileHash $f.FullName).Hash) { throw "pak mismatch $($f.Name)" } }
  if (@(Get-ChildItem $mods | ? { $_.Name -ne 'modlist.txt' -and -not (Test-Path "$bundle\$($_.Name)") }).Count) { throw 'extra file in Mods' }
}
function Run($step, [string[]]$a) { Say "RUN $step : $($a -join ' ')"; & $exe @a *> "$out\$step.txt"; $c = $LASTEXITCODE; Say "$step exit=$c"; return $c }

Offline; if (Test-Path 'E:\CSC-M3-Live\live-test\release-hold') { throw 'release-hold present' }; Exact13
Say "PRECHECK PASS (offline, exact 13-mod baseline); probe $Name $((Get-FileHash $Pak).Hash)"
$src = "E:\CSC-M3-Live\mod-sources\$Name"; New-Item -ItemType Directory -Force $src | Out-Null; Copy-Item $Pak $src -Force
$srcPak = Join-Path $src (Split-Path $Pak -Leaf)

if ((Run '01-backup' @('cold-backup')) -ne 0) { throw 'backup failed' }
$bid = ((Select-String "$out\01-backup.txt" -Pattern 'BackupId:\s*(\S+)' | Select -First 1).Matches[0].Groups[1].Value)
$meta = Get-Content "E:\CSC-M3-Live\app-data\backups\$bid\metadata.json" -Raw | ConvertFrom-Json
if (-not ($meta.Succeeded -and $meta.HashesVerified -and $meta.SqliteVerified -and $meta.ManifestWritten)) { throw 'backup not verified' }
Say "PRE-PROBE BACKUP VERIFIED: $bid"

if ((Run '02-import' @('import-local', $srcPak)) -ne 0) { throw 'import failed' }
$boot = Run '03-boot' @('mod-boot', '--hold', '180', '--batch', $Batch)
Say "BOOT exit=$boot"
Offline
if ((Run '04-remove' @('remove-local', (Split-Path $Pak -Leaf))) -ne 0) { throw 'remove failed' }
if ((Run '05-restore' @('restore', $bid)) -ne 0) { throw 'restore failed' }
Exact13
Say "ROLLBACK DONE: probe removed, $bid restored, exact 13-mod baseline"
Say "RESULT boot_exit=$boot backup=$bid"
