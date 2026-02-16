using AxPanel.SL;
using AxPanel.UI.Themes;

namespace AxPanel.UI.UserControls;

public class FooterButton
{
    private readonly ITheme _theme;
    public int Index { get; }
    public string Icon { get; }
    public string Text { get; }
    public Action Action { get; }
    public string Tooltip { get; }

    public FooterButton( int index, string icon, string text, string tooltip, Action action, ITheme theme )
    {
        Index = index;
        Icon = icon;
        Text = text;
        Tooltip = tooltip;
        Action = action;
        _theme = theme;
    }

    public static List<FooterButton> CreateStandardSet( ITheme theme )
    {
        return
        [
            new FooterButton( 0, "\uE7E8", "Завершение работы", "Выключение", ProcessManager.Shutdown, theme ),
            new FooterButton( 1, "\uE777", "Перезагрузка", "Перезагрузка", ProcessManager.Restart, theme ),
            new FooterButton( 2, "\uE708", "Спящий режим", "Спящий режим", ProcessManager.Sleep, theme )
        ];
    }

    public Rectangle GetRect( int containerWidth, int containerHeight, int footerHeight )
    {
        int count = 3;
        int btnWidth = containerWidth / count;
        int x = Index * btnWidth;
        int actualWidth = ( Index == count - 1 ) ? containerWidth - x : btnWidth;

        return new Rectangle( x, containerHeight - footerHeight, actualWidth, footerHeight );
    }

    public void Paint( Graphics g, Rectangle rect, Point mousePos )
    {
        bool isHovered = rect.Contains( mousePos );

        // 1. Фон при наведении
        if ( isHovered )
        {
            Color hoverColor = ( Index == 0 ) ? _theme.WindowStyle.FooterCloseBtnHoverColor : _theme.WindowStyle.FooterBtnHoverColor;
            using var brush = new SolidBrush( hoverColor );
            g.FillRectangle( brush, rect );

            // Рамки (псевдо-3D или плоские из темы)
            g.DrawLine( _theme.WindowStyle.FooterBtnBorderDarkPen, rect.X, rect.Y, rect.Right - 1, rect.Y );
            g.DrawLine( _theme.WindowStyle.FooterBtnBorderLightPen, rect.X, rect.Bottom - 1, rect.Right - 1, rect.Bottom - 1 );
        }

        // 2. Контент
        string content = isHovered ? Text : Icon;
        Font font = isHovered ? _theme.WindowStyle.FooterTextFont : _theme.WindowStyle.FooterIconFont;

        TextRenderer.DrawText( g, content, font, rect, _theme.WindowStyle.FooterTextColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis );

        // 3. Разделитель (рисует только кнопка 0 и 1)
        if ( Index < 2 )
        {
            using var pen = new Pen( _theme.WindowStyle.FooterSeparatorColor );
            g.DrawLine( pen, rect.Right - 1, rect.Y + 5, rect.Right - 1, rect.Bottom - 5 );
        }
    }
}
