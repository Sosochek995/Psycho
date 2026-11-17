using System.Text.Json.Serialization;

namespace ZharikovApp.Models;

/// <summary>
/// Корневая модель пользовательской сессии, сохраняемая в JSON-файл
/// (например %APPDATA%/ZharikovApp/session.json).
/// Содержит текущее состояние прохождения и историю результатов.
/// </summary>
public class AppSession
{
    /// <summary>Версия формата сохранения (для будущих миграций).</summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>Идентификатор сессии (GUID).</summary>
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Имя пользователя/профиля (необязательное).</summary>
    [JsonPropertyName("userName")]
    public string? UserName { get; set; }

    /// <summary>Дата создания сессии.</summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>Текущее незавершённое прохождение блока.</summary>
    [JsonPropertyName("currentState")]
    public SessionState CurrentState { get; set; } = new();

    /// <summary>История завершённых блоков (по одному результату на блок).</summary>
    [JsonPropertyName("results")]
    public List<BlockResult> Results { get; set; } = new();

    /// <summary>Количество пройденных блоков (0–12).</summary>
    [JsonIgnore]
    public int CompletedCount => Results.Count(r => r.IsCompleted);

    /// <summary>Все ли 12 блоков пройдены.</summary>
    [JsonIgnore]
    public bool IsFullyCompleted => CompletedCount >= 12;

    /// <summary>Добавить/заменить результат блока (idempotent по BlockId).</summary>
    public void UpsertResult(BlockResult result)
    {
        var index = Results.FindIndex(r =>
            string.Equals(r.BlockId, result.BlockId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            Results[index] = result;
        else
            Results.Add(result);
    }

    /// <summary>Получить результат блока по id (null, если блок не пройден).</summary>
    public BlockResult? GetResult(string blockId) =>
        Results.FirstOrDefault(r =>
            string.Equals(r.BlockId, blockId, StringComparison.OrdinalIgnoreCase));

    /// <summary>Проверка корректности всей сессии.</summary>
    public bool IsValid(out IReadOnlyList<string> errors)
    {
        var list = new List<string>();

        if (Version < 1)
            list.Add($"AppSession: Version ({Version}) должна быть >= 1.");
        if (string.IsNullOrWhiteSpace(SessionId))
            list.Add("AppSession: SessionId не может быть пустым.");

        if (!CurrentState.IsValid(out var stateErrors))
            list.AddRange(stateErrors.Select(e => $"AppSession: {e}"));

        var seenBlocks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in Results)
        {
            if (!result.IsValid(out var resultErrors))
                list.AddRange(resultErrors.Select(e => $"AppSession: {e}"));
            if (!seenBlocks.Add(result.BlockId))
                list.Add($"AppSession: дублируется результат блока '{result.BlockId}'.");
        }

        errors = list;
        return list.Count == 0;
    }
}
