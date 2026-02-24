using AxPanel.Contracts;
using AxPanel.UI.Themes;
using AxPanel.UI.UserControls;

namespace AxPanel;

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

    public int GetIndexAt( Point mouseLocation, int scrollValue, int containerWidth, IReadOnlyList<LaunchButtonView> allButtons, ITheme theme )
    {
        if ( allButtons.Count <= 1 ) return 0;

        // Начальная точка (заголовок + прокрутка)
        int currentY = theme.ContainerStyle.HeaderHeight + scrollValue;
        int sHeight = theme.ButtonStyle.SpaceHeight > 0 ? theme.ButtonStyle.SpaceHeight : 3;

        // Нам важна только вертикальная координата мыши (или центра кнопки)
        int centerY = mouseLocation.Y;

        for ( int i = 0; i < allButtons.Count; i++ )
        {
            int btnHeight = allButtons[ i ].Height;

            // Если координата Y выше середины текущей кнопки
            if ( centerY < currentY + ( btnHeight / 2 ) )
            {
                return i;
            }

            // Переходим к следующей позиции
            currentY += btnHeight + sHeight;
        }

        // Если мы ниже всех кнопок
        return allButtons.Count - 1;
    }
}