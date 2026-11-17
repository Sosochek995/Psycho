using Xunit;
using ZharikovApp.Models;
using ZharikovApp.Services;

namespace ZharikovApp.Tests;

/// <summary>
/// Модульные тесты ScoringService. Блоки собираются в коде строго по
/// docs/methodics_source.md (ключи, веса, интерпретации — дословно из источника).
/// </summary>
public class ScoringServiceTests
{
    private readonly ScoringService _service = new();

    // =====================================================================
    //  Конструкторы блоков-образцов (по материалам источника методик)
    // =====================================================================

    /// <summary>Блок 1 «Диагностика лидерских способностей»: SumKey, 50 вопросов, ключ 1а…50б.</summary>
    private static TestBlock BuildBlock1()
    {
        // Ключ блока 1 из источника: 1а, 2а, 3б, 4а, 5а, 6б, 7а, 8б, 9б, 10а, …, 50б.
        var keyString =
            "a,a,b,a,a,b,a,b,b,a," +   // 1–10
            "a,a,b,b,a,b,a,b,b,a," +   // 11–20
            "a,a,a,a,b,a,b,a,b,b," +   // 21–30
            "a,a,b,a,b,b,a,b,a,b," +   // 31–40
            "a,a,a,a,b,a,b,a,b,b";     // 41–50
        var keys = keyString.Split(',');
        Assert.Equal(50, keys.Length);

        var block = new TestBlock
        {
            Id = "block_01_leadership",
            Order = 1,
            Title = "Диагностика лидерских способностей",
            Instruction = "Вам предлагается 50 высказываний, на которые требуется дать ответ «да» или «нет».",
            AnswerType = QuestionType.YesNo,
            QuestionCountExpected = 50,
            Scoring = new ScoringConfig
            {
                Type = ScoringType.SumKey,
                MaxScore = 50,
                Keys = keys
                    .Select((k, i) => ($"q{i + 1:00}", (object)k))
                    .ToDictionary(x => x.Item1, x => x.Item2),
                Interpretations =
                {
                    new InterpretationRule { Max = 25, Level = "Слабо", Text = "Качества лидера выражены слабо." },
                    new InterpretationRule { Min = 26, Max = 35, Level = "Средне", Text = "Качества лидера выражены средне." },
                    new InterpretationRule { Min = 36, Max = 40, Level = "Сильно", Text = "Лидерские качества выражены сильно." },
                    new InterpretationRule { Min = 41, Level = "Диктат", Text = "Данный человек как лидер склонен к диктату." },
                },
            },
        };

        for (int i = 1; i <= 50; i++)
        {
            block.Questions.Add(new Question
            {
                Id = $"q{i:00}",
                Text = $"Высказывание {i}",
                Type = QuestionType.YesNo,
                Options = YesNoOptions(),
            });
        }
        return block;
    }

    /// <summary>Блок 2 «Любят ли тебя люди?»: SumKeyWeighted, цена верного ответа 5.</summary>
    private static TestBlock BuildBlock2()
    {
        // «да» = 5 баллов: 6, 13, 14, 20; «нет» = 5 баллов: остальные 16 вопросов.
        var yesKeys = new[] { 6, 13, 14, 20 };
        var block = NewYesNoBlock(
            id: "block_02_love", order: 2, title: "Любят ли тебя люди?", count: 20,
            scoring: new ScoringConfig
            {
                Type = ScoringType.SumKeyWeighted,
                PointsPerCorrectAnswer = 5,
                MaxScore = 100,
                Interpretations =
                {
                    new InterpretationRule { Min = 85, Max = 100, Level = "Отлично", Text = "Отлично, окружающие вас любят." },
                    new InterpretationRule { Min = 75, Max = 84, Level = "Хорошо", Text = "Хорошо." },
                    new InterpretationRule { Min = 65, Max = 74, Level = "Удовлетворительно", Text = "Удовлетворительно." },
                    new InterpretationRule { Max = 64, Level = "Плохо", Text = "Плохо." },
                },
            });

        block.Scoring.Keys = Enumerable.Range(1, 20)
            .Select(i => ($"q{i:00}", (object)(yesKeys.Contains(i) ? "a" : "b")))
            .ToDictionary(x => x.Item1, x => x.Item2);
        return block;
    }

    /// <summary>Блок 7 «Лидер или администратор»: OddEvenSum, шкалы Лидер/Администратор 0–100.</summary>
    private static TestBlock BuildBlock7() => NewScaleBlock(
        id: "block_07_leader_admin", order: 7, title: "Лидер или администратор", count: 20, min: 0, max: 10,
        scoring: new ScoringConfig
        {
            Type = ScoringType.OddEvenSum,
            OddScaleName = "Лидер",
            EvenScaleName = "Администратор",
            MaxScorePerScale = 100,
            ResultFormat = ResultFormat.TwoScales,
            Interpretations =
            {
                new InterpretationRule { Condition = "bothLow", Level = "Низкая активность", Text = "Низкая активность в управленческой деятельности." },
                new InterpretationRule { Condition = "oddGtEven", Level = "Демократ", Text = "Опора на коллегиальные формы руководства (демократ)." },
                new InterpretationRule { Condition = "evenGtOdd", Level = "Авторитар", Text = "Опора на административно-директивные установки (авторитар)." },
            },
        });

    /// <summary>Блок 10 «Экспресс-тест на леворукость»: CountLeft, 10 вопросов правая/левая.</summary>
    private static TestBlock BuildBlock10()
    {
        var block = new TestBlock
        {
            Id = "block_10_left_hand", Order = 10, Title = "Экспресс-тест на леворукость",
            Instruction = "Зафиксируйте, какой рукой вам естественнее выполнять действия.",
            AnswerType = QuestionType.LeftRightChoice, QuestionCountExpected = 10,
            Scoring = new ScoringConfig
            {
                Type = ScoringType.CountLeft,
                MaxScore = 10,
                Interpretations =
                {
                    new InterpretationRule { Min = 0, Max = 2, Level = "Правша", Text = "Выраженный правша." },
                    new InterpretationRule { Min = 3, Max = 5, Level = "Амбидекстр", Text = "Амбидекстр или скрытый левша." },
                    new InterpretationRule { Min = 6, Max = 8, Level = "Левша", Text = "Выраженный левша." },
                    new InterpretationRule { Min = 9, Max = 10, Level = "Чистый левша", Text = "Чистый левша." },
                },
            },
        };

        for (int i = 1; i <= 10; i++)
            block.Questions.Add(new Question
            {
                Id = $"q{i:00}", Text = $"Действие {i}",
                Type = QuestionType.LeftRightChoice,
                Options = new List<AnswerOption>
                {
                    new() { Key = "right", Text = "правая" },
                    new() { Key = "left", Text = "левая" },
                },
            });
        return block;
    }

    /// <summary>Блок 11 «Творческий возраст»: FormulaMultiplier, Т = 2 × (1·б + 3·в + 4·г + 2·д).</summary>
    private static TestBlock BuildBlock11()
    {
        var block = new TestBlock
        {
            Id = "block_11_creative_age", Order = 11, Title = "Творческий возраст",
            Instruction = "Выберите наиболее подходящую реакцию (от «а» до «д»).",
            AnswerType = QuestionType.SingleChoice, QuestionCountExpected = 10,
            Scoring = new ScoringConfig
            {
                Type = ScoringType.FormulaMultiplier,
                Formula = "sum * 2",
                ResultUnit = "years",
                Weights = new Dictionary<string, int> { ["a"] = 0, ["b"] = 1, ["c"] = 3, ["d"] = 4, ["e"] = 2 },
            },
        };

        for (int i = 1; i <= 10; i++)
            block.Questions.Add(new Question
            {
                Id = $"q{i:00}", Text = $"Ситуация {i}",
                Type = QuestionType.SingleChoice,
                Options = new List<AnswerOption>
                {
                    new() { Key = "a", Text = "вариант а" },
                    new() { Key = "b", Text = "вариант б" },
                    new() { Key = "c", Text = "вариант в" },
                    new() { Key = "d", Text = "вариант г" },
                    new() { Key = "e", Text = "вариант д" },
                },
            });
        return block;
    }

    /// <summary>Блок 12 «Творческий потенциал»: OddEvenSum, шкалы Т/П 0–100.</summary>
    private static TestBlock BuildBlock12() => NewScaleBlock(
        id: "block_12_creative_potential", order: 12, title: "Творческий потенциал", count: 50, min: 0, max: 4,
        scoring: new ScoringConfig
        {
            Type = ScoringType.OddEvenSum,
            OddScaleName = "Т",
            EvenScaleName = "П",
            MaxScorePerScale = 100,
            ResultFormat = ResultFormat.TwoScales,
            Interpretations =
            {
                new InterpretationRule { Condition = "bothLow", Level = "Спад активности", Text = "Снижение активности, усталость, отсутствие интереса к саморазвитию." },
                new InterpretationRule { Condition = "equal", Level = "Гармоничная личность", Text = "Потенциал полностью обеспечивается мотивацией и реализуется в поведении." },
                new InterpretationRule { Condition = "oddGtEven", Level = "Непризнанный гений", Text = "Есть творческие проявления, но может не хватать мотивации, амбиций или веры в себя." },
                new InterpretationRule { Condition = "evenGtOdd", Level = "Трудоголик/Карьерист", Text = "Высокая мотивация и амбиции, но может не хватать гибкости и оригинальности." },
            },
        });

    // ---- фабрики ---------------------------------------------------------

    private static List<AnswerOption> YesNoOptions() => new()
    {
        new() { Key = "a", Text = "да" },
        new() { Key = "b", Text = "нет" },
    };

    private static TestBlock NewYesNoBlock(string id, int order, string title, int count, ScoringConfig scoring)
    {
        var block = new TestBlock
        {
            Id = id, Order = order, Title = title,
            Instruction = "Инструкция", AnswerType = QuestionType.YesNo,
            QuestionCountExpected = count, Scoring = scoring,
        };
        for (int i = 1; i <= count; i++)
            block.Questions.Add(new Question
            {
                Id = $"q{i:00}", Text = $"Утверждение {i}",
                Type = QuestionType.YesNo, Options = YesNoOptions(),
            });
        return block;
    }

    private static TestBlock NewScaleBlock(string id, int order, string title, int count, int min, int max, ScoringConfig scoring)
    {
        var block = new TestBlock
        {
            Id = id, Order = order, Title = title,
            Instruction = "Инструкция", AnswerType = QuestionType.Scale,
            QuestionCountExpected = count, Scoring = scoring,
        };
        for (int i = 1; i <= count; i++)
            block.Questions.Add(new Question
            {
                Id = $"q{i:00}", Text = $"Утверждение {i}",
                Type = QuestionType.Scale, Scale = new ScaleConfig { Min = min, Max = max },
            });
        return block;
    }

    private static UserAnswers Answers(string blockId, params (string qid, string value)[] items)
    {
        var a = new UserAnswers(blockId);
        foreach (var (qid, value) in items) a.Set(qid, value);
        return a;
    }

    private static UserAnswers AllSame(TestBlock block, Func<int, string> valueFor)
    {
        var a = new UserAnswers(block.Id);
        for (int i = 0; i < block.Questions.Count; i++)
            a.Set(block.Questions[i].Id, valueFor(i + 1));
        return a;
    }

    private static string KeyOf(TestBlock block, int number) =>
        block.Scoring.Keys![$"q{number:00}"].ToString()!;

    // =====================================================================
    //  Блок 1 — SumKey
    // =====================================================================

    [Fact]
    public void Block1_AllKeyAnswers_Scores50_DictatLevel()
    {
        var block = BuildBlock1();
        var answers = AllSame(block, i => KeyOf(block, i));

        var result = _service.CalculateBlock(block, answers);

        Assert.Equal(50, result.PrimaryValue);
        Assert.Equal("Диктат", result.Level);
        Assert.Contains("склонен к диктату", result.Summary!);
        Assert.Equal(50, result.Details!.Count); // по детали на каждый вопрос
    }

    [Fact]
    public void Block1_AllOppositeAnswers_Scores0_SlaboLevel()
    {
        var block = BuildBlock1();
        var answers = AllSame(block, i => KeyOf(block, i) == "a" ? "b" : "a");

        var result = _service.CalculateBlock(block, answers);

        Assert.Equal(0, result.PrimaryValue);
        Assert.Equal("Слабо", result.Level);
    }

    [Fact]
    public void Block1_OneWrongAnswer_CountsOnlyMatches()
    {
        var block = BuildBlock1();
        // Первый вопрос — против ключа (q01 ключ «a», отвечаем «b»), остальные — по ключу → 49.
        var answers = AllSame(block, i => i == 1 ? "b" : KeyOf(block, i));

        var result = _service.CalculateBlock(block, answers);

        Assert.Equal(49, result.PrimaryValue);
        Assert.EndsWith("0", result.Details!["q01"]);   // q01 не засчитан
        Assert.EndsWith("+1", result.Details["q02"]);   // q02 засчитан
    }

    [Fact]
    public void Block1_MissingRequiredAnswer_FailsValidation()
    {
        var block = BuildBlock1();
        var answers = Answers(block.Id, ("q01", "a")); // только один ответ из 50

        var errors = _service.ValidateAnswers(block, answers);
        Assert.Equal(49, errors.Count);
        Assert.Throws<InvalidOperationException>(() => _service.CalculateBlock(block, answers));
    }

    // =====================================================================
    //  Блок 2 — SumKeyWeighted (5 баллов за верный ответ)
    // =====================================================================

    [Fact]
    public void Block2_AllCorrectScores100_Otlichno()
    {
        var block = BuildBlock2();
        var answers = AllSame(block, i => KeyOf(block, i));

        var result = _service.CalculateBlock(block, answers);

        Assert.Equal(100, result.PrimaryValue);
        Assert.Equal("Отлично", result.Level);
        Assert.Equal("20", result.Details!["correct_count"]);
    }

    [Fact]
    public void Block2_ThreeWrongAnswers_Scores85()
    {
        var block = BuildBlock2();
        var answers = AllSame(block, i =>
            i is 1 or 2 or 3
                ? (KeyOf(block, i) == "a" ? "b" : "a")
                : KeyOf(block, i));

        var result = _service.CalculateBlock(block, answers);

        Assert.Equal(85, result.PrimaryValue); // 17 × 5
        Assert.Equal("Отлично", result.Level); // 85 входит в диапазон 85–100
    }

    [Fact]
    public void Block2_KeyDirection_Q6YesIsCorrect()
    {
        var block = BuildBlock2();
        // По источнику: «да» оценивается в 5 баллов для вопросов 6, 13, 14, 20.
        Assert.Equal("a", KeyOf(block, 6));
        Assert.Equal("b", KeyOf(block, 1));

        var good = AllSame(block, i => KeyOf(block, i));
        var badQ6 = AllSame(block, i => i == 6 ? "b" : KeyOf(block, i));

        Assert.Equal(100, _service.CalculateBlock(block, good).PrimaryValue);
        Assert.Equal(95, _service.CalculateBlock(block, badQ6).PrimaryValue);
    }

    // =====================================================================
    //  Блок 7 — OddEvenSum (Лидер / Администратор)
    // =====================================================================

    [Fact]
    public void Block7_MaxOddMinEven_LeaderDemocrat()
    {
        var block = BuildBlock7();
        var answers = AllSame(block, i => i % 2 == 1 ? "10" : "0"); // нечётные=10, чётные=0

        var result = _service.CalculateBlock(block, answers);

        Assert.Equal(100, result.Scores["Лидер"]);
        Assert.Equal(0, result.Scores["Администратор"]);
        Assert.Equal("Демократ", result.Level);
    }

    [Fact]
    public void Block7_MaxEvenMinOdd_Avtoritar()
    {
        var block = BuildBlock7();
        var answers = AllSame(block, i => i % 2 == 0 ? "10" : "0");

        var result = _service.CalculateBlock(block, answers);

        Assert.Equal(0, result.Scores["Лидер"]);
        Assert.Equal(100, result.Scores["Администратор"]);
        Assert.Equal("Авторитар", result.Level);
    }

    [Fact]
    public void Block7_AllZeros_LowActivity()
    {
        var block = BuildBlock7();
        var answers = AllSame(block, _ => "0");

        var result = _service.CalculateBlock(block, answers);

        Assert.Equal("Низкая активность", result.Level);
    }

    [Fact]
    public void Block7_OutOfRangeValue_FailsValidation()
    {
        var block = BuildBlock7();
        var answers = AllSame(block, _ => "11"); // вне шкалы 0–10

        var errors = _service.ValidateAnswers(block, answers);
        Assert.Equal(20, errors.Count);
    }

    // =====================================================================
    //  Блок 10 — CountLeft
    // =====================================================================

    [Fact]
    public void Block10_AllRight_Pravsha()
    {
        var block = BuildBlock10();
        var answers = AllSame(block, _ => "right");

        var result = _service.CalculateBlock(block, answers);

        Assert.Equal(0, result.PrimaryValue);
        Assert.Equal("Правша", result.Level);
    }

    [Fact]
    public void Block10_FourLeft_Ambidextr()
    {
        var block = BuildBlock10();
        var answers = AllSame(block, i => i <= 4 ? "left" : "right");

        var result = _service.CalculateBlock(block, answers);

        Assert.Equal(4, result.PrimaryValue);
        Assert.Equal("Амбидекстр", result.Level);
    }

    [Fact]
    public void Block10_AllLeft_ChistiyLevsha()
    {
        var block = BuildBlock10();
        var answers = AllSame(block, _ => "left");

        var result = _service.CalculateBlock(block, answers);

        Assert.Equal(10, result.PrimaryValue);
        Assert.Equal("Чистый левша", result.Level);
    }

    [Fact]
    public void Block10_RussianSynonyms_CountedAsLeft()
    {
        var block = BuildBlock10();
        // Варианты источника подписаны «правая»/«левая»: ключи left/right,
        // тексты — русские. Проверяем, что ответ по тексту варианта принимается.
        var answers = Answers(block.Id,
            ("q01", "левая"), ("q02", "левая"), ("q03", "левая"));
        for (int i = 4; i <= 10; i++) answers.Set($"q{i:00}", "правая");

        var result = _service.CalculateBlock(block, answers);

        Assert.Equal(3, result.PrimaryValue);
        Assert.Equal("Амбидекстр", result.Level);
    }

    // =====================================================================
    //  Блок 11 — FormulaMultiplier (Т = 2 × Σ весов)
    // =====================================================================

    [Fact]
    public void Block11_AllA_CreativeAgeZero()
    {
        var block = BuildBlock11();
        var answers = AllSame(block, _ => "a");

        var result = _service.CalculateBlock(block, answers);

        Assert.Equal(0, result.PrimaryValue);
        Assert.Equal("0", result.Details!["weighted_sum"]);
    }

    [Fact]
    public void Block11_FormulaFromSource_TwoBThreeCThreeDE_AgeFifty()
    {
        // Пример расчёта по формуле источника Т = 2 × (1·б + 3·в + 4·г + 2·д):
        // ответы б,б,в,в,в,г,г,г,д,а → 1+1+3+3+3+4+4+4+2+0 = 25; Т = 25 × 2 = 50 лет.
        var block = BuildBlock11();
        var seq = new[] { "b", "b", "c", "c", "c", "d", "d", "d", "e", "a" };
        var answers = AllSame(block, i => seq[i - 1]);

        var result = _service.CalculateBlock(block, answers);

        Assert.Equal(50, result.PrimaryValue);
        Assert.Equal("25", result.Details!["weighted_sum"]);
        Assert.Equal("2", result.Details["multiplier"]);
        Assert.Equal("2", result.Details["count_b"]);
        Assert.Equal("3", result.Details["count_c"]);
        Assert.Equal("3", result.Details["count_d"]);
        Assert.Equal("1", result.Details["count_e"]);
        Assert.Equal("1", result.Details["count_a"]);
    }

    [Fact]
    public void Block11_AllD_MaxCreativeAgeEighty()
    {
        var block = BuildBlock11();
        var answers = AllSame(block, _ => "d"); // 10 × 4 × 2 = 80

        var result = _service.CalculateBlock(block, answers);

        Assert.Equal(80, result.PrimaryValue);
    }

    // =====================================================================
    //  Блок 12 — OddEvenSum (Т / П)
    // =====================================================================

    [Fact]
    public void Block12_MaxOddMinEven_UnpriznanniyGeniy()
    {
        var block = BuildBlock12();
        var answers = AllSame(block, i => i % 2 == 1 ? "4" : "0");

        var result = _service.CalculateBlock(block, answers);

        Assert.Equal(100, result.Scores["Т"]);
        Assert.Equal(0, result.Scores["П"]);
        Assert.Equal("Непризнанный гений", result.Level);
    }

    [Fact]
    public void Block12_MaxEvenMinOdd_Trudogolik()
    {
        var block = BuildBlock12();
        var answers = AllSame(block, i => i % 2 == 0 ? "4" : "0");

        var result = _service.CalculateBlock(block, answers);

        Assert.Equal(0, result.Scores["Т"]);
        Assert.Equal(100, result.Scores["П"]);
        Assert.Equal("Трудоголик/Карьерист", result.Level);
    }

    [Fact]
    public void Block12_EqualScales_GarmonichnayaLichnost()
    {
        var block = BuildBlock12();
        var answers = AllSame(block, _ => "2"); // Т = П = 50

        var result = _service.CalculateBlock(block, answers);

        Assert.Equal(50, result.Scores["Т"]);
        Assert.Equal(50, result.Scores["П"]);
        Assert.Equal("Гармоничная личность", result.Level);
    }

    [Fact]
    public void Block12_MixedValues_DeterministicAndDetailed()
    {
        var block = BuildBlock12();
        // Нечётные: 3, чётные: 1 → Т = 25·3 = 75, П = 25·1 = 25.
        var answers = AllSame(block, i => i % 2 == 1 ? "3" : "1");

        var first = _service.CalculateBlock(block, answers);
        var second = _service.CalculateBlock(block, answers);

        Assert.Equal(75, first.Scores["Т"]);
        Assert.Equal(25, first.Scores["П"]);
        Assert.Equal(first.Scores, second.Scores); // детерминированность
        Assert.Equal(first.Summary, second.Summary);
        Assert.Equal("Непризнанный гений", first.Level);
        Assert.Equal(50, first.Details!.Count);
    }

    // =====================================================================
    //  Cross-cutting
    // =====================================================================

    [Fact]
    public void CalculateBlock_WrongBlockIdInAnswers_Throws()
    {
        var block = BuildBlock7();
        var answers = new UserAnswers("some_other_block");
        Assert.Throws<InvalidOperationException>(() => _service.CalculateBlock(block, answers));
    }

    [Fact]
    public void GetInterpretation_ReturnsRuleForLastCalculatedResult()
    {
        var block = BuildBlock10();
        var answers = AllSame(block, _ => "left");
        var result = _service.CalculateBlock(block, answers);

        var rule = _service.GetInterpretation(result);

        Assert.NotNull(rule);
        Assert.Equal("Чистый левша", rule!.Level);
    }

    [Fact]
    public void Result_SerializesToSystemTextJson()
    {
        var block = BuildBlock12();
        var answers = AllSame(block, _ => "2");
        var result = _service.CalculateBlock(block, answers);

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        var restored = System.Text.Json.JsonSerializer.Deserialize<BlockResult>(json);

        Assert.NotNull(restored);
        Assert.Equal(result.Scores["Т"], restored!.Scores["Т"]);
        Assert.Equal(result.Level, restored.Level);
    }
}
