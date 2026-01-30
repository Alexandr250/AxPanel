// ReSharper disable ConvertIfStatementToNullCoalescingExpression
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
// ReSharper disable UnusedMember.Global
// ReSharper disable ConvertToAutoProperty

using AxPanel.UI.ElementStyles;

namespace AxPanel.UI.Themes;

public class DarkTheme : ITheme
{
    private ButtonStyle _buttonStyle;
    private WindowStyle _windowStyle;
    private ContainerStyle _containerStyle;

    public WindowStyle WindowStyle
    {
        get
        {
            if (_windowStyle == null)
                _windowStyle = new WindowStyle();

            return _windowStyle;
        }
    }

    public ContainerStyle ContainerStyle
    {
        get
        {
            if (_containerStyle == null)
                _containerStyle = new ContainerStyle();

            return _containerStyle;
        }
    }

    public ButtonStyle ButtonStyle
    {
        get
        {
            if (_buttonStyle == null)
                _buttonStyle = new ButtonStyle();

            return _buttonStyle;
        }
    }
}
