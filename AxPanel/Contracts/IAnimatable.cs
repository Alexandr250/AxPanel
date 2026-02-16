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
    void UpdateVisual();
}

//public interface IAnimatable
//{
//    // Внутренности (уже есть)
//    IReadOnlyList<LaunchButtonView> Buttons { get; }
//    ILayoutEngine LayoutEngine { get; }
//    ITheme Theme { get; }
//    int ScrollValue { get; }
//    void UpdateVisual();

//    // Внешние свойства для аккордеона
//    int TargetHeight { get; set; }
//    int CurrentTop { get; set; }
//    int CurrentHeight { get; set; }
//    int CurrentWidth { get; set; }
//}
