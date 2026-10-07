# 14 -> 18 mods on staging (operator, 2026-10-07; Tot ! Enhanced Sudo dropped after the 19-mod run): verified backup, read-only MQ01 persistence check on the backup
# copy, import the 5 new Enhanced mods through the harness Local pipeline (appended after MQ14), 19-mod validation boot.
$ErrorActionPreference = 'Stop'
$env:CSC_LIVE_TESTS = '1'; $env:CSC_LIVE_ROOT = 'E:\CSC-M3-Live'
$env:CSC_SERVER_DIR = 'D:\steamnew\steamapps\common\Conan Exiles Dedicated Server'
$env:CSC_CLIENT_ROOT = 'D:\steamnew\steamapps\common\Conan Exiles'
$exe = 'E:\github\gameee\tests\ConanServerControl.LiveHarness\bin\Release\net8.0\ConanServerControl.LiveHarness.exe'
$mods = "$env:CSC_SERVER_DIR\ConanSandbox\Mods"
$out = 'E:\CSC-M3-Live\live-test\install-18'
New-Item -ItemType Directory -Force $out | Out-Null
function Say($m) { $l = "[$(Get-Date -Format HH:mm:ss)] $m"; Write-Host $l; Add-Content "$out\install.log" $l }
function Offline { if (@(Get-Process | ? { $_.ProcessName -match 'ConanSandbox|LiveHarness' }).Count) { throw 'Conan/harness process running' } }
function Run($step, [string[]]$a) { Say "RUN $step : $($a -join ' ')"; & $exe @a *> "$out\$step.txt"; $c = $LASTEXITCODE; Say "$step exit=$c"; return $c }

Offline
if (((Get-Content "$mods\modlist.txt" | ? { $_.Trim() }).Count) -ne 14) { throw 'expected the 14-mod pack' }
if ((Run '01-backup' @('cold-backup')) -ne 0) { throw 'backup failed' }
$bid = ((Select-String "$out\01-backup.txt" -Pattern 'BackupId:\s*(\S+)' | Select -First 1).Matches[0].Groups[1].Value)
$meta = Get-Content "E:\CSC-M3-Live\app-data\backups\$bid\metadata.json" -Raw | ConvertFrom-Json
if (-not ($meta.Succeeded -and $meta.HashesVerified -and $meta.SqliteVerified -and $meta.ManifestWritten)) { throw 'backup not verified' }
Say "PRE-INSTALL BACKUP VERIFIED: $bid"

# read-only: MQ01 progress persisted in the world (copy of the backup DB)
$db = "E:\CSC-M3-Live\app-data\backups\$bid\world\game_0.db"; $copy = "$out\game_copy.db"; Copy-Item $db $copy -Force
$py = @"
import sqlite3
c = sqlite3.connect('file:' + r'$copy' + '?mode=ro', uri=True)
rows = c.execute("select p.name, length(p.value), hex(p.value) from properties p where p.name like 'BP_MQ14MainQuestController_C.%'").fetchall()
for n, l, h in rows: print(n, l, bytes.fromhex(h).decode('utf-16le', 'ignore').replace('\x00', '') if 'Ids' in n else '')
print('mod_controllers rows for MQ14:', c.execute("select count(*) from properties where name like 'BP_MQ14MainQuestController_C.%'").fetchone()[0])
"@
$pyf = "$out\mq01_check.py"; Set-Content $pyf $py -Encoding utf8; $res = & python $pyf 2>&1; $res | % { Say "DB: $_" }; Remove-Item $pyf
Remove-Item $copy -Force

$src = [ordered]@{
  'Better_Thrall_ICONS'      = 'E:\CSC-M3-Live\mod-sources\Better_Thrall_ICONS\Better_Thrall_ICONS.pak'
  'Better_Tavern_PATRONS_E'  = 'E:\CSC-M3-Live\mod-sources\Better_Tavern_PATRONS_E\Better_Tavern_PATRONS_E.pak'
  'ModControlPanel'          = 'E:\CSC-M3-Live\mod-sources\ModControlPanel\ModControlPanel.pak'
  'IdeaPoet_EditAppearance'  = 'E:\CSC-M3-Live\mod-sources\IdeaPoet_EditAppearance\IdeaPoet_EditAppearance.pak'
}
$i = 2
foreach ($k in $src.Keys) { $i++; if ((Run ("{0:D2}-import-$k" -f $i) @('import-local', $src[$k])) -ne 0) { throw "import $k failed" } }
$list = Get-Content "$mods\modlist.txt" | ? { $_.Trim() }
Say "MODLIST ($($list.Count)): $($list -join ' | ')"
if ($list.Count -ne 18) { throw 'expected 18 mods after import' }

$b = Run '10-validation-boot' @('mod-boot', '--hold', '180', '--batch', 'cl378787-18mod-validation')
Offline
Say "VALIDATION boot exit=$b"
