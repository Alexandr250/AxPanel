using System.Diagnostics;
using AxPanel.Contracts;
using AxPanel.UI.Themes;
using AxPanel.UI.UserControls;

namespace AxPanel;

public class GridLayoutEngine : ILayoutEngine
{
    private int _defaultButtonWidth = 80;
    public int Gap { get; set; } = 3;

    public (Point Location, int Width) GetLayout( int index, int scrollValue, int containerWidth, IReadOnlyList<LaunchButtonView> allButtons, ITheme theme )
    {
        int sWidth = theme.ButtonStyle.SpaceWidth > 0 ? theme.ButtonStyle.SpaceWidth : Gap; ;
        int sHeight = theme.ButtonStyle.SpaceHeight;
        int targetWidth = theme.ButtonStyle.DefaultWidth > 0 ? theme.ButtonStyle.DefaultWidth : _defaultButtonWidth;

        // Рассчитываем количество колонок, учитывая ширину кнопки и отступ
        int columns = Math.Max( 1, containerWidth / ( targetWidth + sWidth ) );

        // Вычисляем реальную ширину кнопки, чтобы заполнить контейнер без остатка
        int btnWidth = ( containerWidth - ( sWidth * ( columns + 1 ) ) ) / columns;

        int currentY = theme.ContainerStyle.HeaderHeight + sHeight + scrollValue;
        int currentCol = 0;

        for ( int i = 0; i < index; i++ )
        {
            LaunchButtonView prevBtn = allButtons[ i ];
            if ( prevBtn.IsSeparator )
            {
                if ( currentCol > 0 ) currentY += theme.ButtonStyle.DefaultHeight + sHeight;
                currentY += prevBtn.Height + sHeight;
                currentCol = 0;
            }
            else
            {
                currentCol++;
                if ( currentCol >= columns )
                {
                    currentCol = 0;
                    currentY += theme.ButtonStyle.DefaultHeight + sHeight;
                }
            }
        }

        LaunchButtonView currentBtn = allButtons[ index ];
        if ( currentBtn.IsSeparator )
        {
            if ( currentCol > 0 ) currentY += theme.ButtonStyle.DefaultHeight + sHeight;
            return (new Point( sWidth, currentY ), containerWidth - ( sWidth * 2 ));
        }

        int x = sWidth + ( currentCol * ( btnWidth + sWidth ) );
        return (new Point( x, currentY ), btnWidth);
    }

    public int GetTotalContentHeight( IReadOnlyList<LaunchButtonView> allButtons, int containerWidth, ITheme theme )
    {
        if ( allButtons == null || allButtons.Count == 0 )
            return theme.ContainerStyle.HeaderHeight;

        int columns = Math.Max( 1, containerWidth / ( _defaultButtonWidth + Gap ) );

        // Начальная высота — заголовок контейнера + верхний отступ
        int totalHeight = theme.ContainerStyle.HeaderHeight + Gap;
        int currentCol = 0;

        for ( int i = 0; i < allButtons.Count; i++ )
        {
            LaunchButtonView btn = allButtons[ i ];

            if ( btn.IsSeparator )
            {
                // 1. Если до разделителя были кнопки в неполном ряду, закрываем этот ряд
                if ( currentCol > 0 )
                {
                    totalHeight += theme.ButtonStyle.DefaultHeight + Gap;
                    currentCol = 0;
                }

                // 2. Добавляем высоту самого разделителя
                totalHeight += btn.Height + Gap;
            }
            else
            {
                // Если это первая кнопка в ряду, резервируем под неё высоту
                if ( currentCol == 0 )
                {
                    totalHeight += btn.Height + Gap;
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

        int sWidth = theme.ButtonStyle.SpaceWidth > 0 ? theme.ButtonStyle.SpaceWidth : Gap;
        int sHeight = theme.ButtonStyle.SpaceHeight > 0 ? theme.ButtonStyle.SpaceHeight : Gap;
        int targetWidth = theme.ButtonStyle.DefaultWidth > 0 ? theme.ButtonStyle.DefaultWidth : _defaultButtonWidth;

        int columns = Math.Max( 1, containerWidth / ( targetWidth + sWidth ) );
        int btnWidth = ( containerWidth - ( sWidth * ( columns + 1 ) ) ) / columns;

        // Координаты мыши (или центра кнопки) с учетом прокрутки
        int centerX = mouseLocation.X;
        int centerY = mouseLocation.Y - scrollValue;

        int currentY = theme.ContainerStyle.HeaderHeight + sHeight;
        int currentCol = 0;

        for ( int i = 0; i < allButtons.Count; i++ )
        {
            LaunchButtonView btn = allButtons[ i ];
            Rectangle cellRect;

            if ( btn.IsSeparator )
            {
                // Если перед разделителем был неполный ряд — переходим на новую строку
                if ( currentCol > 0 ) currentY += theme.ButtonStyle.DefaultHeight + sHeight;

                cellRect = new Rectangle( sWidth, currentY, containerWidth - ( sWidth * 2 ), btn.Height );

                // Если мышка выше середины разделителя — вставляем перед ним
                if ( centerY < cellRect.Top + cellRect.Height / 2 ) return i;

                currentY += btn.Height + sHeight;
                currentCol = 0;
            }
            else
            {
                int x = sWidth + ( currentCol * ( btnWidth + sWidth ) );
                cellRect = new Rectangle( x, currentY, btnWidth, theme.ButtonStyle.DefaultHeight );

                // Проверка попадания: если мышка выше нижней границы текущей ячейки 
                // И левее её правой границы — это наш целевой индекс
                if ( centerY < cellRect.Bottom && centerX < cellRect.Right ) return i;

                currentCol++;
                if ( currentCol >= columns )
                {
                    currentCol = 0;
                    currentY += theme.ButtonStyle.DefaultHeight + sHeight;
                }
            }
        }

        return allButtons.Count - 1;
    }
}