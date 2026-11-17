using System.Text.Json.Serialization;

namespace ZharikovApp.Models;

/// <summary>
/// Вариант ответа на вопрос (например «а) да» или вариант выбора параметра
/// с встроенной интерпретацией).
/// </summary>
public class AnswerOption
{
    /// <summary>Ключ варианта: "a", "b", "c", "yes", "no", "left", "right" и т. п.</summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>Отображаемый текст варианта.</summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Интерпретация варианта (используется в ParameterChoice — блоки 8, 9).
    /// Может быть null для обычных вопросов.
    /// </summary>
    [JsonPropertyName("interpretation")]
    public string? Interpretation { get; set; }

    /// <summary>Произвольные дополнительные данные.</summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>Проверка корректности варианта.</summary>
    public bool IsValid(out IReadOnlyList<string> errors)
    {
        errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Key))
            ((List<string>)errors).Add("AnswerOption.Key не может быть пустым.");
        if (string.IsNullOrWhiteSpace(Text))
            ((List<string>)errors).Add($"AnswerOption '{Key}': Text не может быть пустым.");
        return errors.Count == 0;
    }
}
