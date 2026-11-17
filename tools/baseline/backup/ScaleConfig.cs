using System.Text.Json.Serialization;

namespace ZharikovApp.Models;

/// <summary>
/// Конфигурация числовой шкалы для вопросов типа Scale
/// (например 0–10 в блоках 3, 7 или 0–4 в блоках 4, 12).
/// </summary>
public class ScaleConfig
{
    /// <summary>Минимальное значение шкалы.</summary>
    [JsonPropertyName("min")]
    public int Min { get; set; }

    /// <summary>Максимальное значение шкалы.</summary>
    [JsonPropertyName("max")]
    public int Max { get; set; }

    /// <summary>Подпись минимума (например «полное несогласие»).</summary>
    [JsonPropertyName("minLabel")]
    public string? MinLabel { get; set; }

    /// <summary>Подпись максимума (например «полное согласие»).</summary>
    [JsonPropertyName("maxLabel")]
    public string? MaxLabel { get; set; }

    /// <summary>Шаг шкалы (по умолчанию 1).</summary>
    [JsonPropertyName("step")]
    public int Step { get; set; } = 1;

    /// <summary>Проверка корректности конфигурации шкалы.</summary>
    public bool IsValid(out IReadOnlyList<string> errors)
    {
        var list = new List<string>();
        if (Min >= Max)
            list.Add($"ScaleConfig: Min ({Min}) должен быть меньше Max ({Max}).");
        if (Step <= 0)
            list.Add($"ScaleConfig: Step ({Step}) должен быть положительным.");
        errors = list;
        return list.Count == 0;
    }

    /// <summary>Проверка попадания значения в диапазон шкалы.</summary>
    public bool Contains(int value) => value >= Min && value <= Max;
}
