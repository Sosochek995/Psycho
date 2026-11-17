# ============================================================================
# guard_check.ps1 — автоматическая проверка защиты методик ZharikovApp.
# Запуск:  powershell -ExecutionPolicy Bypass -File tools/guard_check.ps1
# Код возврата 0 = ЗАЩИТА В ПОРЯДКЕ, ненулевой = НАРУШЕНИЕ ЗАЩИТЫ.
# ============================================================================
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$fail = New-Object System.Collections.Generic.List[string]
$warn = New-Object System.Collections.Generic.List[string]

Write-Host "== 1. Проверка SHA-256 защищённых файлов =="
$pf = Get-Content 'tools/baseline/protected_files.json' -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($item in $pf.files) {
    if (-not (Test-Path $item.path)) { $fail.Add("ОТСУТСТВУЕТ: $($item.path)"); continue }
    $h = (Get-FileHash -Algorithm SHA256 $item.path).Hash.ToLower()
    if ($h -ne $item.sha256.ToLower()) { $fail.Add("ХЭШ НЕ СОВПАДАЕТ: $($item.path)") }
}
if ($fail.Count -eq 0) { Write-Host "  OK: $($pf.files.Count) файлов совпадают" }

Write-Host "== 2. Смысловой слепок методик =="
python3 tools/guard_snapshot.py tools/baseline/_current_snapshot.json
if ($LASTEXITCODE -ne 0) { $fail.Add("Не удалось снять слепок") }
$snapOk = python3 -c @'
import json,sys
a=json.load(open('tools/baseline/_current_snapshot.json',encoding='utf-8'))
b=json.load(open('tools/baseline/methodology_baseline.json',encoding='utf-8'))
ka=a['blocks']; kb=b['blocks'] if isinstance(b,dict) and 'blocks' in b else b
if isinstance(kb,dict): kb=[kb[k] for k in sorted(kb,key=lambda x:kb[x]['order'])]
diff=[x['id'] for x,y in zip(ka,kb) if json.dumps(x,sort_keys=True,ensure_ascii=False)!=json.dumps(y,sort_keys=True,ensure_ascii=False)]
print('SNAPSHOT_OK' if not diff and len(ka)==len(kb) else 'SNAPSHOT_DIFF:'+','.join(diff))
sys.exit(0 if not diff and len(ka)==len(kb) else 1)
'@
Write-Host "  $snapOk"
if ($snapOk -notlike 'SNAPSHOT_OK') { $fail.Add("Расхождение смыслового слепка: $snapOk") }

Write-Host "== 3. Количество блоков и вопросов =="
$counts = python3 -c @'
import json,os
exp={'block_01_leadership':50,'block_02_love':20,'block_03_artist_thinker':10,'block_04_extraversion':9,'block_05_biomarkers':4,'block_06_palmer':6,'block_07_leader_admin':20,'block_08_graphology':9,'block_09_signature':8,'block_10_left_hand':10,'block_11_creative_age':10,'block_12_creative_potential':50}
files=[f for f in os.listdir('Data/Blocks') if f.endswith('.json')]
print(len(files))
bad=[]
for f in sorted(files):
    d=json.load(open('Data/Blocks/'+f,encoding='utf-8')); n=len(d['questions'])
    e=exp.get(d['id'])
    if e is None or n!=e: bad.append(f"{d['id']}: {n} != {e}")
print('COUNTS_OK' if not bad else 'COUNTS_BAD:'+';'.join(bad))
'@
$blockCount = ($counts | Select-Object -First 1)
$countsStatus = ($counts | Select-Object -Skip 1)
Write-Host "  блоков: $blockCount"; Write-Host "  $countsStatus"
if ($blockCount -ne 12) { $fail.Add("Блоков не 12: $blockCount") }
if ($countsStatus -notlike 'COUNTS_OK') { $fail.Add("Количество вопросов не соответствует эталону: $countsStatus") }

Write-Host "== 4. Контрольные векторы через код приложения =="
dotnet build tools/GuardRunner -v q --nologo | Out-Null
$runner = Join-Path $root 'tools/GuardRunner/bin/Debug/net8.0/GuardVectorRunner.dll'
if (-not (Test-Path $runner)) { $fail.Add("GuardRunner не собран — прогон векторов невозможен") }
else {
    dotnet $runner Data/Blocks tools/baseline/control_vectors.json tools/baseline/_actual_vectors.json
    if ($LASTEXITCODE -ne 0) { $fail.Add("GuardRunner завершился с ошибкой") }
    else {
        $cmp = python3 tools/guard_compare.py tools/baseline/control_vectors.json tools/baseline/_actual_vectors.json
        Write-Host $cmp
        $j = $cmp | ConvertFrom-Json
        if ($j.failed -gt 0) { foreach ($m in $j.mismatches) { $fail.Add("ВЕКТОР: $m") } }
        $script:vecPassed = $j.passed; $script:vecFailed = $j.failed
    }
}

Write-Host "== 5. Отчёт =="
$status = if ($fail.Count -eq 0) { 'ЗАЩИТА В ПОРЯДКЕ' } else { 'НАРУШЕНИЕ ЗАЩИТЫ' }
$report = @(
"# Отчёт проверки защиты методик", "",
"- Дата: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')",
"- Статус хэшей: $(if (($fail | Where-Object { $_ -like '*ХЭШ*' -or $_ -like '*ОТСУТСТВУЕТ*' }).Count -eq 0) {'OK'} else {'НАРУШЕНО'})",
"- Статус смыслового слепка: $(if ($snapOk -like 'SNAPSHOT_OK') {'OK'} else {'НАРУШЕН'})",
"- Количество блоков: $blockCount",
"- Количество вопросов по блокам: $countsStatus",
"- Контрольных векторов: всего $((($script:vecPassed + $script:vecFailed))) , прошло: $($script:vecPassed), провалилось: $($script:vecFailed)",
"", "## Расхождения", $(if ($fail.Count) { ($fail | ForEach-Object { "- $_" }) -join "`n" } else { "- нет" }), "",
"## Итог: **$status**")
Set-Content docs/guard_report.md ($report -join "`n") -Encoding UTF8
Write-Host $status
if ($fail.Count -gt 0) { $fail | ForEach-Object { Write-Host "  - $_" }; exit 1 }
exit 0
