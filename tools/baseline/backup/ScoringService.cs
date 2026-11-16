using System.Globalization;
using System.Text.Json;
using ZharikovApp.Models;
using ZharikovApp.Services.Interfaces;

namespace ZharikovApp.Services;

/// <summary>
/// Реализация <see cref="IScoringService"/>: девять стратегий подсчёта
/// (SumKey, SumKeyWeighted, SumScale, OddEvenSum, FormulaMultiplier,
/// CountLeft, SequenceCombo, PalmerMatrix, QualitativeProfile)
/// плюс HemisphereSum для блока 3.
/// Сервис не зависит от UI; расчёты детерминированы:
/// одинаковые (block, answers) дают одинаковый результат (кроме CompletedAt).
/// </summary>
public class ScoringService : IScoringService
{
    // =====================================================================
    //  Валидация ответов
    // =====================================================================

    /// <inheritdoc />
    public IReadOnlyList<string> ValidateAnswers(TestBlock block, UserAnswers answers)
    {
        if (block is null) throw new ArgumentNullException(nameof(block));
        if (answers is null) throw new ArgumentNullException(nameof(answers));

        var errors = new List<string>();

        if (!string.Equals(answers.BlockId, block.Id, StringComparison.OrdinalIgnoreCase))
            errors.Add($"Ответы относятся к блоку '{answers.BlockId}', а не к '{block.Id}'.");

        var questionIds = new HashSet<string>(block.Questions.Select(q => q.Id), StringComparer.OrdinalIgnoreCase);
        foreach (var key in answers.Values.Keys)
            if (!questionIds.Contains(key))
                errors.Add($"Найден ответ на несуществующий вопрос '{key}'.");

        foreach (var q in block.Questions)
        {
            var value = answers.Get(q.Id);

            if (string.IsNullOrWhiteSpace(value))
            {
                if (q.Required)
                    errors.Add($"Вопрос '{q.Id}' обязателен, ответ не дан.");
                continue;
            }

            switch (q.Type)
            {
                case QuestionType.Scale:
                    if (q.Scale is not null && TryParseInt(value, out var num))
                    {
                        if (!q.Scale.Contains(num))
                            errors.Add($"Вопрос '{q.Id}': значение {num} вне шкалы [{q.Scale.Min}..{q.Scale.Max}].");
                    }
                    else
                    {
                        errors.Add($"Вопрос '{q.Id}': ожидалось числовое значение шкалы, получено '{value}'.");
                    }
                    break;

                default: // YesNo, SingleChoice, ImageSingleChoice, LeftRightChoice, ParameterChoice
                    if (q.Options.Count > 0 && q.Options.All(o => !OptionMatches(o, value)))
                        errors.Add($"Вопрос '{q.Id}': вариант '{value}' отсутствует среди допустимых " +
                                   $"({string.Join(", ", q.Options.Select(o => o.Key))}).");
                    break;
            }
        }

        return errors;
    }

    // =====================================================================
    //  Расчёт результата блока
    // =====================================================================

    /// <inheritdoc />
    public BlockResult CalculateBlock(TestBlock block, UserAnswers answers)
    {
        if (block is null) throw new ArgumentNullException(nameof(block));
        if (answers is null) throw new ArgumentNullException(nameof(answers));

        var validationErrors = ValidateAnswers(block, answers);
        if (validationErrors.Count > 0)
            throw new InvalidOperationException(
                $"Невалидные ответы для блока '{block.Id}': {string.Join(" | ", validationErrors)}");

        if (!block.IsValid(out var blockErrors))
            throw new InvalidOperationException(
                $"Блок '{block.Id}' некорректен: {string.Join(" | ", blockErrors)}");

        var result = block.Scoring.Type switch
        {
            ScoringType.SumKey => ScoreSumKey(block, answers),
            ScoringType.SumKeyWeighted => ScoreSumKeyWeighted(block, answers),
            ScoringType.SumScale => ScoreSumScale(block, answers),
            ScoringType.OddEvenSum => ScoreOddEvenSum(block, answers),
            ScoringType.HemisphereSum => ScoreHemisphereSum(block, answers),
            ScoringType.FormulaMultiplier or ScoringType.WeightedFormula => ScoreFormulaMultiplier(block, answers),
            ScoringType.CountLeft => ScoreCountLeft(block, answers),
            ScoringType.SequenceCombo => ScoreSequenceCombo(block, answers),
            ScoringType.PalmerMatrix => ScorePalmerMatrix(block, answers),
            ScoringType.QualitativeProfile => ScoreQualitativeProfile(block, answers),
            _ => throw new NotSupportedException($"Тип подсчёта '{block.Scoring.Type}' не поддерживается."),
        };

        result.BlockId = block.Id;
        result.Title = block.Title;
        result.IsCompleted = true;

        _lastBlock = block; // контекст для GetInterpretation(result)
        ApplyInterpretation(block, result);
        return result;
    }

    // =====================================================================
    //  Стратегия 1. SumKey — 1 балл за совпадение с ключом (блок 1)
    // =====================================================================

    private static BlockResult ScoreSumKey(TestBlock block, UserAnswers answers)
    {
        var details = new Dictionary<string, string>();
        double score = 0;

        foreach (var q in block.Questions)
        {
            var answer = answers.Get(q.Id) ?? string.Empty;
            var correct = block.Scoring.Keys is not null &&
                          block.Scoring.Keys.TryGetValue(q.Id, out var keyElement) &&
                          KeyEquals(GetStringValue(keyElement), answer);
            if (correct) score += 1;
            details[q.Id] = $"{FormatOptionText(q, answer)} — {(correct ? "+1" : "0")}";
        }

        return new BlockResult
        {
            Scores = new Dictionary<string, double> { ["score"] = score },
            PrimaryValue = score,
            Details = details,
        };
    }

    // =====================================================================
    //  Стратегия 2. SumKeyWeighted — совпадение с ключом × вес (блок 2)
    // =====================================================================

    private static BlockResult ScoreSumKeyWeighted(TestBlock block, UserAnswers answers)
    {
        var weight = block.Scoring.PointsPerCorrectAnswer ?? 1;
        var details = new Dictionary<string, string>();
        double score = 0;
        int correctCount = 0;

        foreach (var q in block.Questions)
        {
            var answer = answers.Get(q.Id) ?? string.Empty;
            var correct = block.Scoring.Keys is not null &&
                          block.Scoring.Keys.TryGetValue(q.Id, out var keyElement) &&
                          KeyEquals(GetStringValue(keyElement), answer);
            if (correct)
            {
                score += weight;
                correctCount++;
            }
            details[q.Id] = $"{FormatOptionText(q, answer)} — {(correct ? $"+{weight}" : "0")}";
        }

        details["correct_count"] = correctCount.ToString(CultureInfo.InvariantCulture);

        return new BlockResult
        {
            Scores = new Dictionary<string, double> { ["score"] = score },
            PrimaryValue = score,
            Details = details,
        };
    }

    // =====================================================================
    //  Стратегия 3. SumScale — сумма всех баллов шкалы (блок 4)
    // =====================================================================

    private static BlockResult ScoreSumScale(TestBlock block, UserAnswers answers)
    {
        var details = new Dictionary<string, string>();
        double score = 0;

        foreach (var q in block.Questions)
        {
            var value = ParseScaleValue(answers.Get(q.Id));
            score += value;
            details[q.Id] = value.ToString(CultureInfo.InvariantCulture);
        }

        return new BlockResult
        {
            Scores = new Dictionary<string, double> { ["score"] = score },
            PrimaryValue = score,
            Details = details,
        };
    }

    // =====================================================================
    //  Стратегия 4. OddEvenSum — две шкалы: нечётные / чётные (блоки 7, 12)
    // =====================================================================

    private static BlockResult ScoreOddEvenSum(TestBlock block, UserAnswers answers)
    {
        var oddName = block.Scoring.OddScaleName ?? "odd";
        var evenName = block.Scoring.EvenScaleName ?? "even";
        var details = new Dictionary<string, string>();
        double odd = 0, even = 0;

        for (int i = 0; i < block.Questions.Count; i++)
        {
            var q = block.Questions[i];
            var value = ParseScaleValue(answers.Get(q.Id));
            bool isOdd = IsOddNumbered(block, q, i);
            if (isOdd) odd += value; else even += value;
            details[q.Id] = $"{(isOdd ? oddName : evenName)}: {value}";
        }

        var scores = new Dictionary<string, double> { [oddName] = odd, [evenName] = even };
        return new BlockResult
        {
            Scores = scores,
            PrimaryValue = Math.Max(odd, even),
            Details = details,
        };
    }

    // =====================================================================
    //  HemisphereSum — суммы по группам вопросов Л/П (блок 3)
    // =====================================================================

    private static BlockResult ScoreHemisphereSum(TestBlock block, UserAnswers answers)
    {
        var groups = block.Scoring.QuestionGroups
            ?? throw new InvalidOperationException($"Блок '{block.Id}': для HemisphereSum не заданы QuestionGroups.");

        var leftIds = groups.TryGetValue("left", out var l) ? l : new List<string>();
        var rightIds = groups.TryGetValue("right", out var r) ? r : new List<string>();

        double Sum(IEnumerable<string> ids) => ids
            .Select(id => block.Questions.FirstOrDefault(q => string.Equals(q.Id, id, StringComparison.OrdinalIgnoreCase)))
            .Where(q => q is not null)
            .Sum(q => ParseScaleValue(answers.Get(q!.Id)));

        double left = Sum(leftIds), right = Sum(rightIds);

        var details = new Dictionary<string, string>
        {
            ["left_questions"] = string.Join(",", leftIds),
            ["right_questions"] = string.Join(",", rightIds),
            ["left_sum"] = left.ToString(CultureInfo.InvariantCulture),
            ["right_sum"] = right.ToString(CultureInfo.InvariantCulture),
            ["diff_L_minus_P"] = (left - right).ToString(CultureInfo.InvariantCulture),
        };

        return new BlockResult
        {
            Scores = new Dictionary<string, double> { ["Л"] = left, ["П"] = right },
            PrimaryValue = left - right,
            Details = details,
        };
    }

    // =====================================================================
    //  Стратегия 5. FormulaMultiplier — Σ весов вариантов × коэффициент (блок 11)
    //  Формула источника: Т = 2 × (1·б + 3·в + 4·г + 2·д), «а» = 0.
    // =====================================================================

    private static BlockResult ScoreFormulaMultiplier(TestBlock block, UserAnswers answers)
    {
        var weights = block.Scoring.Weights
            ?? throw new InvalidOperationException($"Блок '{block.Id}': для FormulaMultiplier не заданы Weights.");

        double sum = 0;
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var details = new Dictionary<string, string>();

        foreach (var q in block.Questions)
        {
            var answer = answers.Get(q.Id) ?? string.Empty;
            var weight = weights.TryGetValue(NormalizeOptionKey(answer), out var w) ? w : 0;
            sum += weight;
            counts[NormalizeOptionKey(answer)] = counts.GetValueOrDefault(NormalizeOptionKey(answer)) + 1;
            details[q.Id] = $"{answer} → {weight}";
        }

        double multiplier = ParseFormulaMultiplier(block.Scoring.Formula);
        double total = sum * multiplier;

        foreach (var (key, count) in counts.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            details[$"count_{key}"] = count.ToString(CultureInfo.InvariantCulture);
        details["weighted_sum"] = sum.ToString(CultureInfo.InvariantCulture);
        details["multiplier"] = multiplier.ToString(CultureInfo.InvariantCulture);

        return new BlockResult
        {
            Scores = new Dictionary<string, double> { ["score"] = total },
            PrimaryValue = total,
            Details = details,
        };
    }

    // =====================================================================
    //  Стратегия 6. CountLeft — количество ответов «левая» (блок 10)
    // =====================================================================

    private static BlockResult ScoreCountLeft(TestBlock block, UserAnswers answers)
    {
        var details = new Dictionary<string, string>();
        double leftCount = 0;

        foreach (var q in block.Questions)
        {
            var raw = answers.Get(q.Id) ?? string.Empty;
            // Ответ может быть задан ключом ("left") или текстом варианта ("левая").
            var option = q.Options.FirstOrDefault(o => OptionMatches(o, raw));
            var answer = NormalizeOptionKey(option?.Key ?? raw);
            bool isLeft = answer is "l" or "left" or "левая" or "лево" or "ле";
            if (isLeft) leftCount++;
            details[q.Id] = $"{answer} — {(isLeft ? "+1" : "0")}";
        }

        return new BlockResult
        {
            Scores = new Dictionary<string, double> { ["score"] = leftCount },
            PrimaryValue = leftCount,
            Details = details,
        };
    }

    // =====================================================================
    //  Стратегия 7. SequenceCombo — строка из букв сторон (блок 5)
    // =====================================================================

    private static BlockResult ScoreSequenceCombo(TestBlock block, UserAnswers answers)
    {
        var letters = new char[block.Questions.Count];
        var details = new Dictionary<string, string>();

        for (int i = 0; i < block.Questions.Count; i++)
        {
            var q = block.Questions[i];
            var answer = NormalizeOptionKey(answers.Get(q.Id) ?? string.Empty);
            letters[i] = answer switch
            {
                "l" or "left" or "левая" or "лево" or "ле" or "л" => 'Л',
                "r" or "right" or "правая" or "право" or "пр" or "п" => 'П',
                _ => throw new InvalidOperationException($"Вопрос '{q.Id}': неизвестное значение стороны '{answer}'."),
            };
            details[q.Id] = letters[i].ToString();
        }

        var combo = new string(letters);
        return new BlockResult
        {
            Scores = new Dictionary<string, double>(),
            PrimaryValue = null,
            Level = combo,
            Details = details,
        };
    }

    // =====================================================================
    //  Стратегия 8. PalmerMatrix — баллы по матрице (блок 6)
    //  Keys: questionId → { optionKey: points }.
    // =====================================================================

    private static BlockResult ScorePalmerMatrix(TestBlock block, UserAnswers answers)
    {
        var keys = block.Scoring.Keys
            ?? throw new InvalidOperationException($"Блок '{block.Id}': для PalmerMatrix не заданы Keys (матрица).");

        var details = new Dictionary<string, string>();
        double score = 0;

        foreach (var q in block.Questions)
        {
            var answer = NormalizeOptionKey(answers.Get(q.Id) ?? string.Empty);
            int points = 0;
            if (keys.TryGetValue(q.Id, out var matrixElement))
            {
                var row = ParseOptionPoints(matrixElement);
                row.TryGetValue(answer, out points);
            }
            score += points;
            details[q.Id] = $"{FormatOptionText(q, answer)} — {points}";
        }

        return new BlockResult
        {
            Scores = new Dictionary<string, double> { ["score"] = score },
            PrimaryValue = score,
            Details = details,
        };
    }

    // =====================================================================
    //  Стратегия 9. QualitativeProfile — текстовый профиль без баллов (блоки 8, 9)
    // =====================================================================

    private static BlockResult ScoreQualitativeProfile(TestBlock block, UserAnswers answers)
    {
        var details = new Dictionary<string, string>();
        var profileParts = new List<string>();

        foreach (var q in block.Questions)
        {
            var answer = answers.Get(q.Id) ?? string.Empty;
            var option = q.Options.FirstOrDefault(o => KeyEquals(o.Key, answer));
            var groupLabel = q.Group ?? q.Text;
            var interpretation = option?.Interpretation ?? option?.Text ?? answer;

            details[q.Id] = $"{groupLabel}: {option?.Text ?? answer}";
            profileParts.Add($"{groupLabel} — {interpretation}");
        }

        return new BlockResult
        {
            Scores = new Dictionary<string, double>(),
            PrimaryValue = null,
            Level = "Профиль",
            Summary = string.Join(Environment.NewLine, profileParts),
            Details = details,
        };
    }

    // =====================================================================
    //  Интерпретация
    // =====================================================================

    /// <inheritdoc />
    public InterpretationRule? GetInterpretation(BlockResult result)
    {
        if (result is null) throw new ArgumentNullException(nameof(result));
        if (_lastBlock is null || !string.Equals(_lastBlock.Id, result.BlockId, StringComparison.OrdinalIgnoreCase))
            return null; // нет контекста блока — подбор правила невозможен
        return FindRule(_lastBlock, result);
    }

    private TestBlock? _lastBlock;

    /// <summary>Подобрать правило интерпретации для результата и записать его в результат.</summary>
    private void ApplyInterpretation(TestBlock block, BlockResult result)
    {
        var matched = FindRule(block, result);
        if (matched is null) return;

        if (result.Level is null || result.Level == "Профиль")
            result.Level = matched.Level;

        if (block.Scoring.Type != ScoringType.QualitativeProfile || string.IsNullOrEmpty(result.Summary))
            result.Summary = matched.Text;
    }

    /// <summary>
    /// Поиск подходящего правила. Приоритет: точное строковое значение (Value) →
    /// условие сравнения двух шкал (Condition) → числовой диапазон (Min/Max).
    /// </summary>
    private static InterpretationRule? FindRule(TestBlock block, BlockResult result)
    {
        var rules = block.Scoring.Interpretations;
        InterpretationRule? matched = null;

        // 1) Строковое совпадение (блок 5: комбинации Л/П).
        if (result.Level is not null)
            matched = rules.FirstOrDefault(r => r.MatchesValue(result.Level));

        // 2) Условия сравнения двух шкал (блоки 3, 7, 12).
        if (matched is null && result.Scores.Count >= 2)
        {
            var values = result.Scores.Values.ToArray();
            double odd = values[0], even = values[1];
            double thresholdLow = (block.Scoring.MaxScorePerScale ?? block.Scoring.MaxScore ?? 100) * 0.3;

            foreach (var rule in rules)
            {
                if (string.IsNullOrEmpty(rule.Condition)) continue;
                if (MatchesCondition(rule.Condition, odd, even, thresholdLow))
                {
                    matched = rule;
                    break;
                }
            }
        }

        // 3) Числовой диапазон по основному значению (блоки 1, 2, 4, 6, 10, 11).
        if (matched is null && result.PrimaryValue is not null)
            matched = rules.FirstOrDefault(r => (r.Min is not null || r.Max is not null)
                                                && r.MatchesScore(result.PrimaryValue.Value));

        return matched;
    }

    /// <summary>Разбор условий вида oddGtEven / evenGtOdd / bothLow / absDiffLt:N / oddGtBy:N / evenGtBy:N.</summary>
    private static bool MatchesCondition(string condition, double first, double second, double lowThreshold)
    {
        var parts = condition.Split(':', 2);
        switch (parts[0].Trim().ToLowerInvariant())
        {
            case "oddgteven": return first > second;
            case "evengtodds": case "evengtodd": return second > first;
            case "bothlow": return first <= lowThreshold && second <= lowThreshold;
            case "equal": return Math.Abs(first - second) < double.Epsilon;
            case "absdifflt": return Math.Abs(first - second) < ParseArg(parts, 5);
            case "oddgtby": return first - second > ParseArg(parts, 0);
            case "evengtby": return second - first > ParseArg(parts, 0);
            default: return false;
        }
    }

    private static double ParseArg(string[] parts, double fallback) =>
        parts.Length == 2 && TryParseDouble(parts[1], out var v) ? v : fallback;

    // =====================================================================
    //  Вспомогательные методы
    // =====================================================================

    /// <summary>Нечётный ли вопрос (по числовому суффиксу id, напр. q07; иначе — по позиции).</summary>
    private static bool IsOddNumbered(TestBlock block, Question q, int indexPosition)
    {
        // Авторитетный источник нумерации — номер пункта в опроснике (docs/methodics_source.md).
        // 1) sourceNumber (для блоков 7 и 12 заполнен по источнику);
        // 2) иначе порядковый номер в массиве (позиция 0 ⇒ вопрос №1 ⇒ нечётный);
        // 3) сырой индекс массива как номер НИКОГДА не используется.
        if (q.SourceNumber is int sn && sn > 0)
            return sn % 2 == 1;
        return indexPosition % 2 == 0; // позиция 0 ⇒ вопрос №1 ⇒ нечётный
    }

    private static string NormalizeOptionKey(string key) => key.Trim().ToLowerInvariant() switch
    {
        "yes" or "y" or "да" or "д" => "a",
        "no" or "n" or "нет" or "н" => "b",
        // кириллические литеры вариантов (а, б, в, …) приводим к латинским (a, b, c, …),
        // чтобы ключи/матрицы источника («1а», «а = 7») совпадали с ключами опций JSON.
        var other when RuLetterIndex(other) >= 0 => LatinForRuIndex(RuLetterIndex(other)),
        _ => key.Trim().ToLowerInvariant(),
    };

    /// <summary>Индекс русской литеры варианта (а→0, б→1, в→2, г→3, д→4, е→5), иначе -1.</summary>
    private static int RuLetterIndex(string s) => s switch
    {
        "а" => 0, "б" => 1, "в" => 2, "г" => 3, "д" => 4, "е" => 5,
        _ => -1,
    };

    private static string LatinForRuIndex(int i) => "abcdef"[i].ToString();

    private static bool KeyEquals(string? a, string? b)
    {
        if (a is null || b is null) return false;
        var na = NormalizeOptionKey(a);
        var nb = NormalizeOptionKey(b);
        return string.Equals(na, nb, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase) ||
               SideSynonym(a) is not null && SideSynonym(a) == SideSynonym(b);
    }

    /// <summary>Нормализация русских названий сторон в left/right (для LeftRightChoice).</summary>
    private static string? SideSynonym(string? key) => key?.Trim().ToLowerInvariant() switch
    {
        "левая" or "лево" or "левый" or "л" => "left",
        "правая" or "право" or "правый" or "п" => "right",
        _ => null,
    };

    /// <summary>Совпадает ли ответ с вариантом: по ключу либо по полному тексту варианта.</summary>
    private static bool OptionMatches(AnswerOption option, string answer) =>
        KeyEquals(option.Key, answer) ||
        string.Equals(option.Text?.Trim(), answer.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string FormatOptionText(Question q, string answerKey)
    {
        var option = q.Options.FirstOrDefault(o => KeyEquals(o.Key, answerKey) ||
                                                  string.Equals(o.Text?.Trim(), answerKey.Trim(), StringComparison.OrdinalIgnoreCase));
        return option is null ? answerKey : $"{option.Key}) {option.Text}";
    }

    private static int ParseScaleValue(string? raw) =>
        TryParseInt(raw ?? string.Empty, out var v) ? v : 0;

    private static bool TryParseInt(string s, out int value) =>
        int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool TryParseDouble(string s, out double value) =>
        double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    /// <summary>Достаёт строку из JsonElement-значения словаря Keys.</summary>
    private static string GetStringValue(object? element) => element switch
    {
        JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString() ?? string.Empty,
        string s => s,
        null => string.Empty,
        _ => element.ToString() ?? string.Empty,
    };

    /// <summary>Разбирает строку матрицы Пальмера вида "a=0,b=4,c=7" либо JSON-объект.</summary>
    private static Dictionary<string, int> ParseOptionPoints(object? element)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        switch (element)
        {
            case JsonElement je when je.ValueKind == JsonValueKind.Object:
                foreach (var prop in je.EnumerateObject())
                    if (prop.Value.TryGetInt32(out var n))
                        map[prop.Name] = n;
                break;
            case JsonElement js when js.ValueKind == JsonValueKind.String:
                FillFromPairs(js.GetString());
                break;
            case string s:
                FillFromPairs(s);
                break;
        }
        return map;

        void FillFromPairs(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            foreach (var pair in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var kv = pair.Split('=', 2);
                if (kv.Length == 2 && int.TryParse(kv[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                    map[kv[0].Trim()] = v;
            }
        }
    }

    /// <summary>Извлекает множитель из формулы вида "sum * 2" (по умолчанию 1).</summary>
    private static double ParseFormulaMultiplier(string? formula)
    {
        if (string.IsNullOrWhiteSpace(formula)) return 1;
        var m = System.Text.RegularExpressions.Regex.Match(formula, @"\*\s*(\d+(?:[.,]\d+)?)");
        if (m.Success && TryParseDouble(m.Groups[1].Value, out var k)) return k;
        return 1;
    }
}
