using AxPanel.Contracts;
using AxPanel.UI.Themes;
using AxPanel.UI.UserControls;

namespace AxPanel
{
    public class GridLayoutEngine : ILayoutEngine
    {
        /// <summary>
        /// Количество колонок в сетке
        /// </summary>
        public int Columns { get; set; } = 3;

        /// <summary>
        /// Межстрочный и межколоночный интервал (пиксели)
        /// </summary>
        public int Gap { get; set; } = 3;

        public (Point Location, int Width) GetLayout( int index, int scrollValue, int containerWidth, LaunchButton btn, ITheme theme )
        {
            // 1. Расчет доступной ширины для кнопок (вычитаем отступы между колонками)
            // Формула: (Вся ширина - отступы слева, справа и между кнопками) / кол-во колонок
            int totalGapsWidth = Gap * ( Columns + 1 );
            int btnWidth = ( containerWidth - totalGapsWidth ) / Columns;

            // 2. Определяем позицию в сетке
            int row = index / Columns; // Номер строки (0, 1, 2...)
            int col = index % Columns; // Номер столбца (0, 1, 2...)
            //int col = index % Columns; // Убедитесь, что Columns > 1
            //int x = Gap + ( col * ( btnWidth + Gap ) );

            // 3. Вычисляем X координату
            // Отступ слева + (ширина кнопки + отступ) * номер колонки
            int x = Gap + ( col * ( btnWidth + Gap ) );

            // 4. Вычисляем Y координату
            // Заголовок + (высота кнопки + отступ) * номер строки + прокрутка
            int y = theme.ContainerStyle.HeaderHeight +
                    ( btn.Height + Gap ) * row +
                    scrollValue;

            return (new Point( x, y ), btnWidth);
        }

        public int GetTotalContentHeight( int itemsCount, ITheme theme )
        {
            if ( itemsCount == 0 ) return theme.ContainerStyle.HeaderHeight;

            // Вычисляем количество строк
            int rows = ( int )Math.Ceiling( ( double )itemsCount / Columns );

            // Высота = Заголовок + (Высота кнопки + отступ) * количество строк
            return theme.ContainerStyle.HeaderHeight +
                   ( theme.ButtonStyle.DefaultHeight + Gap ) * rows + Gap;
        }
    }
}
