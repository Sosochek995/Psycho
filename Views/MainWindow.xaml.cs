using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZharikovApp.ViewModels;

namespace ZharikovApp.Views;

/// <summary>
/// Главное окно. Загрузка блоков через LoadedCommand и клавиатурная навигация:
/// цифры — вариант или значение шкалы, стрелки — перемещение,
/// Enter — следующий вопрос, Ctrl+Enter — завершить блок.
/// Логика подсчёта не затрагивается: вызываются те же команды, что и кнопки.
/// </summary>
public partial class MainWindow : Window
{
    private int _pendingScale = -1;
    private DateTime _pendingAt = DateTime.MinValue;
    private bool _loaded;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>ViewModel окна. DataContext задаётся в XAML.</summary>
    public MainViewModel? ViewModel => DataContext as MainViewModel;

    /// <summary>
    /// Загрузка 12 блоков и восстановление сессии.
    /// Без этого вызова список методик остаётся пустым.
    /// </summary>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;

        if (DataContext is MainViewModel vm)
            await vm.LoadedCommand.ExecuteAsync(null);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var vm = ViewModel;
        if (vm is null || !vm.IsQuestionsVisible) return;

        var q = vm.CurrentQuestion as QuestionViewModel;
        if (q is null) return;

        int digit = DigitOf(e.Key);
        if (digit >= 0)
        {
            if (q.IsScale) HandleScaleDigit(q, digit);
            else HandleChoiceDigit(q, digit);
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.Right:
            case Key.Down:
                // Фокус в списке блоков — стрелки должны листать список.
                if (Keyboard.FocusedElement is ListBoxItem) break;
                Move(q, +1);
                e.Handled = true;
                break;

            case Key.Left:
            case Key.Up:
                if (Keyboard.FocusedElement is ListBoxItem) break;
                Move(q, -1);
                e.Handled = true;
                break;

            case Key.Enter:
                // Фокус на кнопке варианта — Enter нажимает её, а не листает дальше.
                if (Keyboard.FocusedElement is Button) break;

                if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                {
                    _pendingScale = -1;
                    if (vm.CompleteBlockCommand.CanExecute(null))
                        vm.CompleteBlockCommand.Execute(null);
                    e.Handled = true;
                }
                else if (q.IsAnswered)
                {
                    _pendingScale = -1;
                    if (vm.GoNextCommand.CanExecute(null))
                        vm.GoNextCommand.Execute(null);
                    else
                        vm.StatusMessage = "Это последний вопрос блока — нажмите Ctrl+Enter или «Завершить блок».";
                    e.Handled = true;
                }
                break;
        }
    }

    /// <summary>Цифра клавиши, включая цифровой блок. Иначе -1.</summary>
    private static int DigitOf(Key key) => key switch
    {
        Key.D0 or Key.NumPad0 => 0,
        Key.D1 or Key.NumPad1 => 1,
        Key.D2 or Key.NumPad2 => 2,
        Key.D3 or Key.NumPad3 => 3,
        Key.D4 or Key.NumPad4 => 4,
        Key.D5 or Key.NumPad5 => 5,
        Key.D6 or Key.NumPad6 => 6,
        Key.D7 or Key.NumPad7 => 7,
        Key.D8 or Key.NumPad8 => 8,
        Key.D9 or Key.NumPad9 => 9,
        _ => -1
    };

    /// <summary>Цифры 1…9 выбирают первый…девятый вариант, 0 — десятый.</summary>
    private static void HandleChoiceDigit(QuestionViewModel q, int digit)
    {
        int index = digit == 0 ? 9 : digit - 1;
        if (index >= 0 && index < q.Options.Count)
            q.SelectOption(q.Options[index].Key);
    }

    /// <summary>
    /// Значение шкалы цифрой. Для шкал до 10 поддерживается двойное нажатие:
    /// «1» затем «0» в пределах 0,9 с даёт 10.
    /// </summary>
    private void HandleScaleDigit(QuestionViewModel q, int digit)
    {
        var now = DateTime.UtcNow;
        bool fresh = _pendingScale >= 0 && (now - _pendingAt).TotalMilliseconds < 900;

        if (fresh)
        {
            int combined = _pendingScale * 10 + digit;
            if (combined >= q.ScaleMin && combined <= q.ScaleMax)
            {
                q.SelectScale(combined);
                _pendingScale = -1;
                return;
            }
        }

        if (digit >= q.ScaleMin && digit <= q.ScaleMax)
            q.SelectScale(digit);

        _pendingScale = digit > 0 && digit * 10 <= q.ScaleMax ? digit : -1;
        _pendingAt = now;
    }

    /// <summary>Сдвиг выбора на один шаг. Без ответа — начинаем с края шкалы/списка.</summary>
    private void Move(QuestionViewModel q, int delta)
    {
        _pendingScale = -1;

        if (q.IsScale)
        {
            int next = q.HasExplicitScaleAnswer
                ? q.ScaleValue + delta
                : delta > 0 ? q.ScaleMin : q.ScaleMax;
            q.SelectScale(Math.Clamp(next, q.ScaleMin, q.ScaleMax));
            return;
        }

        int count = q.Options.Count;
        if (count == 0) return;

        int current = -1;
        for (int i = 0; i < count; i++)
            if (q.Options[i].IsSelected) { current = i; break; }

        int index = current < 0
            ? delta > 0 ? 0 : count - 1
            : Math.Clamp(current + delta, 0, count - 1);

        q.SelectOption(q.Options[index].Key);
    }
}
