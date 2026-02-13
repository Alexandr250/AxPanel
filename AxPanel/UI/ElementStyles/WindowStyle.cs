using System.Text.Json.Serialization;

namespace AxPanel.UI.ElementStyles;

#pragma warning disable CS8618
public class WindowStyle
{
    // Цвета для кнопки "Свернуть"
    [JsonIgnore] public SolidBrush MinBtnHoverBrush { get; set; } = new SolidBrush( Color.FromArgb( 40, Color.White ) );

    // Цвета для кнопки "Закрыть"
    [JsonIgnore] public SolidBrush CloseBtnHoverBrush { get; set; } = new SolidBrush( Color.Crimson );

    // HEX-обертки
    [JsonPropertyName( "MinBtnHoverColor" )]
    public string MinBtnHoverHex
    {
        get => ColorTranslator.ToHtml( MinBtnHoverBrush.Color );
        set { MinBtnHoverBrush?.Dispose(); MinBtnHoverBrush = new SolidBrush( ColorTranslator.FromHtml( value ) ); }
    }

    [JsonPropertyName( "CloseBtnHoverColor" )]
    public string CloseBtnHoverHex
    {
        get => ColorTranslator.ToHtml( CloseBtnHoverBrush.Color );
        set { CloseBtnHoverBrush?.Dispose(); CloseBtnHoverBrush = new SolidBrush( ColorTranslator.FromHtml( value ) ); }
    }

    // Цвет иконок управления (свернуть/закрыть)
    [JsonIgnore] public Color ControlIconColor { get; set; } = Color.White;

    // Индикатор тройного клика (те самые точки)
    [JsonIgnore] public Color ExitIndicatorColor { get; set; } = Color.OrangeRed;

    // Цвет текста в заголовке (AX-PANEL v1.0)
    [JsonIgnore] public Color TitleColor { get; set; } = Color.Gray;

    // Линия-разделитель под хедером
    [JsonIgnore] public Color SeparatorColor { get; set; } = Color.FromArgb( 60, 60, 60 );

    // Добавляем HEX-обертки для них (по аналогии с прошлыми)
    [JsonPropertyName( "ControlIconColor" )]
    public string ControlIconHex { get => ColorTranslator.ToHtml( ControlIconColor ); set => ControlIconColor = ColorTranslator.FromHtml( value ); }

    [JsonPropertyName( "ExitIndicatorColor" )]
    public string ExitIndicatorHex { get => ColorTranslator.ToHtml( ExitIndicatorColor ); set => ExitIndicatorColor = ColorTranslator.FromHtml( value ); }

    [JsonPropertyName( "TitleColor" )]
    public string TitleHex { get => ColorTranslator.ToHtml( TitleColor ); set => TitleColor = ColorTranslator.FromHtml( value ); }

    [JsonPropertyName( "SeparatorColor" )]
    public string SeparatorHex { get => ColorTranslator.ToHtml( SeparatorColor ); set => SeparatorColor = ColorTranslator.FromHtml( value ); }

    // Оригинальные свойства исключаем из сериализации
    [JsonIgnore]
    public Color BackColor { get; set; } = Color.FromArgb( 20, 20, 20 );

    [JsonIgnore]
    public Color HeaderColor { get; set; } = Color.FromArgb( 0, 0, 105 );

    // Обычные типы (int) сериализуются без проблем
    public int HeaderHeight { get; set; } = 30;

    // --- Свойства-обертки для JSON ---

    [JsonPropertyName( "BackColor" )]
    public string BackColorHex
    {
        get => ColorTranslator.ToHtml( BackColor );
        set => BackColor = ColorTranslator.FromHtml( value );
    }

    [JsonPropertyName( "HeaderColor" )]
    public string HeaderColorHex
    {
        get => ColorTranslator.ToHtml( HeaderColor );
        set => HeaderColor = ColorTranslator.FromHtml( value );
    }

    [JsonIgnore]
    public Color FooterColor { get; set; } = Color.FromArgb( 0, 0, 105 );

    [JsonPropertyName( "FooterColor" )]
    public string FooterColorHex
    {
        get => ColorTranslator.ToHtml( FooterColor );
        set => FooterColor = ColorTranslator.FromHtml( value );
    }

    // Цвета кнопок футера
    [JsonIgnore] public Color FooterBtnHoverColor { get; set; } = Color.FromArgb( 50, 255, 255, 255 );
    [JsonIgnore] public Color FooterCloseBtnHoverColor { get; set; } = Color.FromArgb( 100, 232, 17, 35 );
    [JsonIgnore] public Color FooterTextColor { get; set; } = Color.White;
    [JsonIgnore] public Color FooterSeparatorColor { get; set; } = Color.FromArgb( 40, 255, 255, 255 );

    // Шрифты
    [JsonIgnore] public Font FooterIconFont { get; set; } = new Font( "Segoe MDL2 Assets", 10 );
    [JsonIgnore] public Font FooterTextFont { get; set; } = new Font( "Segoe UI", 9f );

    [JsonPropertyName( "FooterBtnHoverColor" )]
    public string FooterBtnHoverHex { get => ColorTranslator.ToHtml( FooterBtnHoverColor ); set => FooterBtnHoverColor = ColorTranslator.FromHtml( value ); }

    [JsonPropertyName( "FooterCloseBtnHoverColor" )]
    public string FooterCloseBtnHoverHex { get => ColorTranslator.ToHtml( FooterCloseBtnHoverColor ); set => FooterCloseBtnHoverColor = ColorTranslator.FromHtml( value ); }

    [JsonPropertyName( "FooterTextColor" )]
    public string FooterTextHex { get => ColorTranslator.ToHtml( FooterTextColor ); set => FooterTextColor = ColorTranslator.FromHtml( value ); }

    [JsonPropertyName( "FooterSeparatorColor" )]
    public string FooterSeparatorHex { get => ColorTranslator.ToHtml( FooterSeparatorColor ); set => FooterSeparatorColor = ColorTranslator.FromHtml( value ); }

    // --- Обертки для шрифтов ---
    [JsonPropertyName( "FooterIconFont" )]
    public string FooterIconFontString
    {
        get => $"{FooterIconFont.Name}, {FooterIconFont.Size}, {FooterIconFont.Style}";
        set { FooterIconFont?.Dispose(); FooterIconFont = ParseFont( value, "Segoe MDL2 Assets", 10 ); }
    }

    [JsonPropertyName( "FooterTextFont" )]
    public string FooterTextFontString
    {
        get => $"{FooterTextFont.Name}, {FooterTextFont.Size}, {FooterTextFont.Style}";
        set { FooterTextFont?.Dispose(); FooterTextFont = ParseFont( value, "Segoe UI", 9 ); }
    }

    // Вспомогательный метод парсинга (используй тот, что мы писали ранее с InvariantCulture)
    private Font ParseFont( string value, string defName, float defSize ) { return new Font( defName, defSize ); }

    [JsonIgnore] public Pen FooterBtnBorderLightPen { get; set; } = new Pen( Color.White );
    [JsonIgnore] public Pen FooterBtnBorderDarkPen { get; set; } = new Pen( Color.Gray );

    [JsonPropertyName( "FooterBtnBorderLightColor" )]
    public string FooterBtnBorderLightHex
    {
        get => ColorTranslator.ToHtml( FooterBtnBorderLightPen.Color );
        set { FooterBtnBorderLightPen?.Dispose(); FooterBtnBorderLightPen = new Pen( ColorTranslator.FromHtml( value ) ); }
    }

    [JsonPropertyName( "FooterBtnBorderDarkColor" )]
    public string FooterBtnBorderDarkHex
    {
        get => ColorTranslator.ToHtml( FooterBtnBorderDarkPen.Color );
        set { FooterBtnBorderDarkPen?.Dispose(); FooterBtnBorderDarkPen = new Pen( ColorTranslator.FromHtml( value ) ); }
    }

    [JsonIgnore] public Pen ControlBtnBorderLightPen { get; set; } = new Pen( Color.White );
    [JsonIgnore] public Pen ControlBtnBorderDarkPen { get; set; } = new Pen( Color.Gray );

    [JsonPropertyName( "ControlBtnBorderLightColor" )]
    public string ControlBtnBorderLightHex
    {
        get => ColorTranslator.ToHtml( ControlBtnBorderLightPen.Color );
        set { ControlBtnBorderLightPen?.Dispose(); ControlBtnBorderLightPen = new Pen( ColorTranslator.FromHtml( value ) ); }
    }

    [JsonPropertyName( "ControlBtnBorderDarkColor" )]
    public string ControlBtnBorderDarkHex
    {
        get => ColorTranslator.ToHtml( ControlBtnBorderDarkPen.Color );
        set { ControlBtnBorderDarkPen?.Dispose(); ControlBtnBorderDarkPen = new Pen( ColorTranslator.FromHtml( value ) ); }
    }

    [JsonIgnore] public Pen WindowBorderLightPen { get; set; } = new Pen( Color.White );
    [JsonIgnore] public Pen WindowBorderDarkPen { get; set; } = new Pen( Color.Gray );

    [JsonPropertyName( "WindowBorderLightColor" )]
    public string WindowBorderLightHex
    {
        get => ColorTranslator.ToHtml( WindowBorderLightPen.Color );
        set { WindowBorderLightPen?.Dispose(); WindowBorderLightPen = new Pen( ColorTranslator.FromHtml( value ) ); }
    }

    [JsonPropertyName( "WindowBorderDarkColor" )]
    public string WindowBorderDarkHex
    {
        get => ColorTranslator.ToHtml( WindowBorderDarkPen.Color );
        set { WindowBorderDarkPen?.Dispose(); WindowBorderDarkPen = new Pen( ColorTranslator.FromHtml( value ) ); }
    }

    // Толщину границы тоже выносим, чтобы в Win95 она была жирной, а в Pastel - 1px
    public int BorderWidth { get; set; } = 5;

    [JsonIgnore] public Color AccentColor { get; set; } = Color.FromArgb( 0, 120, 215 );

    [JsonPropertyName( "AccentColor" )]
    public string AccentHex
    {
        get => ColorTranslator.ToHtml( AccentColor );
        set => AccentColor = ColorTranslator.FromHtml( value );
    }
}