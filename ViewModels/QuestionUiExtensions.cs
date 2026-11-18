using System;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ZharikovApp.ViewModels;

/// <summary>
/// Дополнения к ViewModel вопроса для кнопочного ввода.
/// Отдельный файл partial-класса: существующая логика подсчёта не изменяется.
/// </summary>
public partial class QuestionViewModel
{
    private const string RuLetters = "абвгдежзиклмнопрстуфхцчшщыэюя";

    private ObservableCollection<ScaleChoiceViewModel>? _scaleChoices;
    private IRelayCommand<int>? _selectScaleCommand;

    /// <summary>Значения шкалы как отдельные кнопки: 0…10, 0…4 и т.д.</summary>
    public ObservableCollection<ScaleChoiceViewModel> ScaleChoices => _scaleChoices ??= BuildScaleChoices();

    /// <summary>Команда явного выбора значения шкалы, в том числе нуля.</summary>
    public IRelayCommand<int> SelectScaleCommand => _selectScaleCommand ??= new RelayCommand<int>(SelectScale);

    /// <summary>Подпись под шкалой: выбранное значение либо «не выбрано».</summary>
    public string ScaleAnswerText => HasExplicitScaleAnswer
        ? ScaleValue.ToString(CultureInfo.InvariantCulture)
        : "не выбрано";

    private ObservableCollection<ScaleChoiceViewModel> BuildScaleChoices()
    {
        var list = new ObservableCollection<ScaleChoiceViewModel>();
        for (int v = ScaleMin; v <= ScaleMax; v++)
            list.Add(new ScaleChoiceViewModel(v, this));
        return list;
    }

    /// <summary>
    /// Явно фиксирует ответ по шкале. В отличие от ползунка, запись происходит
    /// всегда — поэтому нажатие «0» отличается от состояния «ответа нет».
    /// </summary>
    public void SelectScale(int value)
    {
        int clamped = Math.Clamp(value, ScaleMin, ScaleMax);
        _state.Answers[Question.Id] = clamped.ToString(CultureInfo.InvariantCulture);

        _suppressSave = true;
        ScaleValue = clamped;
        _suppressSave = false;

        foreach (var c in ScaleChoices) c.RefreshSelection();
        OnPropertyChanged(nameof(HasExplicitScaleAnswer));
        OnPropertyChanged(nameof(IsAnswered));
        OnPropertyChanged(nameof(ScaleAnswerText));
        MarkDirtyAndNotify();
    }

    internal static string LetterFor(int index) =>
        index >= 0 && index < RuLetters.Length
            ? RuLetters[index].ToString()
            : (index + 1).ToString(CultureInfo.InvariantCulture);
}

/// <summary>Одна кнопка значения шкалы.</summary>
public class ScaleChoiceViewModel : ObservableObject
{
    private readonly QuestionViewModel _owner;

    public ScaleChoiceViewModel(int value, QuestionViewModel owner)
    {
        Value = value;
        _owner = owner;
        SelectCommand = new RelayCommand(() => _owner.SelectScale(Value));
    }

    public int Value { get; }
    public IRelayCommand SelectCommand { get; }

    /// <summary>Подсвечивается только явно выбранный ответ, а не дефолтный ноль.</summary>
    public bool IsSelected => _owner.HasExplicitScaleAnswer && _owner.ScaleValue == Value;

    public void RefreshSelection() => OnPropertyChanged(nameof(IsSelected));
}

public partial class AnswerOptionViewModel
{
    /// <summary>Кириллическая литера варианта: а, б, в, г, д…</summary>
    public string Letter => QuestionViewModel.LetterFor(_owner.Options.IndexOf(this));
}
