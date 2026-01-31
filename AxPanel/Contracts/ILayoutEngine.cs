using AxPanel.UI.Themes;
using AxPanel.UI.UserControls;

namespace AxPanel.Contracts
{
    public interface ILayoutEngine
    {
        /// <summary>
        /// Вычисляет целевой Y (или X) для кнопки на основе её индекса и состояния прокрутки.
        /// </summary>
        //int GetTargetPosition( int index, int scrollValue, LaunchButton button, ITheme theme );
        
        // Возвращает: X, Y и Width для кнопки
        (Point Location, int Width) GetLayout( int index, int scrollValue, int containerWidth, LaunchButton btn, ITheme theme );

        /// <summary>
        /// Общая высота всего контента (нужна для ограничения прокрутки).
        /// </summary>
        int GetTotalContentHeight( int itemsCount, ITheme theme );
        
    }
}
