// GuardVectorRunner: прогон контрольных векторов кодом приложения (ScoringService).
// Аргументы: <путь к Data/Blocks> <путь к control_vectors.json> [выходной json]
// Формат вывода: { "blockId": { "vectorName": { primary, scores, level } , ... }, ... }
using System.Text.Json;
using ZharikovApp.Models;
using ZharikovApp.Services;

var jsonOpts = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
};

string blocksDir = args[0];
string vectorsPath = args[1];
string outPath = args.Length > 2 ? args[2] : "-";

using var vecStream = File.OpenRead(vectorsPath);
using var vecDoc = await JsonDocument.ParseAsync(vecStream);
var vecBlocks = vecDoc.RootElement.GetProperty("blocks");

var scoring = new ScoringService();
var actual = new Dictionary<string, Dictionary<string, object?>>();

foreach (var (blockId, blockEl) in vecBlocks.EnumerateObject())
{
    string file = Path.Combine(blocksDir, $"{blockId}.json");
    if (!File.Exists(file))
        throw new FileNotFoundException($"Блок не найден: {file}");

    await using var fs = File.OpenRead(file);
    var block = JsonSerializer.Deserialize<TestBlock>(fs, jsonOpts)
                ?? throw new InvalidOperationException($"Не удалось прочитать блок {blockId}");

    var perVector = new Dictionary<string, object?>();
    foreach (var vector in blockEl.GetProperty("vectors").EnumerateArray())
    {
        string name = vector.GetProperty("name").GetString()!;
        var answers = new UserAnswers(blockId);
        foreach (var a in vector.GetProperty("answers").EnumerateObject())
            answers.Set(a.Name, a.Value.ToString());

        try
        {
            var result = scoring.CalculateBlock(block, answers);
            perVector[name] = new Dictionary<string, object?>
            {
                ["ok"] = true,
                ["primary"] = result.PrimaryValue,
                ["scores"] = result.Scores,
                ["level"] = result.Level,
            };
        }
        catch (Exception ex)
        {
            perVector[name] = new Dictionary<string, object?>
            {
                ["ok"] = false,
                ["error"] = ex.Message,
            };
        }
    }
    actual[blockId] = perVector;
}

var output = JsonSerializer.Serialize(actual, new JsonSerializerOptions { WriteIndented = true });
if (outPath == "-")
    Console.WriteLine(output);
else
    File.WriteAllText(outPath, output);
