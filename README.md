# ZharikovApp

Windows-приложение (каркас) для диагностики по методикам Жарикова и Крушельницкого.

## Стек

- C# / .NET 8 (`net8.0-windows`)
- WPF (`UseWPF`)
- MVVM (CommunityToolkit.Mvvm 8.3.2)
- System.Text.Json — для данных блоков в `Data/Blocks/`

Проект собирается и запускается **только на Windows** (WPF не поддерживается на Linux/macOS).

## Структура проекта

```
ZharikovApp/                  (корень репозитория — сам проект)
├── ZharikovApp.csproj        файл проекта
├── app.manifest              манифест (Windows 10/11, PerMonitorV2 DPI)
├── App.xaml / App.xaml.cs    точка входа
├── docs/
│   └── methodics_source.md   ЕДИНСТВЕННЫЙ источник полных методик (12 блоков)
├── Data/
│   └── Blocks/               JSON-файлы блоков (по одному на блок), копируются в выходную папку
├── Assets/
│   └── Images/               изображения (фигуры Пальмера, образцы почерка и подписи),
│                             подключаются как WPF Resource
├── Models/                   модели (BlockBase — базовая модель блока)
├── Services/                 сервисы; Interfaces/ — контракты (IBlockLoaderService)
├── ViewModels/               MainViewModel (CommunityToolkit.Mvvm)
├── Views/                    MainWindow (XAML + code-behind)
├── Helpers/                  PathHelper (пути к Data/Blocks и docs)
└── Resources/                ресурсы стилей/словарей (заполняется позже)
```

## Сборка

Требуется .NET 8 SDK на Windows:

```powershell
dotnet build
```

Сборка проходит без ошибок и предупреждений; данные из `docs/` и `Data/Blocks/`
копируются в выходную папку (`CopyToOutputDirectory=PreserveNewest`).

## Запуск

```powershell
dotnet run
```

или после сборки:

```powershell
.\bin\Debug\net8.0-windows\ZharikovApp.exe
```

Открывается главное окно каркаса. Логика прохождения методик будет добавлена следующим этапом.

## Где лежит источник методик

Полные тексты всех 12 блоков (вопросы, ключи, подсчёт, интерпретация) находятся в файле:

```
docs/methodics_source.md
```

Это единственный источник; сокращать, пересказывать или заменять вопросы запрещено.
JSON-представления блоков для приложения будут храниться в `Data/Blocks/`.

## Где будут лежать изображения

Изображения размещаются в папке:

```
Assets/Images/
```

Планируемые файлы (блоки 6, 8, 9):

- `palmer_figure_1.png` — остроконечное тело (пирамида/конус);
- `palmer_figure_2.png` — шар;
- `palmer_figure_3.png` — гранёная фигура (ромб/алмаз);
- `handwriting_sample.png` — образец почерка;
- `signature_sample.png` — образец подписи.

Файлы `.png`/`.jpg` из этой папки автоматически включаются в сборку как
WPF `Resource` и доступны из XAML, например:
`pack://application:,,,/Assets/Images/palmer_figure_1.png`.

## Текущее состояние (каркас)

Реализовано: структура проекта, конфигурация сборки, главное окно, MainViewModel,
базовая модель блока, интерфейс загрузчика блоков, вспомогательные пути.
Пока **не** реализовано: загрузка JSON-блоков, прохождение тестов, подсчёт и интерпретация.
