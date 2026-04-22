using System.Windows;
using System.Windows.Controls;

namespace LiveWallpaper.Pages;

public partial class SliderRow : WpfUserControl
{
    private bool _ready;

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(SliderRow),
            new PropertyMetadata("", (d, _) =>
                ((SliderRow)d).LabelText.Text = ((SliderRow)d).Label));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(SliderRow),
            new PropertyMetadata(-100.0, (d, e) =>
                ((SliderRow)d).TheSlider.Minimum = (double)e.NewValue));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(SliderRow),
            new PropertyMetadata(100.0, (d, e) =>
                ((SliderRow)d).TheSlider.Maximum = (double)e.NewValue));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(SliderRow),
            new PropertyMetadata(0.0, (d, e) =>
                ((SliderRow)d).TheSlider.Value = (double)e.NewValue));

    public event EventHandler? ValueChanged;

    public string Label   { get => (string)GetValue(LabelProperty);  set => SetValue(LabelProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty);set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty);set => SetValue(MaximumProperty, value); }
    public double Value   { get => (double)GetValue(ValueProperty);  set => SetValue(ValueProperty, value); }
    public int    IntValue => (int)Math.Round(TheSlider.Value);

    public SliderRow()
    {
        InitializeComponent();
        TheSlider.Minimum = -100;
        TheSlider.Maximum = 100;
        TheSlider.Value   = 0;
        Loaded += (_, _) =>
        {
            _ready = true;
            UpdateLabel();
        };
    }

    private void Slider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        SetValue(ValueProperty, e.NewValue);
        UpdateLabel();
        if (_ready) ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateLabel()
    {
        if (ValueText != null)
            ValueText.Text = $"{IntValue}";
    }
}
