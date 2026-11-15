using System.Text.Json;
using Xunit;
using ZharikovApp.Models;
using ZharikovApp.Services;

namespace ZharikovApp.Tests;

/// <summary>
/// Интеграционные проверки импортированных JSON-блоков (Data/Blocks/*.json):
/// десериализация моделями приложения, полнота источника и контрольные расчёты
/// ScoringService на реальных данных.
/// </summary>
public class ImportedBlocksTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly string _blocksDir;
    private readonly Dictionary<string, TestBlock> _blocks = new();

    public ImportedBlocksTests()
    {
        // tests/ZharikovApp.Tests/bin/<cfg>/<tfm>/ → корень репозитория
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ZharikovApp.csproj")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        _blocksDir = Path.Combine(dir!.FullName, "Data", "Blocks");
        Assert.True(Directory.Exists(_blocksDir), $"Нет папки {_blocksDir}");

        foreach (var file in Directory.GetFiles(_blocksDir, "*.json"))
        {
            var block = JsonSerializer.Deserialize<TestBlock>(File.ReadAllText(file), JsonOptions);
            Assert.NotNull(block);
            if (!block!.IsValid(out var errors))
                Assert.Fail($"{Path.GetFileName(file)}: {string.Join(" | ", errors)}");
            _blocks[block.Id] = block;
        }
    }

    public void Dispose() { }

    // ------------------------------------------------------------------
    //  Структура
    // ------------------------------------------------------------------

    [Fact]
    public void All_12_Blocks_Are_Present_And_Ordered()
    {
        Assert.Equal(12, _blocks.Count);
        Assert.Equal(Enumerable.Range(1, 12), _blocks.Values.OrderBy(b => b.Order).Select(b => b.Order));
        foreach (var b in _blocks.Values)
        {
            Assert.False(string.IsNullOrWhiteSpace(b.Instruction), $"{b.Id}: пустая инструкция");
            Assert.NotEmpty(b.Questions);
            Assert.NotEmpty(b.Scoring.Interpretations);
        }
    }

    [Theory]
    [InlineData("block_01_leadership", 50)]
    [InlineData("block_02_love", 20)]
    [InlineData("block_03_artist_thinker", 10)]
    [InlineData("block_04_extraversion", 9)]
    [InlineData("block_05_biomarkers", 4)]
    [InlineData("block_06_palmer", 6)]
    [InlineData("block_07_leader_admin", 20)]
    [InlineData("block_08_graphology", 9)]
    [InlineData("block_09_signature", 8)]
    [InlineData("block_10_left_hand", 10)]
    [InlineData("block_11_creative_age", 10)]
    [InlineData("block_12_creative_potential", 50)]
    public void Block_Has_Expected_Question_Count(string id, int count)
    {
        Assert.Equal(count, _blocks[id].Questions.Count);
    }

    [Fact]
    public void Block1_Key_Covers_All_50_Questions()
    {
        var keys = _blocks["block_01_leadership"].Scoring.Keys!;
        Assert.Equal(50, keys.Count);
        Assert.All(keys.Values, v => Assert.Contains(GetString(v), new[] { "a", "b" }));
    }

    [Fact]
    public void Block5_Has_All_16_Combo_Interpretations()
    {
        var rules = _blocks["block_05_biomarkers"].Scoring.Interpretations;
        Assert.Equal(16, rules.Count);
        Assert.All(rules, r => Assert.Matches("^[ЛП]{4}$", r.Value!));
    }

    [Fact]
    public void Block6_Palmer_Matrix_Has_Six_Rows_And_ImageKeys()
    {
        var block = _blocks["block_06_palmer"];
        Assert.Equal(6, block.Scoring.Keys!.Count);
        Assert.All(block.Questions, q =>
        {
            Assert.False(string.IsNullOrWhiteSpace(q.ImageKey));
            Assert.Equal(QuestionType.ImageSingleChoice, q.Type);
        });
    }

    // ------------------------------------------------------------------
    //  Контрольные расчёты ScoringService на импортированных JSON
    // ------------------------------------------------------------------

    [Fact]
    public void Block1_AllKeyAnswers_Gives_Dictator_Level()
    {
        var block = _blocks["block_01_leadership"];
        var answers = new UserAnswers(block.Id);
        foreach (var q in block.Questions)
            answers.Set(q.Id, GetString(block.Scoring.Keys![q.Id]));
        var result = new ScoringService().CalculateBlock(block, answers);
        Assert.Equal(50, result.PrimaryValue);
        Assert.Equal("Склонность к диктату", result.Level);
    }

    [Fact]
    public void Block2_AllCorrectAnswers_Gives_100_And_Excellent()
    {
        var block = _blocks["block_02_love"];
        var answers = new UserAnswers(block.Id);
        foreach (var q in block.Questions)
            answers.Set(q.Id, GetString(block.Scoring.Keys![q.Id]) == "yes" ? "да" : "нет");
        var result = new ScoringService().CalculateBlock(block, answers);
        Assert.Equal(100, result.PrimaryValue);
        Assert.Equal("Отлично", result.Level);
    }

    [Fact]
    public void Block3_HemisphereSum_Left_Type()
    {
        var block = _blocks["block_03_artist_thinker"];
        var answers = new UserAnswers(block.Id);
        foreach (var q in block.Questions)
            answers.Set(q.Id, q.Tags.Contains("left_hemisphere") ? "10" : "0");
        var result = new ScoringService().CalculateBlock(block, answers);
        Assert.Equal(50, result.Scores["Л"]);
        Assert.Equal(0, result.Scores["П"]);
        Assert.Contains("Мыслитель", result.Level!);
    }

    [Fact]
    public void Block5_SequenceCombo_LPLP_Is_Strongest_Type()
    {
        var block = _blocks["block_05_biomarkers"];
        var answers = new UserAnswers(block.Id);
        string[] sides = { "l", "r", "l", "p" }; // Л, П, Л, П
        for (int i = 0; i < block.Questions.Count; i++)
            answers.Set(block.Questions[i].Id, sides[i]);
        var result = new ScoringService().CalculateBlock(block, answers);
        Assert.Equal("ЛПЛП", result.Level);
        Assert.Contains("самый сильный тип", result.Summary!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Block6_Palmer_MaxMatrix_Gives_42_Flexible_Profile()
    {
        var block = _blocks["block_06_palmer"];
        var answers = new UserAnswers(block.Id);
        foreach (var q in block.Questions)
        {
            var row = block.Scoring.Keys![q.Id];
            var best = EnumerateOptionPoints(row)
                .OrderByDescending(p => p.Value).First();
            answers.Set(q.Id, best.Key);
        }
        var result = new ScoringService().CalculateBlock(block, answers);
        Assert.Equal(42, result.PrimaryValue);
        Assert.Equal("Гибкий профиль общения", result.Level);
    }

    [Fact]
    public void Block7_Leader_Administrator_Scales_Counted_Separately()
    {
        var block = _blocks["block_07_leader_admin"];
        var answers = new UserAnswers(block.Id);
        foreach (var q in block.Questions)
            answers.Set(q.Id, q.Tags.Contains("leader") ? "10" : "2");
        var result = new ScoringService().CalculateBlock(block, answers);
        Assert.Equal(100, result.Scores["Лидер"]);
        Assert.Equal(20, result.Scores["Администратор"]);
        Assert.Contains("Демократ", result.Level!);
    }

    [Fact]
    public void Block8_QualitativeProfile_Builds_Text_Profile()
    {
        var block = _blocks["block_08_graphology"];
        var answers = new UserAnswers(block.Id);
        foreach (var q in block.Questions)
            answers.Set(q.Id, q.Options[0].Key);
        var result = new ScoringService().CalculateBlock(block, answers);
        Assert.Null(result.PrimaryValue);
        Assert.Contains("Сдержанность", result.Summary!); // вариант «Мелкий» параметра «Размер букв»
        Assert.Equal(9, result.Details.Count(d => d.Key.StartsWith("p")));
    }

    [Fact]
    public void Block10_CountLeft_Seven_Left_Hands_Is_Vyrazhenny_Levsha()
    {
        var block = _blocks["block_10_left_hand"];
        var answers = new UserAnswers(block.Id);
        for (int i = 0; i < block.Questions.Count; i++)
            answers.Set(block.Questions[i].Id, i < 7 ? "левая" : "правая");
        var result = new ScoringService().CalculateBlock(block, answers);
        Assert.Equal(7, result.PrimaryValue);
        Assert.Equal("Выраженный левша", result.Level);
    }

    [Fact]
    public void Block11_Formula_TwoTimesWeightedSum_On_Imported_Json()
    {
        var block = _blocks["block_11_creative_age"];
        var answers = new UserAnswers(block.Id);
        // 2×б + 3×в + 3×г + 2×д = 2·1 + 3·3 + 3·4 + 2·2 = 29 → Т = 58
        string[] plan = { "b", "b", "c", "c", "c", "d", "d", "d", "e", "e" };
        for (int i = 0; i < block.Questions.Count; i++)
            answers.Set(block.Questions[i].Id, plan[i]);
        var result = new ScoringService().CalculateBlock(block, answers);
        Assert.Equal(58, result.PrimaryValue);
    }

    [Fact]
    public void Block12_T_and_P_Scales_Counted_Separately()
    {
        var block = _blocks["block_12_creative_potential"];
        var answers = new UserAnswers(block.Id);
        foreach (var q in block.Questions)
            answers.Set(q.Id, q.Tags.Contains("T") ? "4" : "1");
        var result = new ScoringService().CalculateBlock(block, answers);
        Assert.Equal(100, result.Scores["Т"]);
        Assert.Equal(25, result.Scores["П"]);
        Assert.Contains("Непризнанный гений", result.Level!);
    }

    // ------------------------------------------------------------------
    //  Утилиты
    // ------------------------------------------------------------------

    private static string GetString(object element) => element switch
    {
        JsonElement je => je.GetString() ?? string.Empty,
        string s => s,
        _ => element.ToString() ?? string.Empty,
    };

    private static IEnumerable<KeyValuePair<string, int>> EnumerateOptionPoints(object row)
    {
        if (row is JsonElement je && je.ValueKind == JsonValueKind.Object)
            foreach (var p in je.EnumerateObject())
                yield return new KeyValuePair<string, int>(p.Name, p.Value.GetInt32());
    }
}
