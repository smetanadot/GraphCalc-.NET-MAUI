using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DESMOS;

public class GraphParameter : INotifyPropertyChanged
{
    private string _name = "a";
    private double _value = 1.0;
    private double _min = -10.0;
    private double _max = 10.0;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public double Value
    {
        get => _value;
        set
        {
            // Округляем до 2 знаков для красоты
            double rounded = Math.Round(value, 2);
            if (Math.Abs(_value - rounded) > 0.0001)
            {
                _value = rounded;
                OnPropertyChanged();
            }
        }
    }

    public double Min
    {
        get => _min;
        set { _min = value; OnPropertyChanged(); }
    }

    public double Max
    {
        get => _max;
        set { _max = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
