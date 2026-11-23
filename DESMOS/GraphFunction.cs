using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Microsoft.Maui.Graphics;

namespace DESMOS;

public class GraphFunction : INotifyPropertyChanged
{
    private string _formula = "";
    private string _colorHex = "#0000FF";

    public string Formula
    {
        get => _formula;
        set
        {
            if (_formula != value)
            {
                _formula = value;
                OnPropertyChanged();
            }
        }
    }

    public string ColorHex
    {
        get => _colorHex;
        set
        {
            if (_colorHex != value)
            {
                _colorHex = value;
                OnPropertyChanged();
                // Сообщаем UI, что свойство LineColor тоже изменилось!
                OnPropertyChanged(nameof(LineColor));
            }
        }
    }

    [JsonIgnore]
    public Color LineColor
    {
        get => Color.FromArgb(_colorHex);
        set
        {
            // ToArgbHex(true) гарантирует точный формат #AARRGGBB
            ColorHex = value.ToArgbHex(true);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
