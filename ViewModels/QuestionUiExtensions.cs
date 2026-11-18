using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ZharikovApp.ViewModels;

/// <summary>
/// Дополнения к ViewModel вопроса: кнопочный ввод шкалы, кириллические литеры,
/// изображения с тёмной заглушкой. Логика подсчёта не изменяется.
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

    /// <summary>У вопроса есть иллюстрация.</summary>
    public bool HasImage => !string.IsNullOrWhiteSpace(Question.ImageKey);

    /// <summary>Подпись к иллюстрации из карты изображений.</summary>
    public string ImageCaption => ImageCatalog.CaptionFor(Question.ImageKey);

    /// <summary>Имя файла, который нужно положить в Assets/Images.</summary>
    public string ImageFileName => ImageCatalog.FileFor(Question.ImageKey);

    /// <summary>Загруженная картинка либо null.</summary>
    public ImageSource? ImagePicture => ImageCatalog.Load(Question.ImageKey);

    /// <summary>Показывать тёмную заглушку вместо отсутствующей картинки.</summary>
    public bool ShowImagePlaceholder => HasImage && ImagePicture is null;

    private ObservableCollection<ScaleChoiceViewModel> BuildScaleChoices()
    {
        var list = new ObservableCollection<ScaleChoiceViewModel>();
        for (int v = ScaleMin; v <= ScaleMax; v++) list.Add(new ScaleChoiceViewModel(v, this));
        return list;
    }

    /// <summary>
    /// Явно фиксирует ответ по шкале. Запись происходит всегда,
    /// поэтому нажатие «0» отличается от состояния «ответа нет».
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

/// <summary>
/// Карта иллюстраций. Читается из Assets/Images/image_map.json рядом с exe
/// или в %APPDATA%/ZharikovApp — замена картинок не требует перекомпиляции.
/// </summary>
internal static class ImageCatalog
{
    private static readonly Dictionary<string, string[]> Defaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["handwriting_sample"] = new[] { "Образец почерка", "handwriting_sample.png" },
        ["signature_sample"] = new[] { "Образец подписи", "signature_sample.png" },
        ["left_hand_sample"] = new[] { "Схема ведущей руки", "left_hand_sample.png" },
        ["palm_sample"] = new[] { "Схема ладони", "palm_sample.png" },
        ["biomarkers_sample"] = new[] { "Схема биомаркеров", "biomarkers_sample.png" }
    };

    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, JsonElement>? _map;
    private static bool _mapTried;

    private static IEnumerable<string> Roots()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "Assets", "Images");
        var exe = Path.GetDirectoryName(Environment.ProcessPath);
        if (!string.IsNullOrEmpty(exe)) yield return Path.Combine(exe, "Assets", "Images");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                  "ZharikovApp", "Assets", "Images");
    }

    private static void EnsureMap()
    {
        if (_mapTried) return;
        _mapTried = true;
        foreach (var root in Roots())
        {
            var file = Path.Combine(root, "image_map.json");
            if (!File.Exists(file)) continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                if (doc.RootElement.ValueKind != JsonValueKind.Object) continue;
                var dict = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in doc.RootElement.EnumerateObject()) dict[p.Name] = p.Value.Clone();
                _map = dict;
                return;
            }
            catch { /* повреждённая карта не должна ломать приложение */ }
        }
    }

    private static string? Field(string? key, string name)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        EnsureMap();
        if (_map is not null && _map.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.Object &&
            el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String)
            return v.GetString();
        return null;
    }

    public static string CaptionFor(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        return Field(key, "caption")
               ?? (Defaults.TryGetValue(key, out var d) ? d[0] : "Иллюстрация к вопросу");
    }

    public static string FileFor(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        return Field(key, "file")
               ?? (Defaults.TryGetValue(key, out var d) ? d[1] : key + ".png");
    }

    public static ImageSource? Load(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        if (Cache.TryGetValue(key, out var cached)) return cached;

        ImageSource? result = null;
        var name = FileFor(key);
        foreach (var root in Roots())
        {
            foreach (var candidate in new[] { name, key + ".png", key + ".jpg", key + ".jpeg" })
            {
                var path = Path.Combine(root, candidate);
                if (!File.Exists(path)) continue;
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bmp.UriSource = new Uri(path, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();
                    result = bmp;
                }
                catch { result = null; }
                break;
            }
            if (result is not null) break;
        }

        Cache[key] = result;
        return result;
    }
}
