using AxPanel.UI.Themes;

namespace AxPanel.UI.UserControls;

public class Footer
{
    private readonly int _height;
    private readonly List<FooterButton> _buttons;
    private readonly ITheme _theme;

    public int Height => _height;

    public Footer( ITheme theme, int height = 25 )
    {
        _theme = theme;
        _height = height;
        _buttons = FooterButton.CreateStandardSet( theme );
    }

    public void Draw( Graphics g, int containerWidth, int containerHeight, Point mousePos )
    {
        // Отрисовка общего фона футера (опционально, если нужно)
        using var backBrush = new SolidBrush( _theme.WindowStyle.FooterColor );
        g.FillRectangle( backBrush, 0, containerHeight - _height, containerWidth, _height );

        foreach ( var btn in _buttons )
        {
            Rectangle rect = btn.GetRect( containerWidth, containerHeight, _height );
            btn.Paint( g, rect, mousePos );
        }
    }

    public FooterButton? HitTest( int containerWidth, int containerHeight, Point location )
    {
        return _buttons.FirstOrDefault( b =>
            b.GetRect( containerWidth, containerHeight, _height ).Contains( location ) );
    }

    public Rectangle GetFullBounds( int containerWidth, int containerHeight )
    {
        return new Rectangle( 0, containerHeight - _height, containerWidth, _height );
    }
}
