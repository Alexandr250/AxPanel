using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices.JavaScript;
using AxPanel.UI.Themes;
using AxPanel.UI.UserControls;

namespace AxPanel.UI.Drawers;

public class ButtonDrawer
{
    private readonly ITheme _theme;

    public ButtonDrawer( ITheme theme )
    {
        _theme = theme;
    }

    public void Draw( BaseControl control, MouseState mouseState, KeyboardState keyboardState, PaintEventArgs e, float cpuUsage = 0 )
    {
        var g = e.Graphics;
        var rect = new Rectangle( 0, 0, control.Width - 1, control.Height - 1 );

        // 1. Отрисовка разделителя (если путь пустой)
        if ( string.IsNullOrEmpty( control.BaseControlPath ) )
        {
            DrawSeparator( g, control, rect, mouseState );
            return;
        }

        // Определяем "компактный режим" (для сетки)
        bool isCompact = control.Width < 200;

        StringFormat format = new()
        {
            LineAlignment = StringAlignment.Center,
            Alignment = isCompact ? StringAlignment.Center : StringAlignment.Near,
            //Trimming = StringTrimming.Word
            FormatFlags = StringFormatFlags.NoWrap
        };

        // 2. Фон и выделение
        if ( mouseState.MouseInControl )
        {
            g.FillRectangle( keyboardState.AltPressed ? _theme.ButtonStyle.SelectedAltBrush : _theme.ButtonStyle.SelectedBrush, rect );
        }
        else
        {
            g.FillRectangle( _theme.ButtonStyle.UnselectedBrush, rect );
        }

        // 3. Подсветка запущенного процесса (Пульсация)
        if ( control is LaunchButtonView lbPulse && lbPulse.Stats.IsRunning )
        {
            float speedMultiplier = cpuUsage > 50 ? 2.0f : 1.0f;
            float pulse = ( float )( Math.Sin( Environment.TickCount * _theme.ButtonStyle.PulseSpeed * speedMultiplier ) + 1.0 ) / 2.0f;
            int alpha = ( int )( 40 + ( 140 * pulse ) );

            using var pulsePen = new Pen( Color.FromArgb( alpha, _theme.ButtonStyle.RunningColor ), _theme.ButtonStyle.PulseLineWidth );
            g.DrawRectangle( pulsePen, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2 );

            using var pulseBrush = new SolidBrush( Color.FromArgb( ( int )( alpha * 0.1 ), _theme.ButtonStyle.RunningColor ) );
            g.FillRectangle( pulseBrush, rect );
        }

        // 4. Индикаторы CPU/RAM (только в режиме списка)
        if ( !isCompact && control is LaunchButtonView lb && lb.Stats.IsRunning )
        {
            int margin = _theme.ButtonStyle.MeterMargin;
            int indicatorWidth = _theme.ButtonStyle.MeterWidth;
            int indicatorHeight = _theme.ButtonStyle.MeterHeight;

            // Смещение, если видна кнопка удаления
            int xOffset = mouseState.MouseInDeleteButton ? _theme.ButtonStyle.DeleteButtonWidth + margin + 2 : margin;

            // CPU Индикатор
            var cpuRect = new Rectangle( control.Width - indicatorWidth - xOffset, margin, indicatorWidth, indicatorHeight );
            DrawSmallMeter( g, cpuRect, cpuUsage, $"{( int )cpuUsage}%", _theme.ButtonStyle.CpuIndicatorColor );

            // RAM Индикатор
            var ramRect = new Rectangle( cpuRect.X - indicatorWidth - margin, margin, indicatorWidth, indicatorHeight );
            string ramText = lb.Stats.RamMb >= 1024 ? $"{( lb.Stats.RamMb / 1024f ):N1}G" : $"{lb.Stats.RamMb:0}M";
            float ramPercent = ( lb.Stats.RamMb / 1024f ) * 100f;
            DrawSmallMeter( g, ramRect, ramPercent, ramText, _theme.ButtonStyle.RamIndicatorColor );
        }

        // 5. Границы
        g.DrawLine( _theme.ButtonStyle.BorderLightPen, 0, 0, rect.Right, 0 );
        g.DrawLine( _theme.ButtonStyle.BorderLightPen, 0, 0, 0, rect.Bottom );
        g.DrawLine( _theme.ButtonStyle.BorderDarkPen, rect.Right, 0, rect.Right, rect.Bottom );
        g.DrawLine( _theme.ButtonStyle.BorderDarkPen, 0, rect.Bottom, rect.Right, rect.Bottom );
        
        // 6. Текст и иконка
        int iconAreaSize = _theme.ButtonStyle.DefaultHeight;
        int reservedRight = isCompact || control is not LaunchButtonView { Stats.IsRunning: true } ? 0 : 100;

        
        DrawIconAndText( control, mouseState, isCompact, g, iconAreaSize, reservedRight, format );

        // 7. Кнопка удаления (только при наведении)
        if ( mouseState.MouseInDeleteButton )
        {
            DrawDeleteButton( g, control, format );
        }

        // 8. Маркер активности (синяя полоса слева)
        if ( control is LaunchButtonView { Stats.IsRunning: true } )
        {
            using var runningBrush = new SolidBrush( _theme.ButtonStyle.RunningColor );
            g.FillRectangle( runningBrush, 0, 2, _theme.ButtonStyle.ActivityMarkerWidth, control.Height - 4 );
        }

        //бэдж
        if ( control is LaunchButtonView { Stats.IsRunning: true, Stats.WindowCount: > 0 } lb2 )
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int badgeSize = 16;
            int margin = 2;
            // Позиционируем в правом верхнем углу
            Rectangle badgeRect = new( margin, margin, badgeSize, badgeSize );

            // Рисуем подложку бейджа (используем системный акцентный синий)
            var badgeBrush = _theme.ButtonStyle.BadgeBrush;//new SolidBrush( Color.FromArgb( 200, 0, 120, 215 ) ); 

            // Темная окантовка (почти черная, с небольшой прозрачностью)
            var borderPen = _theme.ButtonStyle.BadgeBorderLightPen;//new Pen( Color.FromArgb( 180, 10, 10, 10 ), 1.5f );

            g.FillEllipse( badgeBrush, badgeRect );
            //g.DrawEllipse( borderPen, badgeRect );

            g.DrawArc( _theme.ButtonStyle.BadgeBorderLightPen, badgeRect, 135, 180 );

            // 3. Рисуем "тень" (нижняя правая дуга)
            g.DrawArc( _theme.ButtonStyle.BadgeBorderDarkPen, badgeRect, -45, 180 );

            // Рисуем цифру
            var font = _theme.ButtonStyle.BadgeFont; //new Font( "Segoe UI", 7f, FontStyle.Bold );
            TextRenderer.DrawText( g, lb2.Stats.WindowCount.ToString(), font, badgeRect, _theme.ButtonStyle.BadgeTextColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding );

            // Возвращаем режим без сглаживания для четких линий границ кнопок
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        }

    }

    private void DrawIconAndText( BaseControl control, MouseState mouseState, bool isCompact, Graphics g, int iconAreaSize, int reservedRight, StringFormat format )
    {
        if ( isCompact )
        {
            // Режим сетки: Иконка сверху, текст снизу
            int innerSpace = 5;
            int iconLength = 32;

            Rectangle textRect = new( innerSpace, 
                innerSpace + iconLength + innerSpace, 
                control.Width - innerSpace * 2, 
                control.Height - ( iconLength + innerSpace * 2 ) - innerSpace );
            
            TextFormatFlags flags = TextFormatFlags.WordBreak |
                                    TextFormatFlags.HorizontalCenter |
                                    TextFormatFlags.Top |
                                    TextFormatFlags.EndEllipsis |
                                    TextFormatFlags.NoPadding |
                                    TextFormatFlags.TextBoxControl;

            Size proposedSize = new Size( textRect.Width, int.MaxValue );
            Size textSize = TextRenderer.MeasureText( g, control.Text, control.Font, proposedSize, flags );

            bool isTextTrimmed = textSize.Height > textRect.Height;

            Color textColor = Color.Black; // Цвет по умолчанию
            if ( _theme.ButtonStyle.MainFontBrush is SolidBrush sb )
            {
                textColor = sb.Color;
            }

            if ( control.Icon != null )
            {
                Rectangle iconRect;
                float opacity = 1.0f;

                if ( mouseState.MouseInControl && isTextTrimmed )
                {
                    // Центрируем иконку по всему контролу и делаем прозрачной
                    iconRect = new Rectangle( ( control.Width - iconLength ) / 2, ( control.Height - iconLength ) / 2, iconLength, iconLength );
                    opacity = 0.5f;

                    Size fullTextSize = TextRenderer.MeasureText( g, control.Text, control.Font, new Size( control.Width - innerSpace * 2, int.MaxValue ), flags );

                    int centerY = Math.Max( innerSpace, ( control.Height - fullTextSize.Height ) / 2 );

                    textRect = new Rectangle( innerSpace, centerY, control.Width - innerSpace * 2, control.Height - centerY );

                    flags |= TextFormatFlags.Top;
                    flags &= ~TextFormatFlags.VerticalCenter;
                }
                else
                {
                    iconRect = new Rectangle( ( control.Width - iconLength ) / 2, innerSpace, iconLength, iconLength );
                }

                // Рисуем иконку с учетом прозрачности
                if ( opacity < 1.0f )
                {
                    using Bitmap bmp = control.Icon.ToBitmap();
                    using ImageAttributes ia = new();
                    ColorMatrix cm = new() { Matrix33 = opacity };
                    ia.SetColorMatrix( cm );
                    g.DrawImage( bmp, iconRect, 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, ia );
                }
                else
                {
                    g.DrawIconUnstretched( control.Icon, iconRect );
                }
            }

            TextRenderer.DrawText( g, control.Text, control.Font, textRect, textColor, flags );
        }
        else
        {
            // Режим списка: Иконка слева, текст в две строки
            if ( control.Icon != null )
            {
                int center = iconAreaSize / 2;
                g.DrawIconUnstretched( control.Icon, new Rectangle( center - 16, center - 16, 32, 32 ) );
            }
            var textRectTop = new Rectangle( iconAreaSize, 0, control.Width - iconAreaSize - reservedRight, control.Height / 2 );
            var textRectBottom = new Rectangle( iconAreaSize, control.Height / 2, control.Width - iconAreaSize - reservedRight, control.Height / 2 );

            g.DrawString( control.Text, control.Font, _theme.ButtonStyle.MainFontBrush, textRectTop, format );
            g.DrawString( control.BaseControlPath, control.Font, _theme.ButtonStyle.AdditionalFontBrush, textRectBottom, format );
        }
    }

    private void DrawSmallMeter( Graphics g, Rectangle rect, float percent, string text, Color color )
    {
        //using var bg = new SolidBrush( Color.FromArgb( 100, 0, 0, 0 ) );
        g.FillRectangle( _theme.ButtonStyle.MeterBgBrush, rect );

        // 2. Рисуем объемную окантовку (эффект впадины)
        // Верхняя и левая линии (темные)
        g.DrawLine( _theme.ButtonStyle.MeterBorderDarkPen, rect.X, rect.Y, rect.Right - 1, rect.Y );
        g.DrawLine( _theme.ButtonStyle.MeterBorderDarkPen, rect.X, rect.Y, rect.X, rect.Bottom - 1 );

        // Нижняя и правая линии (светлые)
        g.DrawLine( _theme.ButtonStyle.MeterBorderLightPen, rect.X, rect.Bottom - 1, rect.Right - 1, rect.Bottom - 1 );
        g.DrawLine( _theme.ButtonStyle.MeterBorderLightPen, rect.Right - 1, rect.Y, rect.Right - 1, rect.Bottom - 1 );


        int fillWidth = ( int )( rect.Width * ( Math.Clamp( percent, 0, 100 ) / 100f ) );
        if ( fillWidth > 0 )
        {
            using var bar = new SolidBrush( Color.FromArgb( 150, color ) );
            g.FillRectangle( bar, rect.X, rect.Y, fillWidth, rect.Height );
        }
        //using var font = new Font( "Segoe UI", 6.5f, FontStyle.Bold );
        TextRenderer.DrawText( g, 
            text, 
            _theme.ButtonStyle.MeterFont, 
            rect, 
            _theme.ButtonStyle.MeterTextColor, 
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter );

    }

    private void DrawSeparator( Graphics g, BaseControl control, Rectangle rect, MouseState mouseState )
    {
        g.FillRectangle( _theme.ButtonStyle.UnselectedBrush, rect );
        string cleanText = control.Text.Replace( "-", "" ).Trim().ToUpper();
        var font = _theme.ButtonStyle.SeparatorFont; //new Font( "Segoe UI", 7f, FontStyle.Bold );
        var size = g.MeasureString( cleanText, font );
        int textWidth = ( int )size.Width + 10;
        int lineY = rect.Height / 2;

        g.DrawLine( _theme.ButtonStyle.BorderLightPen, 10, lineY, ( rect.Width / 2 ) - ( textWidth / 2 ), lineY );
        g.DrawLine( _theme.ButtonStyle.BorderLightPen, ( rect.Width / 2 ) + ( textWidth / 2 ), lineY, rect.Width - 10, lineY );

        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString( cleanText, font, _theme.ButtonStyle.AdditionalFontBrush, rect, format );

        // Рисуем кнопку группового запуска справа
        int btnSize = 16;
        int margin = 10;
        Rectangle playRect = new Rectangle( rect.Width - btnSize - margin, ( rect.Height - btnSize ) / 2, btnSize, btnSize );

        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        //using var playBrush = new SolidBrush( Color.FromArgb( 180, Color.LimeGreen ) );

        // Рисуем треугольник
        Point[] points = {
            new Point(playRect.X + 4, playRect.Y + 3),
            new Point(playRect.X + 4, playRect.Y + btnSize - 3),
            new Point(playRect.X + btnSize - 2, playRect.Y + btnSize / 2)
        };
        g.FillPolygon( _theme.ButtonStyle.GroupPlayBrush, points );

        // Добавляем легкое свечение вокруг при наведении
        if ( mouseState.MouseInGroupPlay )
        {
            using var glowPen = new Pen( _theme.ButtonStyle.GroupPlayGlowColor, 2 );
            g.DrawEllipse( glowPen, playRect );
        }
    }

    private void DrawDeleteButton( Graphics g, BaseControl control, StringFormat format )
    {
        return;
        int margin = 4;
        var delRect = new Rectangle( control.Width - _theme.ButtonStyle.DeleteButtonWidth - margin, margin, _theme.ButtonStyle.DeleteButtonWidth, control.Height - ( margin * 2 ) );
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var backBrush = new SolidBrush( Color.FromArgb( 50, 230, 60, 60 ) );
        g.FillRectangle( backBrush, delRect );

        using var pen = new Pen( Color.White, 1.5f );
        int cx = delRect.X + delRect.Width / 2;
        int cy = delRect.Y + delRect.Height / 2;
        g.DrawRectangle( pen, cx - 4, cy - 2, 8, 7 );
        g.DrawLine( pen, cx - 6, cy - 4, cx + 6, cy - 4 );
        g.DrawLine( pen, cx - 2, cy - 6, cx + 2, cy - 6 );
        g.DrawLine( pen, cx + 2, cy - 6, cx + 2, cy - 6 );

        using var borderPen = new Pen( Color.FromArgb( 100, Color.White ) );
        g.DrawRectangle( borderPen, delRect );
    }
}