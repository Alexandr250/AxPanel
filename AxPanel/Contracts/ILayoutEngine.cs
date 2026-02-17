using AxPanel.UI.Themes;
using AxPanel.UI.UserControls;

namespace AxPanel.Contracts;

public interface ILayoutEngine
{
    (Point Location, int Width) GetLayout(
        int index,
        int scrollValue,
        int containerWidth,
        IReadOnlyList<LaunchButtonView> allButtons,
        ITheme theme );

    int GetTotalContentHeight( IReadOnlyList<LaunchButtonView> allButtons, int containerWidth, ITheme theme );

    int GetIndexAt(
        Point mouseLocation,
        int scrollValue,
        int containerWidth,
        IReadOnlyList<LaunchButtonView> allButtons,
        ITheme theme );
}