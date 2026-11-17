using System.Windows;
using ZharikovApp.ViewModels;

namespace ZharikovApp.Views;

/// <summary>
/// Приветственное окно. Показывается до главного окна.
/// Результат выбора пользователя возвращается через <see cref="ChosenAction"/>
/// (коды — в <see cref="WelcomeViewModel"/>).
/// </summary>
public partial class WelcomeWindow : Window
{
    public WelcomeWindow()
    {
        InitializeComponent();

        var vm = (WelcomeViewModel)DataContext;
        vm.Close = action =>
        {
            ChosenAction = action;
            Close();
        };
    }

    /// <summary>ViewModel окна (настраивается из App перед показом).</summary>
    public WelcomeViewModel ViewModel => (WelcomeViewModel)DataContext;

    /// <summary>Действие, выбранное пользователем (0 — отмена/закрытие).</summary>
    public int ChosenAction { get; private set; } = WelcomeViewModel.CancelAction;
}
