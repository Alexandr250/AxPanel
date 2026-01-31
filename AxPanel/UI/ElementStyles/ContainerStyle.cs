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
        // Очищаем все графические ресурсы
        DisposeResource( BorderLightPen );
        DisposeResource( BorderDarkPen );
        DisposeResource( HeaderBrush );
        DisposeResource( ForeBrush );

        // Шрифты тоже нужно очищать, если они не системные
        if ( !ReferenceEquals( Font, SystemFonts.DefaultFont ) )
        {
            Font?.Dispose();
        }

        GC.SuppressFinalize( this );
    }

    private void DisposeResource( IDisposable resource )
    {
        // Если ресурс не null и не является системным статиком — удаляем
        if ( resource != null && !IsSystemResource( resource ) )
        {
            resource.Dispose();
        }
    }

    private bool IsSystemResource( object resource )
    {
        return ReferenceEquals( resource, Brushes.White ) ||
               ReferenceEquals( resource, Pens.Black ) ||
               ReferenceEquals( resource, SystemFonts.DefaultFont );
    }
}