using AxPanel.UI.ElementStyles;

// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
// ReSharper disable ConvertIfStatementToNullCoalescingExpression

namespace AxPanel.UI.Themes;

public class OldTheme : ITheme
{
    private ButtonStyle _buttonStyle;
    private WindowStyle _windowStyle;
    private ContainerStyle _containerStyle;

    public WindowStyle WindowStyle
    {
        get
        {
            if ( _windowStyle == null )
                _windowStyle = new WindowStyle { BackColor = Color.LightGray };

            return _windowStyle;
        }
    }

    public ContainerStyle ContainerStyle
    {
        get
        {
            if( _containerStyle == null )
            {
                _containerStyle = new ContainerStyle
                {
                    BackColor = Color.FromArgb( 150, 150, 150 ),
                    HeaderBrush = new SolidBrush( Color.FromArgb( 0, 35, 100 ) ),
                    ForeBrush = Brushes.White,
                    BorderLightPen = new Pen( Color.FromArgb( 0, 50, 150 ) ),
                    BorderDarkPen = new Pen( Color.FromArgb( 0, 20, 55 ) )
                };
            }

            return _containerStyle;
        }
    }

    public ButtonStyle ButtonStyle
    {
        get
        {
            if( _buttonStyle == null )
            {
                _buttonStyle = new ButtonStyle
                {
                    UnselectedBrush = new SolidBrush( Color.FromArgb( 200, 200, 200 ) ),
                    SelectedBrush = new SolidBrush( Color.FromArgb( 220, 220, 220 ) ),
                    BorderLightPen = new Pen( Color.FromArgb( 230, 230, 230 ) ),
                    BorderDarkPen = new Pen( Color.FromArgb( 100, 100, 100 ) ),
                    MainFontBrush = Brushes.Black,
                    AdditionalFontBrush = Brushes.DimGray,
                    SelectedAltBrush = new SolidBrush( Color.FromArgb( 250, 200, 190 ) )
                };
            }

            return _buttonStyle;
        }
    }
}
