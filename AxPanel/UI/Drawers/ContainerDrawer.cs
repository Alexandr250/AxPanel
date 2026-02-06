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
        int radius = _theme.ContainerStyle.PhantomRadius; // Радиус скругления

        using( GraphicsPath path = GetRoundedRect( rect, radius ) )
        {
            // 1. Глубокий фон "ямы"
            g.FillPath( _theme.ContainerStyle.PhantomBgBrush, path );

            //2.Эффект вдавленности( внутренняя тень )
            g.SetClip( path );
            g.DrawPath( _theme.ContainerStyle.PhantomShadowPen, path );
            g.ResetClip();

            // 3. Тонкий пунктирный контур
            g.DrawPath( _theme.ContainerStyle.PhantomDashPen, path );
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
            using StringFormat format = new()
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Far,
                Trimming = StringTrimming.EllipsisCharacter
            };
            Rectangle textRect = new( rect.X + 4, rect.Y, rect.Width - 8, rect.Height - 4 );
            g.DrawString( btn.Text, _theme.ContainerStyle.PhantomFont, _theme.ContainerStyle.PhantomTextBrush, textRect, format );
        }
    }

    // Хелпер для создания скругленного прямоугольника
    private GraphicsPath GetRoundedRect( Rectangle bounds, int radius )
    {
        GraphicsPath path = new();

        if ( radius <= 0 )
        {
            path.AddRectangle( bounds );
            return path;
        }

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
        int pad = 5;
        g.DrawLine( _theme.ContainerStyle.DeleteBtnCrossPen, x + pad, y + pad, x + buttonSize - pad, y + buttonSize - pad );
        g.DrawLine( _theme.ContainerStyle.DeleteBtnCrossPen, x + buttonSize - pad, y + pad, x + pad, y + buttonSize - pad );
    }
}
