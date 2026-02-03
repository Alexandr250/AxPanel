// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertIfStatementToNullCoalescingExpression
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace AxPanel.UI.ElementStyles;

#pragma warning disable CS8618
public class ButtonStyle : IDisposable
{
    // Группировка констант
    public int DefaultHeight { get; set; } = 60; // 42
    public int DeleteButtonWidth { get; set; } = 50;
    public int SpaceHeight { get; set; } = 1;

    // Использование автосвойств с инициализацией
    public Pen BorderLightPen { get; set; } = new( Color.FromArgb( 30, 30, 30 ) );
    public Pen BorderDarkPen { get; set; } = new( Color.FromArgb( 30, 30, 30 ) );

    public Brush SelectedBrush { get; set; } = new SolidBrush( Color.FromArgb( 80, 80, 80 ) );
    public Brush SelectedAltBrush { get; set; } = new SolidBrush( Color.FromArgb( 190, 80, 80 ) );
    public Brush UnselectedBrush { get; set; } = new SolidBrush( Color.FromArgb( 60, 60, 60 ) );

    // Readonly свойства (если менять их не планируется)
    public Brush DeleteButtonBrush { get; } = new SolidBrush( Color.FromArgb( 150, 140, 140, 140 ) );
    public Pen DeleteButtonBorderPen { get; } = new( Color.FromArgb( 150, 80, 80, 80 ) );

    public Brush MainFontBrush { get; set; } = Brushes.White;
    public Brush AdditionalFontBrush { get; set; } = new SolidBrush( Color.FromArgb( 150, 150, 150 ) );

    public Font MinFont { get; } = new Font( FontFamily.GenericSerif, 6, FontStyle.Regular, GraphicsUnit.Pixel );

    // Очистка ресурсов
    public void Dispose()
    {
        BorderLightPen?.Dispose();
        BorderDarkPen?.Dispose();
        SelectedBrush?.Dispose();
        SelectedAltBrush?.Dispose();
        UnselectedBrush?.Dispose();
        DeleteButtonBrush?.Dispose();
        DeleteButtonBorderPen?.Dispose();
        AdditionalFontBrush?.Dispose();
        MinFont?.Dispose();

        // Brushes.White удалять нельзя, так как это системный ресурс
        GC.SuppressFinalize( this );
    }
}