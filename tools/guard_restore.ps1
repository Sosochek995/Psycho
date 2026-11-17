# ============================================================================
# guard_restore.ps1 — откат защищённых файлов из tools/baseline/backup/.
# Восстанавливает ТОЛЬКО файлы из protected_files.json.
# Пользовательские результаты/сессии (Data/sessions, answers_draft и т.п.) НЕ трогает.
# Перед восстановлением создаёт emergency-копию текущего состояния.
# ============================================================================
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$ts = Get-Date -Format 'yyyyMMdd_HHmmss'
$emerg = "tools/baseline/emergency_backup_$ts"

Write-Host "== 0. Emergency-копия текущего состояния -> $emerg =="
New-Item -ItemType Directory -Force -Path "$emerg/Data/Blocks","$emerg/Services/Interfaces","$emerg/Models","$emerg/docs" | Out-Null
Copy-Item docs/methodics_source.md "$emerg/docs/" -Force
Copy-Item Data/Blocks/*.json "$emerg/Data/Blocks/" -Force
if (Test-Path Services/ScoringService.cs) { Copy-Item Services/ScoringService.cs "$emerg/Services/" -Force }
if (Test-Path Services/Interfaces/IScoringService.cs) { Copy-Item Services/Interfaces/IScoringService.cs "$emerg/Services/Interfaces/" -Force }
foreach ($m in 'ScoringConfig.cs','Enums.cs','Question.cs','AnswerOption.cs','ScaleConfig.cs','InterpretationRule.cs') {
    if (Test-Path "Models/$m") { Copy-Item "Models/$m" "$emerg/Models/" -Force }
}

Write-Host "== 1. Восстановление по protected_files.json =="
$pf = Get-Content 'tools/baseline/protected_files.json' -Raw -Encoding UTF8 | ConvertFrom-Json
$restored = @(); $missingSrc = @()
foreach ($item in $pf.files) {
    $name = Split-Path $item.path -Leaf
    $src = Join-Path 'tools/baseline/backup' $name
    if (-not (Test-Path $src)) { $missingSrc += $item.path; continue }
    $h = (Get-FileHash -Algorithm SHA256 $src).Hash.ToLower()
    if ($h -ne $item.sha256.ToLower()) { Write-Warning "Резервная копия повреждена (хэш не совпадает): $src — пропуск"; $missingSrc += $item.path; continue }
    New-Item -ItemType Directory -Force -Path (Split-Path $item.path) | Out-Null
    Copy-Item $src $item.path -Force
    $restored += $item.path
}
$log = @("# Протокол восстановления $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')", "",
         "Emergency-копия: $emerg", "", "## Восстановлено:") + ($restored | ForEach-Object { "- $_" })
if ($missingSrc.Count) { $log += @("", "## НЕ ВОССТАНОВЛЕНО (нет корректной копии в backup):") + ($missingSrc | ForEach-Object { "- $_" }) }
Add-Content docs/restore_log.md (($log -join "`n") + "`n`n") -Encoding UTF8
Write-Host ("Восстановлено файлов: " + $restored.Count)
$restored | ForEach-Object { Write-Host "  - $_" }
if ($missingSrc.Count) { Write-Host "НЕВОЗМОЖНО восстановить:"; $missingSrc | ForEach-Object { Write-Host "  - $_" }; exit 2 }
exit 0
