using System.IO;

namespace ZharikovApp.Helpers;

/// <summary>
/// Вспомогательные методы для работы с путями приложения.
/// </summary>
public static class PathHelper
{
    /// <summary>Корневая папка запуска приложения.</summary>
    public static string AppRoot => AppContext.BaseDirectory;

    /// <summary>
    /// Папка с внешними данными (Data/, docs/). При single-file публикации
    /// эти файлы не извлекаются рядом с exe, поэтому используется папка
    /// %APPDATA%/ZharikovApp/Data. При первом запуске данные копируются туда
    /// из выходной папки сборки (dev-режим / обычная публикация без SingleFile).
    /// </summary>
    public static string ExternalDataRoot
    {
        get
        {
            var dir = Path.Combine(UserDataDirectory, "Data");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>Папка с JSON-файлами блоков методик.</summary>
    public static string BlocksDirectory => Path.Combine(ExternalDataRoot, "Blocks");

    /// <summary>Путь к файлу источника методик.</summary>
    public static string MethodicsSourcePath => Path.Combine(ExternalDataRoot, "methodics_source.md");

    /// <summary>
    /// Обеспечивает наличие внешних данных в %APPDATA%/ZharikovApp/Data:
    /// при первом запуске копирует Data/Blocks и docs из каталога приложения,
    /// если они там существуют (в single-file exe их нет — молча пропускает).
    /// </summary>
    public static void EnsureExternalData()
    {
        try
        {
            // Блоки методик
            var srcBlocks = Path.Combine(AppRoot, "Data", "Blocks");
            if (Directory.Exists(srcBlocks))
            {
                Directory.CreateDirectory(BlocksDirectory);
                foreach (var file in Directory.GetFiles(srcBlocks, "*.json"))
                {
                    var target = Path.Combine(BlocksDirectory, Path.GetFileName(file));
                    if (!File.Exists(target) ||
                        File.GetLastWriteTimeUtc(file) > File.GetLastWriteTimeUtc(target))
                    {
                        File.Copy(file, target, overwrite: true);
                    }
                }
            }

            // Источник методик
            var srcDocs = Path.Combine(AppRoot, "docs", "methodics_source.md");
            var dstDocs = MethodicsSourcePath;
            if (File.Exists(srcDocs) &&
                (!File.Exists(dstDocs) ||
                 File.GetLastWriteTimeUtc(srcDocs) > File.GetLastWriteTimeUtc(dstDocs)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dstDocs)!);
                File.Copy(srcDocs, dstDocs, overwrite: true);
            }
        }
        catch
        {
            // Повреждение/недоступность копирования не должно ронять приложение:
            // сервисы загрузки сами сообщат об отсутствии данных.
        }
    }

    /// <summary>Папка пользовательских данных (%APPDATA%/ZharikovApp).</summary>
    public static string UserDataDirectory
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZharikovApp");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>Файл автосохранения сессии пользователя.</summary>
    public static string SessionFilePath => Path.Combine(UserDataDirectory, "session.json");
}
