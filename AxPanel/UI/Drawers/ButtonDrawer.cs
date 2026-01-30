using AxPanel.UI.Themes;
using AxPanel.UI.UserControls;

namespace AxPanel.UI.Drawers;

public class ButtonDrawer
{
    private ITheme _theme;

    public ButtonDrawer( ITheme theme )
    {
        _theme = theme;
    }

    public void Draw( BaseControl control, MouseState mouseState, KeyboardState keyboardState, PaintEventArgs e, float cpuUsage = 0 )
    {
        var g = e.Graphics;
        var rect = new Rectangle( 0, 0, control.Width - 1, control.Height - 1 );

        if ( string.IsNullOrEmpty( control.Path ) )
        {
            DrawSeparator( g, control, rect );
            return;
        }

        StringFormat format = new()
        {
            LineAlignment = StringAlignment.Center,
            Alignment = StringAlignment.Near,
            FormatFlags = StringFormatFlags.NoWrap
        };

        // 1. Отрисовка фона
        if ( mouseState.MouseInControl )
        {
            g.FillRectangle( keyboardState.AltPressed ? _theme.ButtonStyle.SelectedAltBrush : _theme.ButtonStyle.SelectedBrush, rect );
        }
        else
        {
            g.FillRectangle( _theme.ButtonStyle.UnselectedBrush, rect );
        }

        // --- НОВОЕ: Подсветка и Пульсация ---
        if ( control is LaunchButton lbPulse && lbPulse.IsRunning )
        {
            // Вычисляем фазу пульсации (от 0 до 1) на основе времени
            // Скорость пульсации увеличивается, если CPU > 50%
            float speedMultiplier = cpuUsage > 50 ? 2.0f : 1.0f;
            float pulse = ( float )( Math.Sin( Environment.TickCount * 0.005 * speedMultiplier ) + 1.0 ) / 2.0f;

            // Цвет бордюра (Синий Windows). Альфа меняется от 40 до 180
            int alpha = ( int )( 40 + ( 140 * pulse ) );
            using var pulsePen = new Pen( Color.FromArgb( alpha, 0, 120, 215 ), 1f );

            // Рисуем внутренний светящийся бордюр
            g.DrawRectangle( pulsePen, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2 );

            // Легкое заполнение фона в такт пульсации
            using var pulseBrush = new SolidBrush( Color.FromArgb( ( int )( alpha * 0.1 ), 0, 120, 215 ) );
            g.FillRectangle( pulseBrush, rect );
        }

        if ( control is LaunchButton lb && lb.IsRunning )
        {
            int margin = 4;
            int indicatorWidth = 40;
            int indicatorHeight = 12;
            int xOffset = mouseState.MouseInDeleteButton ? _theme.ButtonStyle.DeleteButtonWidth + margin + 2 : margin;

            // --- Блок CPU ---
            string cpuText = $"{( int )cpuUsage}%";
            var cpuRect = new Rectangle( control.Width - indicatorWidth - xOffset, margin, indicatorWidth, indicatorHeight );
            DrawSmallMeter( g, cpuRect, cpuUsage, cpuText, Color.LimeGreen );

            // --- Блок RAM ---
            var ramRect = new Rectangle( cpuRect.X - indicatorWidth - margin, margin, indicatorWidth, indicatorHeight );
            string ramDisplay = lb.RamUsage >= 1024 ? $"{( lb.RamUsage / 1024f ):N1}G" : $"{lb.RamUsage:0}M";
            float ramPercent = ( lb.RamUsage / 1024f ) * 100f;
            if ( lb.RamUsage > 0 && ramPercent < 5f ) ramPercent = 5f;

            DrawSmallMeter( g, ramRect, ramPercent, ramDisplay, Color.SkyBlue );
        }

        // 3. Границы (стандартные)
        g.DrawLine( _theme.ButtonStyle.BorderLightPen, 0, 0, rect.Right, 0 );
        g.DrawLine( _theme.ButtonStyle.BorderLightPen, 0, 0, 0, rect.Bottom );
        g.DrawLine( _theme.ButtonStyle.BorderDarkPen, rect.Right, 0, rect.Right, rect.Bottom );
        g.DrawLine( _theme.ButtonStyle.BorderDarkPen, 0, rect.Bottom, rect.Right, rect.Bottom );

        // --- ИСПРАВЛЕНИЕ: Безопасно определяем необходимость отступа ---
        bool isRunning = control is LaunchButton lBtn && lBtn.IsRunning;
        int reservedSpace = isRunning ? 100 : 0;

        // 4. Текст (с ограничением ширины, чтобы не наезжал на индикаторы)
        var textRectTop = new Rectangle( _theme.ButtonStyle.DefaultHeight, 0, control.Width - _theme.ButtonStyle.DefaultHeight - reservedSpace, control.Height / 2 );
        var textRectBottom = new Rectangle( _theme.ButtonStyle.DefaultHeight, control.Height / 2, control.Width - _theme.ButtonStyle.DefaultHeight - reservedSpace, control.Height / 2 );

        string displayName = ( control is LaunchButton l && l.IsRunning ) ? $"{control.Text} ({cpuUsage:0}%)" : control.Text;
        g.DrawString( displayName, control.Font, _theme.ButtonStyle.MainFontBrush, textRectTop, format );
        g.DrawString( control.Path, control.Font, _theme.ButtonStyle.AdditionalFontBrush, textRectBottom, format );


        // В методе Draw после отрисовки основного текста
        if ( control is LaunchButton lbTime && lbTime.IsRunning && lbTime.StartTime.HasValue )
        {
            TimeSpan uptime = DateTime.Now - lbTime.StartTime.Value;

            // Формат: "1д 02:30:15" или "02:30:15" или "30:15"
            string timeText = uptime.TotalDays >= 1
                ? $"{( int )uptime.TotalDays}d {uptime:hh\\:mm\\:ss}"
                : uptime.TotalHours >= 1
                    ? uptime.ToString( @"hh\:mm\:ss" )
                    : uptime.ToString( @"mm\:ss" );

            using var timeFont = new Font( "Segoe UI", 7f, FontStyle.Regular );
            // Рисуем внизу, чуть правее иконки
            //g.DrawString( timeText, timeFont, _theme.ButtonStyle.AdditionalFontBrush, _theme.ButtonStyle.DefaultHeight, control.Height - 14 );
        }

        // 5. Иконка
        if ( control.Icon != null )
        {
            int center = _theme.ButtonStyle.DefaultHeight / 2;
            var iconRect = new Rectangle( center - 16, center - 16, 32, 32 );
            g.DrawIconUnstretched( control.Icon, iconRect );
        }

        // 6. Кнопка удаления
        if ( mouseState.MouseInDeleteButton )
        {
            DrawDeleteButton( g, control, format );
        }

        // 7. Синяя полоска слева
        if ( control is LaunchButton launchBtn && launchBtn.IsRunning )
        {
            using var runningBrush = new SolidBrush( Color.FromArgb( 0, 120, 215 ) );
            g.FillRectangle( runningBrush, 0, 2, 3, control.Height - 4 );
        }
    }

    //public void Draw( BaseControl control, MouseState mouseState, KeyboardState keyboardState, PaintEventArgs e, float cpuUsage = 0 )
    //{
    //    var g = e.Graphics;
    //    var rect = new Rectangle( 0, 0, control.Width - 1, control.Height - 1 );

    //    if ( string.IsNullOrEmpty( control.Path ) )
    //    {
    //        DrawSeparator( g, control, rect );
    //        return;
    //    }

    //    StringFormat format = new()
    //    {
    //        LineAlignment = StringAlignment.Center,
    //        Alignment = StringAlignment.Near,
    //        FormatFlags = StringFormatFlags.NoWrap
    //    };

    //    // 1. Отрисовка фона
    //    if ( mouseState.MouseInControl )
    //    {
    //        g.FillRectangle( keyboardState.AltPressed ? _theme.ButtonStyle.SelectedAltBrush : _theme.ButtonStyle.SelectedBrush, rect );
    //    }
    //    else
    //    {
    //        g.FillRectangle( _theme.ButtonStyle.UnselectedBrush, rect );
    //    }

    //    if ( control is LaunchButton lb && lb.IsRunning )
    //    {
    //        int margin = 4;
    //        int indicatorWidth = 40;
    //        int indicatorHeight = 12; // Сделаем чуть тоньше
    //        int xOffset = mouseState.MouseInDeleteButton ? _theme.ButtonStyle.DeleteButtonWidth + margin + 2 : margin;

    //        // --- Блок CPU ---
    //        string cpuText = $"{( int )cpuUsage}%";
    //        var cpuRect = new Rectangle( control.Width - indicatorWidth - xOffset, margin, indicatorWidth, indicatorHeight );
    //        DrawSmallMeter( g, cpuRect, cpuUsage, cpuText, Color.LimeGreen );

    //        var ramRect = new Rectangle( cpuRect.X - indicatorWidth - margin, margin, indicatorWidth, indicatorHeight );

    //        string ramDisplay = lb.RamUsage >= 1024
    //            ? $"{( lb.RamUsage / 1024f ):N1}G"
    //            : $"{lb.RamUsage:0}M";

    //        // Для RAM вместо % просто рисуем закраску, если считаем что 1ГБ (1024МБ) - это 100% шкалы
    //        float ramPercent = ( lb.RamUsage / 1024f ) * 100f;

    //        // 3. Важнейшая правка: гарантируем видимость полоски (минимум 1-2 пикселя), если RAM > 0
    //        if ( lb.RamUsage > 0 && ramPercent < 5f ) ramPercent = 5f;
    //        DrawSmallMeter( g, ramRect, ramPercent, ramDisplay, Color.SkyBlue );
    //    }

    //    // 3. Границы
    //    g.DrawLine( _theme.ButtonStyle.BorderLightPen, 0, 0, rect.Right, 0 );
    //    g.DrawLine( _theme.ButtonStyle.BorderLightPen, 0, 0, 0, rect.Bottom );
    //    g.DrawLine( _theme.ButtonStyle.BorderDarkPen, rect.Right, 0, rect.Right, rect.Bottom );
    //    g.DrawLine( _theme.ButtonStyle.BorderDarkPen, 0, rect.Bottom, rect.Right, rect.Bottom );

    //    // 4. Текст
    //    var textRectTop = new Rectangle( _theme.ButtonStyle.DefaultHeight, 0, control.Width - 1 - _theme.ButtonStyle.DefaultHeight, control.Height / 2 );
    //    var textRectBottom = new Rectangle( _theme.ButtonStyle.DefaultHeight, control.Height / 2, control.Width - 1 - _theme.ButtonStyle.DefaultHeight, control.Height / 2 );

    //    // Если есть загрузка, можно добавить % в заголовок
    //    string displayName = ( control is LaunchButton l && l.IsRunning ) ? $"{control.Text} ({cpuUsage:0}%)" : control.Text;

    //    g.DrawString( displayName, control.Font, _theme.ButtonStyle.MainFontBrush, textRectTop, format );
    //    g.DrawString( control.Path, control.Font, _theme.ButtonStyle.AdditionalFontBrush, textRectBottom, format );

    //    // 5. Иконка
    //    if ( control.Icon != null )
    //    {
    //        int center = _theme.ButtonStyle.DefaultHeight / 2;
    //        var iconRect = new Rectangle( center - 16, center - 16, 32, 32 );
    //        g.DrawIconUnstretched( control.Icon, iconRect );
    //    }

    //    // 6. Кнопка удаления
    //    if ( mouseState.MouseInDeleteButton )
    //    {
    //        DrawDeleteButton( g, control, format );
    //    }

    //    // 7. Индикатор активности (синяя полоска слева)
    //    if ( control is LaunchButton launchBtn && launchBtn.IsRunning )
    //    {
    //        using var runningBrush = new SolidBrush( Color.FromArgb( 0, 120, 215 ) );
    //        g.FillRectangle( runningBrush, 0, 2, 3, control.Height - 4 );
    //    }
    //}

    private void DrawSmallMeter( Graphics g, Rectangle rect, float percent, string text, Color color )
    {
        using var bg = new SolidBrush( Color.FromArgb( 100, 0, 0, 0 ) );
        g.FillRectangle( bg, rect );

        // Ограничиваем процент, чтобы ширина не превысила 100%
        float clampedPercent = Math.Max( 0, Math.Min( 100, percent ) );

        // Вычисляем ширину закраски
        int fillWidth = ( int )( rect.Width * ( clampedPercent / 100f ) );

        if ( fillWidth > 0 )
        {
            using var bar = new SolidBrush( Color.FromArgb( 150, color ) );
            g.FillRectangle( bar, rect.X, rect.Y, fillWidth, rect.Height );
        }

        using var font = new Font( "Segoe UI", 6.5f, FontStyle.Bold );
        TextRenderer.DrawText( g, text, font, rect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter );
    }

    private void DrawSeparator( Graphics g, BaseControl control, Rectangle rect )
    {
        // Отрисовка фона разделителя (чуть темнее или прозрачнее)
        g.FillRectangle( _theme.ButtonStyle.UnselectedBrush, rect );

        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        // Текст заголовка группы
        string cleanText = control.Text.Replace( "-", "" ).Trim();
        using var font = new Font( "Segoe UI", 7f, FontStyle.Bold );
        var textWidth = ( int )g.MeasureString( cleanText, font ).Width + 10;

        // Рисуем линии по бокам от текста
        int lineY = rect.Height / 2;
        int margin = 10;

        g.DrawLine( _theme.ButtonStyle.BorderLightPen, margin, lineY, ( rect.Width / 2 ) - ( textWidth / 2 ), lineY );
        g.DrawLine( _theme.ButtonStyle.BorderLightPen, ( rect.Width / 2 ) + ( textWidth / 2 ), lineY, rect.Width - margin, lineY );

        g.DrawString( cleanText.ToUpper(), font, _theme.ButtonStyle.AdditionalFontBrush, rect, format );
    }

    private void DrawDeleteButton( Graphics g, BaseControl control, StringFormat format )
    {
        // 1. Настройка области (делаем кнопку чуть компактнее и изящнее)
        int margin = 4;
        var delRect = new Rectangle(
            control.Width - _theme.ButtonStyle.DeleteButtonWidth - margin,
            margin,
            _theme.ButtonStyle.DeleteButtonWidth,
            control.Height - ( margin * 2 )
        );

        // Включаем сглаживание для отрисовки иконок и скруглений
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // 2. Фон кнопки (мягкий красный градиент или полупрозрачный алый)
        using var backBrush = new SolidBrush( Color.FromArgb( 50, 230, 60, 60 ) ); // Акцентный красный

        // Рисуем скругленный прямоугольник (в GDI+ через путь или просто FillRectangle)
        g.FillRectangle( backBrush, delRect );

        // 3. Отрисовка иконки корзины (вместо текста)
        // Рисуем простую векторную корзину белым пером
        using var pen = new Pen( Color.White, 1.5f );
        int cx = delRect.X + delRect.Width / 2;
        int cy = delRect.Y + delRect.Height / 2;

        // Тело корзины
        g.DrawRectangle( pen, cx - 4, cy - 2, 8, 7 );
        // Крышка
        g.DrawLine( pen, cx - 6, cy - 4, cx + 6, cy - 4 );
        // Ручка крышки
        g.DrawLine( pen, cx - 2, cy - 4, cx - 2, cy - 6 );
        g.DrawLine( pen, cx + 2, cy - 4, cx + 2, cy - 6 );
        g.DrawLine( pen, cx - 2, cy - 6, cx + 2, cy - 6 );

        // 4. Тонкая обводка всей кнопки
        using var borderPen = new Pen( Color.FromArgb( 100, Color.White ) );
        g.DrawRectangle( borderPen, delRect );
    }
}