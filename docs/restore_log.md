# Протокол восстановления защищённых файлов

Дата: 2026-11-16 (этап 3, ПРОМТ 0.5)

## Обнаруженные проблемы при инвентаризации
Все 20 защищённых файлов присутствуют в рабочем дереве и совпадают по SHA-256
с tools/baseline/protected_files.json. Однако проект tools/GuardRunner не собирался:
MSBuild reported "Source file ... could not be found" для Services/ScoringService.cs
и Services/Interfaces/IScoringService.cs, хотя файлы существуют на диске
(проверено ls/md5sum/stat). Причина — сбой разрешения относительных путей glob/Include
в кэше сборки GuardRunner (obj), а не потеря файлов.

## Выполненные действия (без изменения содержимого защищённых данных)
| Файл | Действие | Источник | Причина |
|---|---|---|---|
| Services/ScoringService.cs | хэш сверен, копия обновлена в backup | рабочий файл = git HEAD | обеспечение надёжного источника отката |
| Services/Interfaces/IScoringService.cs | восстановлен доступ для сборки (clean obj + явные Compile Include) | git HEAD (хэш совпадает с эталоном) | сборка GuardRunner падала из-за устаревшего кэша MSBuild |
| tools/baseline/backup/ScoringService.cs | обновлена резервная копия | рабочий файл (SHA совпадает с protected_files.json) | backup ранее не содержал этот файл |
| tools/baseline/backup/IScoringService.cs | добавлена резервная копия | рабочий файл | отсутствовала в backup |

Содержимое вопросов, вариантов, ключей, весов, формул и логики подсчёта НЕ изменялось.
Никаких самодельных замен не создавалось.
