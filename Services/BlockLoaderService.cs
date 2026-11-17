using System.IO;
using System.Text.Json;
using ZharikovApp.Helpers;
using ZharikovApp.Models;
using ZharikovApp.Services.Interfaces;

namespace ZharikovApp.Services;

/// <summary>
/// Загрузка блоков методик из JSON-файлов Data/Blocks/*.json (System.Text.Json).
/// Реализация <see cref="IBlockLoaderService"/>. Не зависит от UI.
/// </summary>
public class BlockLoaderService : IBlockLoaderService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly string _blocksDirectory;

    /// <param name="blocksDirectory">Папка с JSON-блоками; по умолчанию Data/Blocks рядом с exe.</param>
    public BlockLoaderService(string? blocksDirectory = null)
    {
        _blocksDirectory = blocksDirectory ?? PathHelper.BlocksDirectory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BlockBase>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        var blocks = await LoadTestBlocksAsync(cancellationToken).ConfigureAwait(false);
        return blocks;
    }

    /// <summary>Загружает все блоки как полные <see cref="TestBlock"/> в порядке Order.</summary>
    public async Task<IReadOnlyList<TestBlock>> LoadTestBlocksAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_blocksDirectory))
            throw new DirectoryNotFoundException($"Папка с блоками не найдена: {_blocksDirectory}");

        var files = Directory.GetFiles(_blocksDirectory, "*.json", SearchOption.TopDirectoryOnly);
        var blocks = new List<TestBlock>(files.Length);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var stream = File.OpenRead(file);
                var block = await JsonSerializer.DeserializeAsync<TestBlock>(stream, JsonOptions, cancellationToken)
                                .ConfigureAwait(false);
                if (block is not null)
                    blocks.Add(block);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Не удалось разобрать JSON блока '{Path.GetFileName(file)}': {ex.Message}", ex);
            }
        }

        return blocks
            .OrderBy(b => b.Order)
            .ThenBy(b => b.Id, StringComparer.Ordinal)
            .ToList();
    }
}
