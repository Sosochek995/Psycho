using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZharikovApp.Helpers;
using ZharikovApp.Models;

namespace ZharikovApp.Services;

/// <summary>
/// Сохранение/загрузка пользовательской сессии (<see cref="AppSession"/>)
/// в JSON-файл %APPDATA%/ZharikovApp/session.json. Автосохранение вызывается
/// ViewModel при каждом изменении ответов и по завершении блока.
/// Сервис не зависит от UI.
/// </summary>
public class SessionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;

    /// <param name="filePath">Путь к файлу сессии; по умолчанию %APPDATA%/ZharikovApp/session.json.</param>
    public SessionService(string? filePath = null)
    {
        _filePath = filePath ?? PathHelper.SessionFilePath;
    }

    /// <summary>Загрузить сессию; если файла нет или он повреждён — новая пустая сессия.</summary>
    public AppSession Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new AppSession();

            var json = File.ReadAllText(_filePath);
            var session = JsonSerializer.Deserialize<AppSession>(json, JsonOptions);
            return session ?? new AppSession();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Повреждённый файл не должен блокировать работу: начинаем новую сессию.
            return new AppSession();
        }
    }

    /// <summary>Сохранить сессию (атомарно: запись во временный файл + замена).</summary>
    public void Save(AppSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var dir = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(dir);

        var tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(session, JsonOptions));
        File.Move(tempPath, _filePath, overwrite: true);
    }

    /// <summary>Удалить сохранённую сессию (сброс прогресса).</summary>
    public void Clear()
    {
        if (File.Exists(_filePath))
            File.Delete(_filePath);
    }
}
