using System.Text.Json.Serialization;

namespace ZharikovApp.Models;

/// <summary>
/// Правило интерпретации результата блока.
/// Покрывает все виды интерпретаций из источника методик:
/// 1) числовые диапазоны (блоки 1, 2, 4, 6, 10);
/// 2) точные значения/строки (блок 5 — комбинации Л/П);
/// 3) сравнение двух шкал (блоки 7, 12 — Leader/Administrator, T/P);
/// 4) свободные условия-выражения (блок 3 — «Л exceeds П более чем на 5»).
/// </summary>
public class InterpretationRule
{
    /// <summary>Нижняя граница диапазона (включительно), если применимо.</summary>
    [JsonPropertyName("min")]
    public double? Min { get; set; }

    /// <summary>Верхняя граница диапазона (включительно), если применимо.</summary>
    [JsonPropertyName("max")]
    public double? Max { get; set; }

    /// <summary>Точное значение (число или строка, напр. "ПППЛ" для блока 5).</summary>
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    /// <summary>
    /// Условие сравнения для двухшкальных блоков:
    /// "oddGtEven", "evenGtOdd", "bothLow", "absDiffLt:5", "oddGtBy:5" и т. п.
    /// </summary>
    [JsonPropertyName("condition")]
    public string? Condition { get; set; }

    /// <summary>Название уровня/типа (например «Экстраверт», «Мыслитель»).</summary>
    [JsonPropertyName("level")]
    public string Level { get; set; } = string.Empty;

    /// <summary>Текст интерпретации для показа пользователю.</summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Проверка корректности правила.</summary>
    public bool IsValid(out IReadOnlyList<string> errors)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(Level))
            list.Add("InterpretationRule: Level не может быть пустым.");
        if (string.IsNullOrWhiteSpace(Text))
            list.Add($"InterpretationRule '{Level}': Text не может быть пустым.");
        if (Min is not null && Max is not null && Min > Max)
            list.Add($"InterpretationRule '{Level}': Min ({Min}) больше Max ({Max}).");
        if (Min is null && Max is null && Value is null && Condition is null)
            list.Add($"InterpretationRule '{Level}': не задано ни Min/Max, ни Value, ни Condition.");
        errors = list;
        return list.Count == 0;
    }

    /// <summary>Применимость правила к числовому результату.</summary>
    public bool MatchesScore(double score) =>
        (Min is null || score >= Min) && (Max is null || score <= Max);

    /// <summary>Применимость правила к строковому результату (комбинации Л/П).</summary>
    public bool MatchesValue(string value) =>
        !string.IsNullOrEmpty(Value) &&
        string.Equals(Value, value, StringComparison.OrdinalIgnoreCase);
}
