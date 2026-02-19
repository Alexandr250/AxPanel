using AxPanel.Contracts;
using AxPanel.UI.Themes;
using AxPanel.UI.UserControls;

namespace AxPanel;

public class GridLayoutEngine : ILayoutEngine
{
    private const int _defaultButtonWidth = 80;
    public int Gap { get; set; } = 3;

    public (Point Location, int Width) GetLayout( int index, int scrollValue, int containerWidth, IReadOnlyList<LaunchButtonView> allButtons, ITheme theme )
    {
        int spaceWidth = theme.ButtonStyle.SpaceWidth > 0 ? theme.ButtonStyle.SpaceWidth : Gap;
        int spaceHeight = theme.ButtonStyle.SpaceHeight;
        int targetWidth = theme.ButtonStyle.DefaultWidth > 0 ? theme.ButtonStyle.DefaultWidth : _defaultButtonWidth;

        // Рассчитываем количество колонок, учитывая ширину кнопки и отступ
        int columnsCount = Math.Max( 1, containerWidth / ( targetWidth + spaceWidth ) );

        // Вычисляем реальную ширину кнопки, чтобы заполнить контейнер без остатка
        int buttonWidth = ( containerWidth - ( spaceWidth * ( columnsCount + 1 ) ) ) / columnsCount;

        int currentY = theme.ContainerStyle.HeaderHeight + spaceHeight + scrollValue;
        int currentCol = 0;

        for ( int i = 0; i < index; i++ )
        {
            LaunchButtonView prevBtn = allButtons[ i ];
            if ( prevBtn.IsSeparator )
            {
                if ( currentCol > 0 ) currentY += theme.ButtonStyle.DefaultHeight + spaceHeight;
                currentY += theme.ButtonStyle.SeparatorHeight + spaceHeight;
                currentCol = 0;
            }
            else
            {
                currentCol++;
                if ( currentCol >= columnsCount )
                {
                    currentCol = 0;
                    currentY += theme.ButtonStyle.DefaultHeight + spaceHeight;
                }
            }
        }

        LaunchButtonView currentBtn = allButtons[ index ];
        if ( currentBtn.IsSeparator )
        {
            currentBtn.Height = theme.ButtonStyle.SeparatorHeight;
            if ( currentCol > 0 ) 
                currentY += theme.ButtonStyle.DefaultHeight + spaceHeight;

            return ( new Point( spaceWidth, currentY ), containerWidth - ( spaceWidth * 2 ) );
        }

        currentBtn.Height = theme.ButtonStyle.DefaultHeight;

        int x = spaceWidth + ( currentCol * ( buttonWidth + spaceWidth ) );
        return (new Point( x, currentY ), buttonWidth);
    }

    public int GetTotalContentHeight( IReadOnlyList<LaunchButtonView> allButtons, int containerWidth, ITheme theme )
    {
        if ( allButtons == null || allButtons.Count == 0 )
            return theme.ContainerStyle.HeaderHeight;

        int columns = Math.Max( 1, containerWidth / ( _defaultButtonWidth + Gap ) );

        // Начальная высота — заголовок контейнера + верхний отступ
        int totalHeight = theme.ContainerStyle.HeaderHeight + Gap;
        int currentCol = 0;

        foreach( LaunchButtonView button in allButtons )
        {
            if ( button.IsSeparator )
            {
                // 1. Если до разделителя были кнопки в неполном ряду, закрываем этот ряд
                if ( currentCol > 0 )
                {
                    totalHeight += theme.ButtonStyle.DefaultHeight + Gap;
                    currentCol = 0;
                }

                // 2. Добавляем высоту самого разделителя
                totalHeight += theme.ButtonStyle.SeparatorHeight + Gap;
            }
            else
            {
                // Если это первая кнопка в ряду, резервируем под неё высоту
                if ( currentCol == 0 )
                {
                    totalHeight += theme.ButtonStyle.DefaultHeight + Gap;
                }

                currentCol++;

                // Если ряд заполнился, сбрасываем счетчик колонок
                if ( currentCol >= columns )
                {
                    currentCol = 0;
                }
            }
        }

        // Если список закончился на неполном ряду кнопок, высота уже учтена в (currentCol == 0)
        return totalHeight + Gap;
    }

    public int GetIndexAt( Point mouseLocation, int scrollValue, int containerWidth, IReadOnlyList<LaunchButtonView> allButtons, ITheme theme )
    {
        if ( allButtons.Count <= 1 ) return 0;

        int spaceWidth = theme.ButtonStyle.SpaceWidth > 0 ? theme.ButtonStyle.SpaceWidth : Gap;
        int spaceHeight = theme.ButtonStyle.SpaceHeight > 0 ? theme.ButtonStyle.SpaceHeight : Gap;
        int targetWidth = theme.ButtonStyle.DefaultWidth > 0 ? theme.ButtonStyle.DefaultWidth : _defaultButtonWidth;

        int columns = Math.Max( 1, containerWidth / ( targetWidth + spaceWidth ) );
        int btnWidth = ( containerWidth - ( spaceWidth * ( columns + 1 ) ) ) / columns;

        // Координаты мыши (или центра кнопки) с учетом прокрутки
        int centerX = mouseLocation.X;
        int centerY = mouseLocation.Y - scrollValue;

        int currentY = theme.ContainerStyle.HeaderHeight + spaceHeight;
        int currentCol = 0;

        for ( int i = 0; i < allButtons.Count; i++ )
        {
            LaunchButtonView btn = allButtons[ i ];
            Rectangle cellRect;

            if ( btn.IsSeparator )
            {
                // Если перед разделителем был неполный ряд — переходим на новую строку
                if ( currentCol > 0 ) 
                    currentY += theme.ButtonStyle.DefaultHeight + spaceHeight;

                cellRect = new Rectangle( spaceWidth, currentY, containerWidth - ( spaceWidth * 2 ), theme.ButtonStyle.SeparatorHeight );

                // Если мышка выше середины разделителя — вставляем перед ним
                if ( centerY < cellRect.Top + cellRect.Height / 2 ) 
                    return i;

                currentY += theme.ButtonStyle.SeparatorHeight + spaceHeight;
                currentCol = 0;
            }
            else
            {
                int x = spaceWidth + ( currentCol * ( btnWidth + spaceWidth ) );
                cellRect = new Rectangle( x, currentY, btnWidth, theme.ButtonStyle.DefaultHeight );

                // Проверка попадания: если мышка выше нижней границы текущей ячейки 
                // И левее её правой границы — это наш целевой индекс
                if ( centerY < cellRect.Bottom && centerX < cellRect.Right ) 
                    return i;

                currentCol++;

                if ( currentCol >= columns )
                {
                    currentCol = 0;
                    currentY += theme.ButtonStyle.DefaultHeight + spaceHeight;
                }
            }
        }

        return allButtons.Count - 1;
    }
}