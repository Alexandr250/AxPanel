using AxPanel.Contracts;
using AxPanel.UI.Themes;
using AxPanel.UI.UserControls;

namespace AxPanel
{
    public class ListLayoutEngine : ILayoutEngine
    {
        public (Point Location, int Width) GetLayout( int index, int scrollValue, int containerWidth, LaunchButton btn, ITheme theme )
        {
            // В списке кнопка всегда прижата к левому краю
            int x = 0;

            // Позиция Y зависит только от индекса
            int y = theme.ContainerStyle.HeaderHeight +
                   ( btn.Height + theme.ButtonStyle.SpaceHeight ) * index +
                   scrollValue;

            // Ширина кнопки — это вся ширина контейнера
            int width = containerWidth;

            return (new Point( x, y ), width);
        }

        public int GetTotalContentHeight( int itemsCount, ITheme theme )
        {
            return theme.ContainerStyle.HeaderHeight +
                   ( theme.ButtonStyle.DefaultHeight + theme.ButtonStyle.SpaceHeight ) * itemsCount;
        }
    }
}
