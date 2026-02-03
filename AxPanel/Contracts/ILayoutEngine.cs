using AxPanel.UI.Themes;
using AxPanel.UI.UserControls;

namespace AxPanel.Contracts
{
    public interface ILayoutEngine
    {
            // Теперь передаем список всех кнопок для анализа контекста
            (Point Location, int Width) GetLayout(
                int index,
                int scrollValue,
                int containerWidth,
                IReadOnlyList<LaunchButtonView> allButtons,
                ITheme theme );

            // Высота теперь считается на основе реального контента, а не просто числа
            int GetTotalContentHeight( IReadOnlyList<LaunchButtonView> allButtons, int containerWidth, ITheme theme );
    }
}
