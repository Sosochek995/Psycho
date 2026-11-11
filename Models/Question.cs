using System.Text.Json.Serialization;

namespace ZharikovApp.Models;

/// <summary>
/// Вопрос блока методики. Покрывает все шесть типов ответов из источника:
/// YesNo, SingleChoice, Scale, ImageSingleChoice, LeftRightChoice, ParameterChoice.
/// </summary>
public class Question
{
    /// <summary>Уникальный id вопроса внутри блока, например "q01".</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Текст вопроса/утверждения/ситуации.</summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Тип вопроса.</summary>
    [JsonPropertyName("type")]
    public QuestionType Type { get; set; }

    /// <summary>Обязателен ли ответ (по умолчанию да).</summary>
    [JsonPropertyName("required")]
    public bool Required { get; set; } = true;

    /// <summary>Варианты ответа (пусто для Scale).</summary>
    [JsonPropertyName("options")]
    public List<AnswerOption> Options { get; set; } = new();

    /// <summary>Конфигурация шкалы (только для Scale).</summary>
    [JsonPropertyName("scale")]
    public ScaleConfig? Scale { get; set; }

    /// <summary>Ключ изображения из Assets/Images (блок 6 — фигуры Пальмера).</summary>
    [JsonPropertyName("imageKey")]
    public string? ImageKey { get; set; }

    /// <summary>Группа вопроса (параметр в блоках 8–9, фигура в блоке 6 и т. п.).</summary>
    [JsonPropertyName("group")]
    public string? Group { get; set; }

    /// <summary>Теги для подсчёта ("odd", "even", "left_hemisphere" и пр.).</summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    /// <summary>Произвольные дополнительные данные.</summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>Проверка корректности вопроса.</summary>
    public bool IsValid(out IReadOnlyList<string> errors)
    {
        var list = new List<string>();

        if (string.IsNullOrWhiteSpace(Id))
            list.Add("Question: Id не может быть пустым.");
        if (string.IsNullOrWhiteSpace(Text))
            list.Add($"Question '{Id}': Text не может быть пустым.");

        switch (Type)
        {
            case QuestionType.Scale:
                if (Scale is null)
                    list.Add($"Question '{Id}' (Scale): отсутствует Scale.");
                else if (!Scale.IsValid(out var scaleErrors))
                    list.AddRange(scaleErrors);
                break;

            case QuestionType.ImageSingleChoice:
                if (string.IsNullOrWhiteSpace(ImageKey))
                    list.Add($"Question '{Id}' (ImageSingleChoice): отсутствует ImageKey.");
                goto default;

            default:
                if (Options.Count < 2)
                    list.Add($"Question '{Id}': требуется не менее двух вариантов ответа.");
                break;
        }

        if (Options.Count > 0)
        {
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var opt in Options)
            {
                if (!opt.IsValid(out var optErrors))
                    list.AddRange(optErrors.Select(e => $"Question '{Id}': {e}"));
                if (!seenKeys.Add(opt.Key))
                    list.Add($"Question '{Id}': дублируется ключ варианта '{opt.Key}'.");
            }
        }

        errors = list;
        return list.Count == 0;
    }
}
