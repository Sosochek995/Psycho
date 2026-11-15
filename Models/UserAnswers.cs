using System.Text.Json.Serialization;

namespace ZharikovApp.Models;

/// <summary>
/// Ответы пользователя на вопросы одного блока: questionId → значение ответа.
/// Формат значения зависит от типа вопроса:
/// "a"/"b"/... (SingleChoice, ImageSingleChoice), "yes"/"no" (YesNo),
/// число шкалы в виде строки ("7") (Scale), "left"/"right" (LeftRightChoice).
/// </summary>
public class UserAnswers
{
    /// <summary>Id блока, к которому относятся ответы.</summary>
    [JsonPropertyName("blockId")]
    public string BlockId { get; set; } = string.Empty;

    /// <summary>Ответы: questionId → значение.</summary>
    [JsonPropertyName("values")]
    public Dictionary<string, string> Values { get; set; } = new();

    public UserAnswers() { }

    public UserAnswers(string blockId) => BlockId = blockId;

    /// <summary>Получить ответ на вопрос (null, если нет).</summary>
    public string? Get(string questionId) =>
        Values.TryGetValue(questionId, out var v) ? v : null;

    /// <summary>Записать ответ на вопрос.</summary>
    public void Set(string questionId, string value) => Values[questionId] = value;

    /// <summary>Создать из состояния чернового прохождения.</summary>
    public static UserAnswers FromSessionState(SessionState state, string blockId) =>
        new(blockId) { Values = new Dictionary<string, string>(state.Answers) };

    /// <summary>Проверка корректности контейнера ответов.</summary>
    public bool IsValid(out IReadOnlyList<string> errors)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(BlockId))
            list.Add("UserAnswers: BlockId не может быть пустым.");
        foreach (var (key, value) in Values)
        {
            if (string.IsNullOrWhiteSpace(key))
                list.Add("UserAnswers: найден пустой ключ вопроса.");
            if (string.IsNullOrWhiteSpace(value))
                list.Add($"UserAnswers: пустой ответ на вопрос '{key}'.");
        }
        errors = list;
        return list.Count == 0;
    }
}
