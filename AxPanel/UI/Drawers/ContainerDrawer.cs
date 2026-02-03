using AxPanel.UI.Themes;
using AxPanel.UI.UserControls;
using System.Drawing.Drawing2D;

namespace AxPanel.UI.Drawers;

public class ContainerDrawer
{
    private readonly ITheme _theme;

    public ContainerDrawer( ITheme theme )
    {
        _theme = theme;
    }

    /// <summary>
    /// Основной метод отрисовки контейнера
    /// </summary>
    public void Draw( ButtonContainerView container, LaunchButtonView draggedBtn, MouseState mouseState, PaintEventArgs e )
    {
        Graphics g = e.Graphics;

        // 1. Расчет базового прямоугольника заголовка
        Rectangle headerRect = new( 0, 0, container.Width - 1, _theme.ContainerStyle.HeaderHeight - 1 );

        // 2. Отрисовка фона и границ заголовка
        g.FillRectangle( _theme.ContainerStyle.HeaderBrush, headerRect );
        g.DrawLine( _theme.ContainerStyle.BorderLightPen, 0, 0, headerRect.Right, 0 );
        g.DrawLine( _theme.ContainerStyle.BorderLightPen, 0, 0, 0, headerRect.Bottom );
        g.DrawLine( _theme.ContainerStyle.BorderDarkPen, headerRect.Right, 0, headerRect.Right, headerRect.Bottom );
        g.DrawLine( _theme.ContainerStyle.BorderDarkPen, 0, headerRect.Bottom, headerRect.Right, headerRect.Bottom );

        // 3. Отрисовка названия панели
        using StringFormat format = new()
        {
            LineAlignment = StringAlignment.Center,
            Alignment = StringAlignment.Near,
            FormatFlags = StringFormatFlags.NoWrap
        };

        Rectangle textRect = new( 16, 0, container.Width - 32, _theme.ContainerStyle.HeaderHeight );
        g.DrawString( container.PanelName, _theme.ContainerStyle.Font, _theme.ContainerStyle.ForeBrush, textRect, format );

        // 4. Отрисовка кнопки удаления (если мышь над ней)
        if ( mouseState.MouseInDeleteButton )
        {
            DrawDeleteButton( g, container.Width );
        }

        // 5. Отрисовка ФАНТОМА (только если идет перетаскивание)
        if ( draggedBtn != null )
        {
            int index = container.Buttons.ToList().IndexOf( draggedBtn );
            if ( index != -1 )
            {
                var layout = container.LayoutEngine.GetLayout(
                    index,
                    container.ScrollValue,
                    container.Width,
                    container.Buttons,
                    _theme );

                // Передаем саму кнопку, чтобы вытянуть из нее иконку
                DrawPhantom( e.Graphics, new Rectangle( layout.Location, new Size( layout.Width, draggedBtn.Height ) ), draggedBtn );
            }
        }
    }

    private void DrawPhantom( Graphics g, Rectangle rect, LaunchButtonView btn )
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int radius = 6; // Радиус скругления

        using ( GraphicsPath path = GetRoundedRect( rect, radius ) )
        {
            // 1. Глубокий фон "ямы"
            using var bgBrush = new SolidBrush( Color.FromArgb( 50, 0, 0, 0 ) );
            g.FillPath( bgBrush, path );

            // 2. Эффект вдавленности (внутренняя тень)
            // Рисуем темную дугу сверху и слева
            //using ( var darkPen = new Pen( Color.FromArgb( 120, 0, 0, 0 ), 1.5f ) )
            //{
            //    // Обрезаем область рисования только верхней левой частью для тени
            //    g.SetClip( path );
            //    g.DrawPath( darkPen, path );
            //    g.ResetClip();
            //}

            // 3. Тонкий пунктирный контур
            using ( var dashPen = new Pen( Color.FromArgb( 90, Color.Black ), 1 ) )
            {
                //dashPen.DashStyle = DashStyle.Dash;
                g.DrawPath( dashPen, path );
            }
        }

        // 4. Отрисовка контента (иконка и текст)
        DrawPhantomContent( g, rect, btn );
    }

    private void DrawPhantomContent( Graphics g, Rectangle rect, LaunchButtonView btn )
    {
        // Иконка
        if ( btn.Icon != null )
        {
            int iconSize = 32;
            int iconX = rect.X + ( rect.Width - iconSize ) / 2;
            int iconY = rect.Y + ( rect.Height / 2 ) - ( iconSize / 2 ) - 5;
            ControlPaint.DrawImageDisabled( g, btn.Icon.ToBitmap(), iconX, iconY, Color.Transparent );
        }

        // Текст
        if ( !string.IsNullOrEmpty( btn.Text ) )
        {
            using var font = new Font( "Segoe UI", 7.5f );
            using var textBrush = new SolidBrush( Color.FromArgb( 110, Color.Gray ) );
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Far,
                Trimming = StringTrimming.EllipsisCharacter
            };
            Rectangle textRect = new Rectangle( rect.X + 4, rect.Y, rect.Width - 8, rect.Height - 4 );
            g.DrawString( btn.Text, font, textBrush, textRect, format );
        }
    }

    // Хелпер для создания скругленного прямоугольника
    private GraphicsPath GetRoundedRect( Rectangle bounds, int radius )
    {
        GraphicsPath path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc( bounds.X, bounds.Y, d, d, 180, 90 );
        path.AddArc( bounds.Right - d, bounds.Y, d, d, 270, 90 );
        path.AddArc( bounds.Right - d, bounds.Bottom - d, d, d, 0, 90 );
        path.AddArc( bounds.X, bounds.Bottom - d, d, d, 90, 90 );
        path.CloseFigure();
        return path;
    }

    private void DrawDeleteButton( Graphics g, int containerWidth )
    {
        int buttonSize = _theme.ContainerStyle.ButtonSize;
        int margin = _theme.ContainerStyle.ButtonMargin;
        int x = containerWidth - buttonSize - margin;
        int y = ( _theme.ContainerStyle.HeaderHeight - buttonSize ) / 2;

        Rectangle deleteRect = new( x, y, buttonSize, buttonSize );

        // Фон кнопки удаления
        g.FillRectangle( _theme.ContainerStyle.ButtonSelectedBrush, deleteRect );

        // Крестик
        using var crossPen = new Pen( Color.White, 1.5f );
        int pad = 5;
        g.DrawLine( crossPen, x + pad, y + pad, x + buttonSize - pad, y + buttonSize - pad );
        g.DrawLine( crossPen, x + buttonSize - pad, y + pad, x + pad, y + buttonSize - pad );
    }
}
