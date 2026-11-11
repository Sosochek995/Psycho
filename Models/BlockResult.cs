using System.Text.Json.Serialization;

namespace ZharikovApp.Models;

/// <summary>
/// Результат прохождения одного блока: баллы, уровень, текстовая сводка.
/// Сохраняется в AppSession.History.
/// </summary>
public class BlockResult
{
    /// <summary>Id блока (например "block_04_extraversion").</summary>
    [JsonPropertyName("blockId")]
    public string BlockId { get; set; } = string.Empty;

    /// <summary>Название блока.</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Дата и время завершения прохождения.</summary>
    [JsonPropertyName("completedAt")]
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>
    /// Именованные шкалы результата: "score", "leader"/"administrator",
    /// "T"/"P", "Л"/"П" и т. п. Для одношкальных блоков — ключ "score".
    /// </summary>
    [JsonPropertyName("scores")]
    public Dictionary<string, double> Scores { get; set; } = new();

    /// <summary>Главное числовое значение результата (основная шкала).</summary>
    [JsonPropertyName("primaryValue")]
    public double? PrimaryValue { get; set; }

    /// <summary>Найденный уровень/тип интерпретации («Экстраверт», «ПППЛ» и т. п.).</summary>
    [JsonPropertyName("level")]
    public string? Level { get; set; }

    /// <summary>Текст интерпретации для пользователя.</summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    /// <summary>Детали: ответы, попиксельный разбор баллов, профиль параметров.</summary>
    [JsonPropertyName("details")]
    public Dictionary<string, string>? Details { get; set; }

    /// <summary>Завершён ли блок полностью.</summary>
    [JsonPropertyName("isCompleted")]
    public bool IsCompleted { get; set; }

    /// <summary>Проверка корректности результата.</summary>
    public bool IsValid(out IReadOnlyList<string> errors)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(BlockId))
            list.Add("BlockResult: BlockId не может быть пустым.");
        if (string.IsNullOrWhiteSpace(Title))
            list.Add($"BlockResult '{BlockId}': Title не может быть пустым.");
        if (IsCompleted && Scores.Count == 0 && string.IsNullOrEmpty(Level))
            list.Add($"BlockResult '{BlockId}': завершённый результат должен содержать хотя бы одну шкалу или уровень.");
        errors = list;
        return list.Count == 0;
    }
}
