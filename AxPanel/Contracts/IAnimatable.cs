using AxPanel.UI.Themes;
using AxPanel.UI.UserControls;

namespace AxPanel.Contracts;

public interface IAnimatable
{
    IReadOnlyList<LaunchButtonView> Buttons { get; }
    ILayoutEngine LayoutEngine { get; }
    ITheme Theme { get; }
    int ScrollValue { get; }
    int Width { get; }
    void UpdateVisual(); // Замена Invalidate для чистоты
}
