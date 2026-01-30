namespace AxPanel.UI.ElementStyles;

#pragma warning disable CS8618
public class ContainerStyle : IDisposable
{
    // Свойства с автоматической инициализацией
    public Pen BorderLightPen { get; set; } = Pens.Black;
    public Pen BorderDarkPen { get; set; } = Pens.Black;

    public int ScrollValueIncrement { get; set; } = 10;
    public int HeaderHeight { get; set; } = 26;

    public Color BackColor { get; set; } = Color.FromArgb( 60, 60, 60 );
    public Font Font { get; set; } = SystemFonts.DefaultFont;

    // Ресурсы, требующие Dispose
    public Brush HeaderBrush { get; set; } = new SolidBrush( Color.FromArgb( 40, 40, 40 ) );
    public Brush ForeBrush { get; set; } = Brushes.White;

    public void Dispose()
    {
        DisposeResource( HeaderBrush );
        GC.SuppressFinalize( this );
    }

    private void DisposeResource( object resource )
    {
        // Вспомогательный метод для безопасной очистки только наших ресурсов
        if ( resource is IDisposable disposable && !IsSystemResource( resource ) )
        {
            disposable.Dispose();
        }
    }

    private bool IsSystemResource( object resource )
    {
        // Простая проверка, чтобы не убить статические кисти/шрифты
        return ReferenceEquals( resource, Brushes.White ) ||
               ReferenceEquals( resource, Pens.Black ) ||
               ReferenceEquals( resource, SystemFonts.DefaultFont );
    }
}