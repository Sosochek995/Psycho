namespace ZharikovApp.Services.Interfaces;

/// <summary>
/// Интерфейс сервиса загрузки блоков методик из Data/Blocks (System.Text.Json).
/// Реализация будет добавлена позже.
/// </summary>
public interface IBlockLoaderService
{
    /// <summary>Загружает все блоки методик в порядке возрастания Order.</summary>
    Task<IReadOnlyList<Models.BlockBase>> LoadAllAsync(CancellationToken cancellationToken = default);
}
