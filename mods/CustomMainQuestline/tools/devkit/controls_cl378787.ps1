# Baseline controls on CL-378787 for the Exile_Priest_4_* spawn-table line. Fail-closed; the harness owns
# start/stop. Order: verified backup -> vanilla boot (empty modlist) -> restore -> 13-mod mod-boot (MQ14 removed)
# -> re-import MQ14 -> restore -> exact 14-mod state.
$ErrorActionPreference = 'Stop'
$env:CSC_LIVE_TESTS = '1'; $env:CSC_LIVE_ROOT = 'E:\CSC-M3-Live'
$env:CSC_SERVER_DIR = 'D:\steamnew\steamapps\common\Conan Exiles Dedicated Server'
$env:CSC_CLIENT_ROOT = 'D:\steamnew\steamapps\common\Conan Exiles'
$exe = 'E:\github\gameee\tests\ConanServerControl.LiveHarness\bin\Release\net8.0\ConanServerControl.LiveHarness.exe'
$mods = "$env:CSC_SERVER_DIR\ConanSandbox\Mods"
$log = "$env:CSC_SERVER_DIR\ConanSandbox\Saved\Logs\ConanSandbox.log"
$src = 'E:\CSC-M3-Live\mod-sources\MQ14MainQuest\MQ14MainQuest.pak'
$out = 'E:\CSC-M3-Live\live-test\update-378787\controls'
New-Item -ItemType Directory -Force $out | Out-Null
function Say($m) { $l = "[$(Get-Date -Format HH:mm:ss)] $m"; Write-Host $l; Add-Content "$out\controls.log" $l }
function Offline { if (@(Get-Process | ? { $_.ProcessName -match 'ConanSandbox|LiveHarness' }).Count) { throw 'Conan/harness process running' } }
function Run($step, [string[]]$a) { Say "RUN $step : $($a -join ' ')"; & $exe @a *> "$out\$step.txt"; $c = $LASTEXITCODE; Say "$step exit=$c"; return $c }
function Priest($file) { @(Select-String $file -Pattern 'could not find weighted table with id: (\S+)' | % { $_.Matches[0].Groups[1].Value }) -join ', ' }
$want = (Get-Content "$mods\modlist.txt" | ? { $_.Trim() }) -join '|'
if (($want -split '\|').Count -ne 14) { throw "expected 14 mods, found: $want" }

Offline
if ((Run '01-backup' @('cold-backup')) -ne 0) { throw 'backup failed' }
$bid = ((Select-String "$out\01-backup.txt" -Pattern 'BackupId:\s*(\S+)' | Select -First 1).Matches[0].Groups[1].Value)
$meta = Get-Content "E:\CSC-M3-Live\app-data\backups\$bid\metadata.json" -Raw | ConvertFrom-Json
if (-not ($meta.Succeeded -and $meta.HashesVerified -and $meta.SqliteVerified -and $meta.ManifestWritten)) { throw 'backup not verified' }
Say "CONTROL BACKUP VERIFIED: $bid"

# 1. vanilla
Set-Content "$mods\modlist.txt" '' -NoNewline
$v = Run '02-vanilla-boot' @('boot', '--hold', '120')
Offline
Copy-Item $log "$out\vanilla-ConanSandbox.log" -Force
Say "VANILLA boot exit=$v; weighted-table ids: $(Priest "$out\vanilla-ConanSandbox.log")"
if ((Run '03-restore' @('restore', $bid)) -ne 0) { throw 'restore after vanilla failed' }
if (((Get-Content "$mods\modlist.txt" | ? { $_.Trim() }) -join '|') -cne $want) { throw 'modlist not restored after vanilla' }

# 2. 13 mods (MQ14 removed)
if ((Run '04-remove-mq14' @('remove-local', 'MQ14MainQuest.pak')) -ne 0) { throw 'remove failed' }
$b = Run '05-13mod-boot' @('mod-boot', '--hold', '180', '--batch', 'cl378787-13mod-control')
Offline
Copy-Item $log "$out\13mod-ConanSandbox.log" -Force
Say "13-MOD boot exit=$b; weighted-table ids: $(Priest "$out\13mod-ConanSandbox.log")"
if ((Run '06-import-mq14' @('import-local', $src)) -ne 0) { throw 're-import failed' }
if ((Run '07-restore' @('restore', $bid)) -ne 0) { throw 'final restore failed' }
if (((Get-Content "$mods\modlist.txt" | ? { $_.Trim() }) -join '|') -cne $want) { throw 'final modlist differs' }
if ((Get-FileHash "$mods\MQ14MainQuest.pak").Hash -ne '585F52AAAE44ED0EC3C51DCDDA10F77BB83BCD079A97657357710770A0EE1C1D') { throw 'MQ14 pak hash differs' }
Say "DONE: exact 14-mod state restored from $bid"
