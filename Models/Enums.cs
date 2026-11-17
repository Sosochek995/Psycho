using System.Text.Json.Serialization;

namespace ZharikovApp.Models;

/// <summary>Тип вопроса (способ ответа).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<QuestionType>))]
public enum QuestionType
{
    /// <summary>Да / Нет.</summary>
    YesNo,

    /// <summary>Одиночный выбор из текстовых вариантов (а, б, в...).</summary>
    SingleChoice,

    /// <summary>Оценка по числовой шкале.</summary>
    Scale,

    /// <summary>Одиночный выбор с опорой на изображение (тест Пальмера).</summary>
    ImageSingleChoice,

    /// <summary>Выбор «левая / правая» сторона.</summary>
    LeftRightChoice,

    /// <summary>Выбор значения параметра из набора групп параметров (графология, подпись).</summary>
    ParameterChoice,
}

/// <summary>Тип подсчёта результатов блока.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScoringType>))]
public enum ScoringType
{
    /// <summary>Блок 1: сумма совпадений с ключом (1 балл за верный ответ).</summary>
    SumKey,

    /// <summary>Блок 2: сумма баллов по ключу с фиксированной ценой верного ответа.</summary>
    SumKeyWeighted,

    /// <summary>Блок 3: суммы по полушариям (Л/П).</summary>
    HemisphereSum,

    /// <summary>Блоки 4, 7, 12: простая сумма ответов по шкале.</summary>
    SumScale,

    /// <summary>Блок 5: последовательность букв Л/П → комбинация.</summary>
    SequenceCombo,

    /// <summary>Блок 6: матрица баллов Пальмера.</summary>
    PalmerMatrix,

    /// <summary>Блоки 7, 12: суммы по нечётным/чётным вопросам (две шкалы).</summary>
    OddEvenSum,

    /// <summary>Блок 8, 9: качественный профиль без баллов.</summary>
    QualitativeProfile,

    /// <summary>Блок 10: подсчёт количества ответов «левая».</summary>
    CountLeft,

    /// <summary>Блок 11: сумма взвешенных баллов, умноженная на коэффициент из Formula.</summary>
    FormulaMultiplier,

    /// <summary>Блок 11 (альтернативное имя): взвешенная формула Т = 2 × (1·б + 3·в + 4·г + 2·д).</summary>
    WeightedFormula,
}

/// <summary>Формат представления результата блока.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ResultFormat>))]
public enum ResultFormat
{
    /// <summary>Число (баллы).</summary>
    Score,

    /// <summary>Две шкалы (например, Лидер/Администратор, Т/П).</summary>
    TwoScales,

    /// <summary>Строка из 4 букв Л/П (блок 5).</summary>
    FourLetters,

    /// <summary>Текстовый вывод (качественный профиль, тип и т. п.).</summary>
    Text,
}
