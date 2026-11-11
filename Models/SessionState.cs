using System.Text.Json.Serialization;

namespace ZharikovApp.Models;

/// <summary>
/// Состояние ТЕКУЩЕГО (незавершённого) прохождения блока:
/// черновые ответы, индекс текущего вопроса. Сохраняется в AppSession
/// для восстановления после перезапуска приложения.
/// </summary>
public class SessionState
{
    /// <summary>Id блока, который сейчас проходится (null — блок не начат).</summary>
    [JsonPropertyName("activeBlockId")]
    public string? ActiveBlockId { get; set; }

    /// <summary>Индекс текущего вопроса внутри активного блока (0-based).</summary>
    [JsonPropertyName("currentQuestionIndex")]
    public int CurrentQuestionIndex { get; set; }

    /// <summary>
    /// Черновые ответы: questionId → значение ответа.
    /// Формат значения зависит от типа вопроса: "a"/"b", "yes"/"no",
    /// число шкалы в виде строки ("7"), "left"/"right".
    /// </summary>
    [JsonPropertyName("answers")]
    public Dictionary<string, string> Answers { get; set; } = new();

    /// <summary>Время последнего изменения состояния.</summary>
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>Есть ли несохранённые ответы.</summary>
    [JsonIgnore]
    public bool HasAnswers => Answers.Count > 0;

    /// <summary>Очистка состояния (после завершения или отмены блока).</summary>
    public void Reset()
    {
        ActiveBlockId = null;
        CurrentQuestionIndex = 0;
        Answers.Clear();
        UpdatedAt = DateTimeOffset.Now;
    }

    /// <summary>Проверка корректности состояния.</summary>
    public bool IsValid(out IReadOnlyList<string> errors)
    {
        var list = new List<string>();
        if (ActiveBlockId is not null && string.IsNullOrWhiteSpace(ActiveBlockId))
            list.Add("SessionState: ActiveBlockId не может быть пустой строкой.");
        if (CurrentQuestionIndex < 0)
            list.Add($"SessionState: CurrentQuestionIndex ({CurrentQuestionIndex}) не может быть отрицательным.");
        foreach (var (key, value) in Answers)
        {
            if (string.IsNullOrWhiteSpace(key))
                list.Add("SessionState: найден пустой ключ ответа.");
            if (string.IsNullOrWhiteSpace(value))
                list.Add($"SessionState: ответ на вопрос '{key}' пуст.");
        }
        errors = list;
        return list.Count == 0;
    }
}
