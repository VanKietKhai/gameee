# Attribute the "Energy source heat map not loaded" startup error to one of the 4 new mods. Starts from the 18-mod
# install; each step removes or re-imports mods through the harness and runs a short mod-boot, then counts the line.
param([Parameter(Mandatory)][string[]]$Remove, [string[]]$Import = @(), [Parameter(Mandatory)][string]$Batch)
$ErrorActionPreference = 'Stop'
$env:CSC_LIVE_TESTS = '1'; $env:CSC_LIVE_ROOT = 'E:\CSC-M3-Live'
$env:CSC_SERVER_DIR = 'D:\steamnew\steamapps\common\Conan Exiles Dedicated Server'
$env:CSC_CLIENT_ROOT = 'D:\steamnew\steamapps\common\Conan Exiles'
$exe = 'E:\github\gameee\tests\ConanServerControl.LiveHarness\bin\Release\net8.0\ConanServerControl.LiveHarness.exe'
$mods = "$env:CSC_SERVER_DIR\ConanSandbox\Mods"
$out = "E:\CSC-M3-Live\live-test\bisect-heatmap\$Batch"
New-Item -ItemType Directory -Force $out | Out-Null
if (@(Get-Process | ? { $_.ProcessName -match 'ConanSandbox|LiveHarness' }).Count) { throw 'Conan/harness process running' }
foreach ($p in $Remove) { & $exe remove-local $p *> "$out\remove-$p.txt"; if ($LASTEXITCODE) { throw "remove $p failed" } }
foreach ($p in $Import) { & $exe import-local $p *> "$out\import-$(Split-Path $p -Leaf).txt"; if ($LASTEXITCODE) { throw "import $p failed" } }
$list = Get-Content "$mods\modlist.txt" | ? { $_.Trim() }
& $exe mod-boot --hold 60 --batch $Batch *> "$out\boot.txt"; $code = $LASTEXITCODE
$log = "E:\CSC-M3-Live\live-test\batch-snapshots\$Batch\boot.log"
$n = @(Select-String $log -Pattern 'Energy source heat map not loaded').Count
"RESULT $Batch mods=$($list.Count) exit=$code heatmapLines=$n modlist=$($list -join '|')" | Tee-Object -Append "$out\result.txt"
