using System.Text.Json.Serialization;

namespace ZharikovApp.Models;

/// <summary>
/// Конфигурация подсчёта результатов блока.
/// Объединяет ключи, веса, формулы и правила интерпретации
/// для всех типов подсчёта (sum_key, hemisphere_sum, palmer_matrix и т. д.).
/// </summary>
public class ScoringConfig
{
    /// <summary>Тип подсчёта.</summary>
    [JsonPropertyName("type")]
    public ScoringType Type { get; set; }

    /// <summary>Максимальный балл (для одношкальных блоков).</summary>
    [JsonPropertyName("maxScore")]
    public int? MaxScore { get; set; }

    /// <summary>
    /// Ключ ответов: questionId → правильный ключ варианта
    /// (блоки 1, 2), либо questionId → список id вопросов группы (блок 3).
    /// Для Пальмера (блок 6): questionId → { optionKey: points }.
    /// </summary>
    [JsonPropertyName("keys")]
    public Dictionary<string, object>? Keys { get; set; }

    /// <summary>
    /// Именованные группы вопросов (блок 3: "left" = [q01,q02,q05,q08,q09],
    /// "right" = [q03,q04,q06,q07,q10]). Используется HemisphereSum.
    /// </summary>
    [JsonPropertyName("questionGroups")]
    public Dictionary<string, List<string>>? QuestionGroups { get; set; }

    /// <summary>Балл за один верный ответ (SumKeyWeighted, блок 2 = 5).</summary>
    [JsonPropertyName("pointsPerCorrectAnswer")]
    public int? PointsPerCorrectAnswer { get; set; }

    /// <summary>Веса вариантов ответа (блок 11: a=0, b=1, c=3, d=4, e=2).</summary>
    [JsonPropertyName("weights")]
    public Dictionary<string, int>? Weights { get; set; }

    /// <summary>
    /// Формула результата (блок 11: "sum * 2", результат — годы).
    /// Поддерживаются простые выражения вида "sum * K" / "sum + K".
    /// </summary>
    [JsonPropertyName("formula")]
    public string? Formula { get; set; }

    /// <summary>Единица измерения результата (например "years").</summary>
    [JsonPropertyName("resultUnit")]
    public string? ResultUnit { get; set; }

    /// <summary>Название шкалы по нечётным вопросам (Лидер / Т).</summary>
    [JsonPropertyName("oddScaleName")]
    public string? OddScaleName { get; set; }

    /// <summary>Название шкалы по чётным вопросам (Администратор / П).</summary>
    [JsonPropertyName("evenScaleName")]
    public string? EvenScaleName { get; set; }

    /// <summary>Максимум на каждую из двух шкал (блоки 7, 12 = 100).</summary>
    [JsonPropertyName("maxScorePerScale")]
    public int? MaxScorePerScale { get; set; }

    /// <summary>Формат вывода результата.</summary>
    [JsonPropertyName("resultFormat")]
    public ResultFormat ResultFormat { get; set; } = ResultFormat.Score;

    /// <summary>Правила интерпретации результата.</summary>
    [JsonPropertyName("interpretations")]
    public List<InterpretationRule> Interpretations { get; set; } = new();

    /// <summary>Дополнительные параметры подсчёта (например группы полушарий блока 3).</summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>Проверка корректности конфигурации подсчёта.</summary>
    public bool IsValid(out IReadOnlyList<string> errors)
    {
        var list = new List<string>();

        if (MaxScore is < 0)
            list.Add($"ScoringConfig: MaxScore ({MaxScore}) не может быть отрицательным.");
        if (PointsPerCorrectAnswer is < 0)
            list.Add($"ScoringConfig: PointsPerCorrectAnswer ({PointsPerCorrectAnswer}) не может быть отрицательным.");
        if (MaxScorePerScale is < 0)
            list.Add($"ScoringConfig: MaxScorePerScale ({MaxScorePerScale}) не может быть отрицательным.");

        if (Type is ScoringType.OddEvenSum &&
            (string.IsNullOrWhiteSpace(OddScaleName) || string.IsNullOrWhiteSpace(EvenScaleName)))
        {
            list.Add("ScoringConfig: для OddEvenSum обязательны OddScaleName и EvenScaleName.");
        }

        foreach (var rule in Interpretations)
        {
            if (!rule.IsValid(out var ruleErrors))
                list.AddRange(ruleErrors);
        }

        errors = list;
        return list.Count == 0;
    }
}
