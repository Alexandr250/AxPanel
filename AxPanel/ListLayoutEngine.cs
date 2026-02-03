using AxPanel.Contracts;
using AxPanel.UI.Themes;
using AxPanel.UI.UserControls;

namespace AxPanel
{
    public class ListLayoutEngine : ILayoutEngine
    {
        public (Point Location, int Width) GetLayout( int index, int scrollValue, int containerWidth, IReadOnlyList<LaunchButtonView> allButtons, ITheme theme )
        {
            // В списке кнопки всегда занимают всю ширину (от края до края)
            int x = 0;
            int currentY = theme.ContainerStyle.HeaderHeight + scrollValue;

            // Суммируем высоты всех предыдущих кнопок
            for ( int i = 0; i < index; i++ )
            {
                currentY += allButtons[ i ].Height + theme.ButtonStyle.SpaceHeight;
            }

            // Возвращаем координаты текущей кнопки
            return (new Point( x, currentY ), containerWidth);
        }

        public int GetTotalContentHeight( IReadOnlyList<LaunchButtonView> allButtons, int containerWidth, ITheme theme )
        {
            if ( allButtons.Count == 0 ) return theme.ContainerStyle.HeaderHeight;

            int totalHeight = theme.ContainerStyle.HeaderHeight;

            // Суммируем высоты всех кнопок с учетом межстрочного интервала
            foreach ( var btn in allButtons )
            {
                totalHeight += btn.Height + theme.ButtonStyle.SpaceHeight;
            }

            return totalHeight;
        }
    }
}
