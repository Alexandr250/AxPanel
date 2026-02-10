// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertIfStatementToNullCoalescingExpression
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

using System.Text.Json.Serialization;

namespace AxPanel.UI.ElementStyles;

#pragma warning disable CS8618
public class ButtonStyle : IDisposable
{
    // Группировка констант
    public int DefaultHeight { get; set; } = 36; // 42
    public int DefaultWidth { get; set; } = 60;

    public int DeleteButtonWidth { get; set; } = 50;
    public int SpaceHeight { get; set; } = 1;
    public int SpaceWidth { get; set; } = 3;

    // Ресурсы для объемной обводки бейджа
    [JsonIgnore] public Pen BadgeBorderLightPen { get; set; } = new Pen( Color.FromArgb( 200, Color.White ), 1.5f );
    [JsonIgnore] public Pen BadgeBorderDarkPen { get; set; } = new Pen( Color.FromArgb( 200, Color.Black ), 1.5f );

    [JsonPropertyName( "BadgeBorderLightColor" )]
    public string BadgeBorderLightHex
    {
        get => ColorTranslator.ToHtml( BadgeBorderLightPen.Color );
        set { BadgeBorderLightPen?.Dispose(); BadgeBorderLightPen = new Pen( ColorTranslator.FromHtml( value ), 1.5f ); }
    }

    [JsonPropertyName( "BadgeBorderDarkColor" )]
    public string BadgeBorderDarkHex
    {
        get => ColorTranslator.ToHtml( BadgeBorderDarkPen.Color );
        set { BadgeBorderDarkPen?.Dispose(); BadgeBorderDarkPen = new Pen( ColorTranslator.FromHtml( value ), 1.5f ); }
    }

    // --- Ресурсы бейджа ---
    [JsonIgnore] public SolidBrush BadgeBrush { get; set; } = new SolidBrush( Color.FromArgb( 200, 0, 120, 215 ) );
    [JsonIgnore] public Color BadgeTextColor { get; set; } = Color.White;
    [JsonIgnore] public Font BadgeFont { get; set; } = new Font( "Segoe UI", 7f, FontStyle.Bold );

    // --- HEX-обертки для JSON ---
    [JsonPropertyName( "BadgeColor" )]
    public string BadgeColorHex
    {
        get => ColorTranslator.ToHtml( BadgeBrush.Color );
        set { BadgeBrush?.Dispose(); BadgeBrush = new SolidBrush( ColorTranslator.FromHtml( value ) ); }
    }

    [JsonPropertyName( "BadgeTextColor" )]
    public string BadgeTextHex
    {
        get => ColorTranslator.ToHtml( BadgeTextColor );
        set => BadgeTextColor = ColorTranslator.FromHtml( value );
    }

    [JsonPropertyName( "BadgeFont" )]
    public string BadgeFontString
    {
        get => $"{BadgeFont.Name}, {BadgeFont.Size}, {BadgeFont.Style}";
        set { BadgeFont?.Dispose(); /* Логика парсинга шрифта */ }
    }


    // Использование автосвойств с инициализацией
    [JsonIgnore]
    public Pen BorderLightPen { get; set; } = new( Color.FromArgb( 30, 30, 30 ) );

    [JsonIgnore]
    public Pen BorderDarkPen { get; set; } = new( Color.FromArgb( 30, 30, 30 ) );

    [JsonIgnore]
    public Brush SelectedBrush { get; set; } = new SolidBrush( Color.FromArgb( 80, 80, 80 ) );

    [JsonIgnore]
    public Brush SelectedAltBrush { get; set; } = new SolidBrush( Color.FromArgb( 190, 80, 80 ) );

    [JsonIgnore]
    public Brush UnselectedBrush { get; set; } = new SolidBrush( Color.FromArgb( 60, 60, 60 ) );

    [JsonIgnore]
    public Brush MainFontBrush { get; set; } = Brushes.White;

    [JsonIgnore]
    public Brush AdditionalFontBrush { get; set; } = new SolidBrush( Color.FromArgb( 150, 150, 150 ) );

    // Readonly свойства (если менять их не планируется)
    [JsonIgnore]
    public Brush DeleteButtonBrush { get; private set; } = new SolidBrush( Color.FromArgb( 150, 140, 140, 140 ) );

    [JsonIgnore]
    public Pen DeleteButtonBorderPen { get; private set; } = new( Color.FromArgb( 150, 80, 80, 80 ) );

    [JsonIgnore]
    public Font MinFont { get; private set; } = new Font( FontFamily.GenericSerif, 6, FontStyle.Regular, GraphicsUnit.Pixel );


    [JsonPropertyName( "BorderLightColor" )]
    public string BorderLightPenHex
    {
        get => ColorTranslator.ToHtml( BorderLightPen.Color );
        set { BorderLightPen?.Dispose(); BorderLightPen = new Pen( ColorTranslator.FromHtml( value ) ); }
    }

    [JsonPropertyName( "BorderDarkColor" )]
    public string BorderDarkPenHex
    {
        get => ColorTranslator.ToHtml( BorderDarkPen.Color );
        set
        {
            // Важно: освобождаем старое перо перед созданием нового
            BorderDarkPen?.Dispose();
            BorderDarkPen = new Pen( ColorTranslator.FromHtml( value ) );
        }
    }

    [JsonPropertyName( "SelectedColor" )]
    public string SelectedBrushHex
    {
        get => ColorTranslator.ToHtml( ( ( SolidBrush )SelectedBrush ).Color );
        set { SelectedBrush?.Dispose(); SelectedBrush = new SolidBrush( ColorTranslator.FromHtml( value ) ); }
    }

    [JsonPropertyName( "SelectedAltColor" )]
    public string SelectedAltBrushHex
    {
        get => ColorTranslator.ToHtml( ( ( SolidBrush )SelectedAltBrush ).Color );
        set
        {
            // Освобождаем старую кисть, чтобы не «сорить» в GDI+
            SelectedAltBrush?.Dispose();
            SelectedAltBrush = new SolidBrush( ColorTranslator.FromHtml( value ) );
        }
    }

    [JsonPropertyName( "UnselectedColor" )]
    public string UnselectedBrushHex
    {
        get => ColorTranslator.ToHtml( ( ( SolidBrush )UnselectedBrush ).Color );
        set { UnselectedBrush?.Dispose(); UnselectedBrush = new SolidBrush( ColorTranslator.FromHtml( value ) ); }
    }

    [JsonPropertyName( "MainFontColor" )]
    public string MainFontBrushHex
    {
        get => ColorTranslator.ToHtml( ( ( SolidBrush )MainFontBrush ).Color );
        set
        {
            Color newColor = ColorTranslator.FromHtml( value );

            // ПРОВЕРКА: Если текущая кисть НЕ системная, то освобождаем её
            // (Системные кисти зашиты в память и не должны удаляться)
            if ( MainFontBrush != Brushes.White && MainFontBrush is SolidBrush )
            {
                MainFontBrush.Dispose();
            }

            MainFontBrush = new SolidBrush( newColor );
        }
    }

    [JsonPropertyName( "AdditionalFontColor" )]
    public string AdditionalFontBrushHex
    {
        get => ColorTranslator.ToHtml( ( ( SolidBrush )AdditionalFontBrush ).Color );
        set
        {
            // Обязательно очищаем старый ресурс GDI+
            AdditionalFontBrush?.Dispose();
            AdditionalFontBrush = new SolidBrush( ColorTranslator.FromHtml( value ) );
        }
    }

    [JsonPropertyName( "DeleteButtonColor" )]
    public string DeleteButtonBrushHex
    {
        get => ColorTranslator.ToHtml( ( ( SolidBrush )DeleteButtonBrush ).Color );
        set
        {
            DeleteButtonBrush?.Dispose();
            DeleteButtonBrush = new SolidBrush( ColorTranslator.FromHtml( value ) );
        }
    }

    [JsonPropertyName( "DeleteButtonBorderColor" )]
    public string DeleteButtonBorderPenHex
    {
        get => ColorTranslator.ToHtml( DeleteButtonBorderPen.Color );
        set
        {
            DeleteButtonBorderPen?.Dispose();
            DeleteButtonBorderPen = new Pen( ColorTranslator.FromHtml( value ) );
        }
    }

    [JsonPropertyName( "MinFont" )]
    public string MinFontString
    {
        get => $"{MinFont.FontFamily.Name}, {MinFont.Size}, {MinFont.Style}";
        set
        {
            try
            {
                var parts = value.Split( ',' );
                string name = parts[ 0 ].Trim();
                float size = float.Parse( parts[ 1 ].Trim(), System.Globalization.CultureInfo.InvariantCulture );
                FontStyle style = ( FontStyle )Enum.Parse( typeof( FontStyle ), parts[ 2 ].Trim() );

                MinFont?.Dispose();
                MinFont = new Font( name, size, style, GraphicsUnit.Pixel );
            }
            catch { /* Оставляем дефолтный шрифт при ошибке в JSON */ }
        }
    }


    // Цвет запущенного процесса (маркер и пульсация)
    [JsonIgnore] public Color RunningColor { get; set; } = Color.FromArgb( 0, 120, 215 );

    [JsonPropertyName( "RunningColor" )]
    public string RunningColorHex
    {
        get => ColorTranslator.ToHtml( RunningColor );
        set => RunningColor = ColorTranslator.FromHtml( value );
    }

    // Настройки пульсации
    public float PulseSpeed { get; set; } = 0.005f;
    public float PulseLineWidth { get; set; } = 1.0f;

    // Настройки маркера слева
    public int ActivityMarkerWidth { get; set; } = 3;

    // --- Параметры разделителя (Separator) ---
    [JsonIgnore] public SolidBrush GroupPlayBrush { get; set; } = new SolidBrush( Color.FromArgb( 180, Color.LimeGreen ) );
    [JsonIgnore] public Color GroupPlayGlowColor { get; set; } = Color.FromArgb( 100, Color.LimeGreen );
    [JsonIgnore] public Font SeparatorFont { get; set; } = new Font( "Segoe UI", 7f, FontStyle.Bold );

    [JsonPropertyName( "GroupPlayColor" )]
    public string GroupPlayHex
    {
        get => ColorTranslator.ToHtml( GroupPlayBrush.Color );
        set { GroupPlayBrush?.Dispose(); GroupPlayBrush = new SolidBrush( ColorTranslator.FromHtml( value ) ); }
    }

    [JsonPropertyName( "GroupPlayGlowColor" )]
    public string GroupPlayGlowHex
    {
        get => ColorTranslator.ToHtml( GroupPlayGlowColor );
        set => GroupPlayGlowColor = ColorTranslator.FromHtml( value );
    }

    [JsonPropertyName( "SeparatorFont" )]
    public string SeparatorFontString
    {
        get => $"{SeparatorFont.Name}, {SeparatorFont.Size}, {SeparatorFont.Style}";
        set
        {
            if ( string.IsNullOrWhiteSpace( value ) ) return;

            try
            {
                var parts = value.Split( ',' );
                if ( parts.Length < 3 ) return;

                string name = parts[ 0 ].Trim();
                // Используем InvariantCulture, чтобы точка в JSON понималась всегда
                if ( !float.TryParse( parts[ 1 ].Trim(), System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out float size ) )
                {
                    size = 7f; // Дефолт, если не распарсилось
                }

                if ( size <= 0 ) size = 7f;

                FontStyle style = FontStyle.Regular;
                if ( Enum.TryParse( parts[ 2 ].Trim(), true, out FontStyle parsedStyle ) )
                {
                    style = parsedStyle;
                }

                var oldFont = SeparatorFont;
                SeparatorFont = new Font( name, size, style );
                oldFont?.Dispose();
            }
            catch
            {
                // Если всё совсем плохо, оставляем как есть или ставим системный
                if ( SeparatorFont == null ) SeparatorFont = new Font( "Segoe UI", 7f, FontStyle.Bold );
            }
        }
    }

    public int MeterWidth { get; set; } = 40;
    public int MeterHeight { get; set; } = 12;
    public int MeterMargin { get; set; } = 4;

    [JsonIgnore] public Color CpuIndicatorColor { get; set; } = Color.LimeGreen;
    [JsonIgnore] public Color RamIndicatorColor { get; set; } = Color.SkyBlue;

    [JsonPropertyName( "CpuColor" )]
    public string CpuColorHex { get => ColorTranslator.ToHtml( CpuIndicatorColor ); set => CpuIndicatorColor = ColorTranslator.FromHtml( value ); }

    [JsonPropertyName( "RamColor" )]
    public string RamColorHex { get => ColorTranslator.ToHtml( RamIndicatorColor ); set => RamIndicatorColor = ColorTranslator.FromHtml( value ); }

    // Ресурсы для метров (индикаторов)
    [JsonIgnore]
    public SolidBrush MeterBgBrush { get; set; } = new SolidBrush( Color.FromArgb( 100, 0, 0, 0 ) );

    [JsonIgnore]
    public Font MeterFont { get; set; } = new Font( "Segoe UI", 6.5f, FontStyle.Bold );

    [JsonIgnore]
    public Color MeterTextColor { get; set; } = Color.White;

    // HEX-обертка для фона
    [JsonPropertyName( "MeterBgColor" )]
    public string MeterBgHex
    {
        get => ColorTranslator.ToHtml( MeterBgBrush.Color );
        set
        {
            MeterBgBrush?.Dispose();
            MeterBgBrush = new SolidBrush( ColorTranslator.FromHtml( value ) );
        }
    }

    // Обертка для цвета текста
    [JsonPropertyName( "MeterTextColor" )]
    public string MeterTextHex
    {
        get => ColorTranslator.ToHtml( MeterTextColor );
        set => MeterTextColor = ColorTranslator.FromHtml( value );
    }

    // Обертка для шрифта
    [JsonPropertyName( "MeterFont" )]
    public string MeterFontString
    {
        get => $"{MeterFont.Name}, {MeterFont.Size}, {MeterFont.Style}";
        set
        {
            if ( string.IsNullOrWhiteSpace( value ) ) return;
            try
            {
                var parts = value.Split( ',' );
                string name = parts[ 0 ].Trim();
                float size = float.Parse( parts[ 1 ].Trim(), System.Globalization.CultureInfo.InvariantCulture );
                FontStyle style = ( FontStyle )Enum.Parse( typeof( FontStyle ), parts[ 2 ].Trim() );

                MeterFont?.Dispose();
                MeterFont = new Font( name, size, style );
            }
            catch { /* дефолт при ошибке */ }
        }
    }

    [JsonIgnore] public Pen MeterBorderLightPen { get; set; } = new Pen( Color.FromArgb( 200, Color.White ), 1f );
    [JsonIgnore] public Pen MeterBorderDarkPen { get; set; } = new Pen( Color.FromArgb( 200, Color.Gray ), 1f );

    [JsonPropertyName( "MeterBorderLightColor" )]
    public string MeterBorderLightHex
    {
        get => ColorTranslator.ToHtml( MeterBorderLightPen.Color );
        set { MeterBorderLightPen?.Dispose(); MeterBorderLightPen = new Pen( ColorTranslator.FromHtml( value ), 1f ); }
    }

    [JsonPropertyName( "MeterBorderDarkColor" )]
    public string MeterBorderDarkHex
    {
        get => ColorTranslator.ToHtml( MeterBorderDarkPen.Color );
        set { MeterBorderDarkPen?.Dispose(); MeterBorderDarkPen = new Pen( ColorTranslator.FromHtml( value ), 1f ); }
    }

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

        MeterBgBrush?.Dispose();
        MeterFont?.Dispose();

        MeterBorderLightPen?.Dispose();
        MeterBorderDarkPen?.Dispose();

        // Brushes.White удалять нельзя, так как это системный ресурс
        GC.SuppressFinalize( this );
    }
}