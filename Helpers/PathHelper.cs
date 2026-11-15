using System.IO;

namespace ZharikovApp.Helpers;

/// <summary>
/// Вспомогательные методы для работы с путями приложения.
/// </summary>
public static class PathHelper
{
    /// <summary>Корневая папка запуска приложения.</summary>
    public static string AppRoot => AppContext.BaseDirectory;

    /// <summary>Папка с JSON-файлами блоков методик.</summary>
    public static string BlocksDirectory => Path.Combine(AppRoot, "Data", "Blocks");

    /// <summary>Путь к файлу источника методик.</summary>
    public static string MethodicsSourcePath => Path.Combine(AppRoot, "docs", "methodics_source.md");

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
