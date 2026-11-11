using System.Text.Json.Serialization;

namespace ZharikovApp.Models;

/// <summary>
/// Полная модель блока методики, загружаемая из Data/Blocks/*.json.
/// Наследует базовые поля BlockBase и добавляет вопросы, изображение,
/// конфигурацию подсчёта и интерпретации.
/// </summary>
public class TestBlock : BlockBase
{
    /// <summary>Тип ответов блока (краткая характеристика, см. источник).</summary>
    [JsonPropertyName("answerType")]
    public QuestionType AnswerType { get; set; }

    /// <summary>Ожидаемое количество вопросов (для самопроверки источника).</summary>
    [JsonPropertyName("questionCountExpected")]
    public int? QuestionCountExpected { get; set; }

    /// <summary>Вопросы (или группы параметров для блоков 8–9).</summary>
    [JsonPropertyName("questions")]
    public List<Question> Questions { get; set; } = new();

    /// <summary>Изображения, используемые блоком (key → файл в Assets/Images).</summary>
    [JsonPropertyName("images")]
    public Dictionary<string, string>? Images { get; set; }

    /// <summary>Конфигурация подсчёта и интерпретаций.</summary>
    [JsonPropertyName("scoring")]
    public ScoringConfig Scoring { get; set; } = new();

    /// <summary>Произвольные дополнительные данные блока.</summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>Полная проверка блока: метаданные, вопросы, подсчёт.</summary>
    public bool IsValid(out IReadOnlyList<string> errors)
    {
        var list = new List<string>();

        if (string.IsNullOrWhiteSpace(Id))
            list.Add("TestBlock: Id не может быть пустым.");
        if (Order is < 1 or > 12)
            list.Add($"TestBlock '{Id}': Order ({Order}) вне диапазона 1–12.");
        if (string.IsNullOrWhiteSpace(Title))
            list.Add($"TestBlock '{Id}': Title не может быть пустым.");
        if (string.IsNullOrWhiteSpace(Instruction))
            list.Add($"TestBlock '{Id}': Instruction не может быть пустым.");

        if (Questions.Count == 0)
            list.Add($"TestBlock '{Id}': список вопросов пуст.");

        if (QuestionCountExpected is not null && QuestionCountExpected != Questions.Count)
            list.Add($"TestBlock '{Id}': ожидалось {QuestionCountExpected} вопросов, фактически {Questions.Count}.");

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var q in Questions)
        {
            if (!q.IsValid(out var qErrors))
                list.AddRange(qErrors.Select(e => $"TestBlock '{Id}': {e}"));
            if (!seenIds.Add(q.Id))
                list.Add($"TestBlock '{Id}': дублируется id вопроса '{q.Id}'.");
        }

        if (!Scoring.IsValid(out var scoringErrors))
            list.AddRange(scoringErrors.Select(e => $"TestBlock '{Id}': {e}"));

        errors = list;
        return list.Count == 0;
    }
}
