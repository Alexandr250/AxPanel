using AxPanel.UI.Themes;
using AxPanel.UI.UserControls;

namespace AxPanel.UI.Drawers;

public class ContainerDrawer
{
    private readonly ITheme _theme;

    public ContainerDrawer( ITheme theme )
    {
        _theme = theme;
    }

    public void Draw( BaseControl control, MouseState mouseState, PaintEventArgs e )
    {
        Graphics g = e.Graphics;

        using StringFormat format = new();

        format.LineAlignment = StringAlignment.Center;
        format.Alignment = StringAlignment.Near;
        format.FormatFlags = StringFormatFlags.NoWrap;

        Rectangle rect = new( 0, 0, control.Width - 1, /*Height - 1*/ _theme.ContainerStyle.HeaderHeight - 1 );

        g.FillRectangle( _theme.ContainerStyle.HeaderBrush, rect );
        g.DrawLine( _theme.ContainerStyle.BorderLightPen, 0, 0, rect.Right, 0 );
        g.DrawLine( _theme.ContainerStyle.BorderLightPen, 0, 0, 0, rect.Bottom );
        g.DrawLine( _theme.ContainerStyle.BorderDarkPen, rect.Right, 0, rect.Right, rect.Bottom );
        g.DrawLine( _theme.ContainerStyle.BorderDarkPen, 0, rect.Bottom, rect.Right, rect.Bottom );

        rect = new Rectangle( 0 + 16, 0, control.Width - 1 - 16, /*Height - 1*/ _theme.ContainerStyle.HeaderHeight );
        g.DrawString( control.Path, _theme.ContainerStyle.Font, _theme.ContainerStyle.ForeBrush, rect, format );
    }
}