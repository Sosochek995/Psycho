using CommunityToolkit.Mvvm.ComponentModel;
using ZharikovApp.Models;

namespace ZharikovApp.ViewModels;

/// <summary>
/// ViewModel главного окна каркаса.
/// Логика прохождения методик будет добавлена позже.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string title = "ZharikovApp — Методики Жарикова и Крушельницкого";

    [ObservableProperty]
    private BlockBase? currentBlock;

    public bool IsBlockSelected => CurrentBlock is not null;

    partial void OnCurrentBlockChanged(BlockBase? value)
    {
        OnPropertyChanged(nameof(IsBlockSelected));
    }
}
