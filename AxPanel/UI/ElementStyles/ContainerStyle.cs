using System.Text.Json.Serialization;

namespace AxPanel.UI.ElementStyles;

#pragma warning disable CS8618
public class ContainerStyle : IDisposable
{
    // --- Обычные свойства (сериализуются автоматически) ---
    public int ScrollValueIncrement { get; set; } = 10;
    public int HeaderHeight { get; set; } = 26;
    public int ButtonSize { get; set; } = 16;
    public int ButtonMargin { get; set; } = 3;

    // --- Оригинальные ресурсы (игнорируем в JSON) ---
    [JsonIgnore] public Pen BorderLightPen { get; set; } = Pens.Black;
    [JsonIgnore] public Pen BorderDarkPen { get; set; } = Pens.Black;
    [JsonIgnore] public Brush ButtonSelectedBrush { get; set; } = Brushes.Crimson;
    [JsonIgnore] public Color BackColor { get; set; } = Color.FromArgb( 60, 60, 60 );
    [JsonIgnore] public Font Font { get; set; } = SystemFonts.DefaultFont;
    [JsonIgnore] public Brush HeaderBrush { get; set; } = new SolidBrush( Color.FromArgb( 40, 40, 40 ) );
    [JsonIgnore] public Brush ForeBrush { get; set; } = Brushes.White;

    // --- Обертки для JSON ---

    [JsonPropertyName( "BorderLightColor" )]
    public string BorderLightPenHex
    {
        get => ColorTranslator.ToHtml( BorderLightPen.Color );
        set { DisposeResource( BorderLightPen ); BorderLightPen = new Pen( ColorTranslator.FromHtml( value ) ); }
    }

    [JsonPropertyName( "BorderDarkColor" )]
    public string BorderDarkPenHex
    {
        get => ColorTranslator.ToHtml( BorderDarkPen.Color );
        set { DisposeResource( BorderDarkPen ); BorderDarkPen = new Pen( ColorTranslator.FromHtml( value ) ); }
    }

    [JsonPropertyName( "ButtonSelectedColor" )]
    public string ButtonSelectedBrushHex
    {
        get => ColorTranslator.ToHtml( ( ( SolidBrush )ButtonSelectedBrush ).Color );
        set { DisposeResource( ButtonSelectedBrush ); ButtonSelectedBrush = new SolidBrush( ColorTranslator.FromHtml( value ) ); }
    }

    [JsonPropertyName( "BackColor" )]
    public string BackColorHex
    {
        get => ColorTranslator.ToHtml( BackColor );
        set => BackColor = ColorTranslator.FromHtml( value );
    }

    [JsonPropertyName( "HeaderColor" )]
    public string HeaderBrushHex
    {
        get => ColorTranslator.ToHtml( ( ( SolidBrush )HeaderBrush ).Color );
        set { DisposeResource( HeaderBrush ); HeaderBrush = new SolidBrush( ColorTranslator.FromHtml( value ) ); }
    }

    [JsonPropertyName( "ForeColor" )]
    public string ForeBrushHex
    {
        get => ColorTranslator.ToHtml( ( ( SolidBrush )ForeBrush ).Color );
        set { DisposeResource( ForeBrush ); ForeBrush = new SolidBrush( ColorTranslator.FromHtml( value ) ); }
    }

    [JsonPropertyName( "Font" )]
    public string FontString
    {
        get => $"{Font.FontFamily.Name}, {Font.Size}, {Font.Style}";
        set
        {
            var parts = value.Split( ',' );
            if ( !ReferenceEquals( Font, SystemFonts.DefaultFont ) ) Font?.Dispose();
            Font = new Font( parts[ 0 ].Trim(), float.Parse( parts[ 1 ].Trim() ), ( FontStyle )Enum.Parse( typeof( FontStyle ), parts[ 2 ].Trim() ) );
        }
    }

    public int PhantomRadius { get; set; } = 6;

    [JsonIgnore] public SolidBrush PhantomBgBrush { get; set; } = new SolidBrush( Color.FromArgb( 50, 0, 0, 0 ) );
    [JsonIgnore] public Pen PhantomShadowPen { get; set; } = new Pen( Color.FromArgb( 120, 0, 0, 0 ), 1.5f );
    [JsonIgnore] public Pen PhantomDashPen { get; set; } = new Pen( Color.FromArgb( 90, Color.Black ), 1 );

    [JsonPropertyName( "PhantomBgColor" )]
    public string PhantomBgHex
    {
        get => ColorTranslator.ToHtml( PhantomBgBrush.Color );
        set { PhantomBgBrush?.Dispose(); PhantomBgBrush = new SolidBrush( ColorTranslator.FromHtml( value ) ); }
    }

    [JsonPropertyName( "PhantomShadowColor" )]
    public string PhantomShadowHex
    {
        get => ColorTranslator.ToHtml( PhantomShadowPen.Color );
        set { PhantomShadowPen?.Dispose(); PhantomShadowPen = new Pen( ColorTranslator.FromHtml( value ), 1.5f ); }
    }

    [JsonPropertyName( "PhantomDashColor" )]
    public string PhantomDashHex
    {
        get => ColorTranslator.ToHtml( PhantomDashPen.Color );
        set { PhantomDashPen?.Dispose(); PhantomDashPen = new Pen( ColorTranslator.FromHtml( value ), 1 ); }
    }

    [JsonIgnore] public SolidBrush PhantomTextBrush { get; set; } = new SolidBrush( Color.FromArgb( 110, Color.Gray ) );
    [JsonIgnore] public Font PhantomFont { get; set; } = new Font( "Segoe UI", 7.5f );

    [JsonPropertyName( "PhantomTextColor" )]
    public string PhantomTextHex
    {
        get => ColorTranslator.ToHtml( PhantomTextBrush.Color );
        set { PhantomTextBrush?.Dispose(); PhantomTextBrush = new SolidBrush( ColorTranslator.FromHtml( value ) ); }
    }

    [JsonPropertyName( "PhantomFont" )]
    public string PhantomFontString
    {
        get => $"{PhantomFont.Name}, {PhantomFont.Size}, {PhantomFont.Style}";
        set
        {
            if ( string.IsNullOrWhiteSpace( value ) ) return;
            try
            {
                var parts = value.Split( ',' );
                if ( parts.Length < 2 ) return;

                string name = parts[ 0 ].Trim();

                // КЛЮЧЕВОЙ МОМЕНТ: InvariantCulture для обработки точки в "7.5"
                if ( !float.TryParse( parts[ 1 ].Trim(), System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out float size ) )
                {
                    size = 7.5f; // Дефолт, если строка пустая или кривая
                }

                if ( size <= 0 ) size = 7.5f; // Защита от "Parameter is not valid"

                FontStyle style = FontStyle.Regular;
                if ( parts.Length > 2 && Enum.TryParse( parts[ 2 ].Trim(), true, out FontStyle parsedStyle ) )
                {
                    style = parsedStyle;
                }

                var oldFont = PhantomFont;
                PhantomFont = new Font( name, size, style );

                // Удаляем старый, только если он не системный
                if ( oldFont != null && !ReferenceEquals( oldFont, SystemFonts.DefaultFont ) )
                    oldFont.Dispose();
            }
            catch
            {
                // Если совсем всё плохо — не даем упасть, ставим безопасный дефолт
                if ( PhantomFont == null ) PhantomFont = new Font( "Segoe UI", 7.5f );
            }
        }
    }

    [JsonIgnore] public Pen DeleteBtnCrossPen { get; set; } = new Pen( Color.White, 1.5f );

    [JsonPropertyName( "DeleteBtnCrossColor" )]
    public string DeleteBtnCrossHex
    {
        get => ColorTranslator.ToHtml( DeleteBtnCrossPen.Color );
        set
        {
            var oldColor = DeleteBtnCrossPen.Color;
            var newColor = ColorTranslator.FromHtml( value );
            float width = DeleteBtnCrossPen.Width;
            DeleteBtnCrossPen?.Dispose();
            DeleteBtnCrossPen = new Pen( newColor, width );
        }
    }

    // Толщина крестика (на случай если в Win95 захочется сделать его жирнее)
    public float DeleteBtnCrossWidth
    {
        get => DeleteBtnCrossPen.Width;
        set
        {
            var color = DeleteBtnCrossPen.Color;
            DeleteBtnCrossPen?.Dispose();
            DeleteBtnCrossPen = new Pen( color, value );
        }
    }

    // --- Логика очистки (твоя оригинальная, дополненная) ---

    public void Dispose()
    {
        DisposeResource( BorderLightPen );
        DisposeResource( BorderDarkPen );
        DisposeResource( HeaderBrush );
        DisposeResource( ForeBrush );
        DisposeResource( ButtonSelectedBrush );

        if ( !ReferenceEquals( Font, SystemFonts.DefaultFont ) ) Font?.Dispose();
        GC.SuppressFinalize( this );
    }

    private void DisposeResource( object resource )
    {
        if ( resource != null && !IsSystemResource( resource ) && resource is IDisposable disp )
        {
            disp.Dispose();
        }
    }

    private bool IsSystemResource( object resource )
    {
        return ReferenceEquals( resource, Brushes.White ) ||
               ReferenceEquals( resource, Brushes.Crimson ) ||
               ReferenceEquals( resource, Pens.Black ) ||
               ReferenceEquals( resource, SystemFonts.DefaultFont );
    }
}