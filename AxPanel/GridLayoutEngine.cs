using System.Diagnostics;
using AxPanel.Contracts;
using AxPanel.UI.Themes;
using AxPanel.UI.UserControls;

namespace AxPanel
{
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
                var prevBtn = allButtons[ i ];
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

            var currentBtn = allButtons[ index ];
            if ( currentBtn.IsSeparator )
            {
                if ( currentCol > 0 ) currentY += theme.ButtonStyle.DefaultHeight + sHeight;
                return (new Point( sWidth, currentY ), containerWidth - ( sWidth * 2 ));
            }

            int x = sWidth + ( currentCol * ( btnWidth + sWidth ) );
            return (new Point( x, currentY ), btnWidth);
        }

        //public (Point Location, int Width) GetLayout( int index, int scrollValue, int containerWidth, IReadOnlyList<LaunchButtonView> allButtons, ITheme theme )
        //{
        //    int columns = Math.Max( 1, containerWidth / ( _defaultButtonWidth + Gap ) );
        //    int btnWidth = ( containerWidth - ( Gap * ( columns + 1 ) ) ) / columns;

        //    int currentY = theme.ContainerStyle.HeaderHeight + Gap + scrollValue;
        //    int currentCol = 0;

        //    for ( int i = 0; i < index; i++ )
        //    {
        //        var prevBtn = allButtons[ i ];

        //        if ( prevBtn.IsSeparator )
        //        {
        //            // Разделитель всегда завершает текущую строку (если она была) и добавляет свою высоту
        //            // Если currentCol > 0, значит до разделителя были кнопки, которые уже заняли строку
        //            if ( currentCol > 0 ) currentY += theme.ButtonStyle.DefaultHeight + Gap;

        //            currentY += prevBtn.Height + Gap;
        //            currentCol = 0;
        //        }
        //        else
        //        {
        //            currentCol++;
        //            if ( currentCol >= columns )
        //            {
        //                currentCol = 0;
        //                currentY += prevBtn.Height + Gap; // Здесь prevBtn.Height — это высота обычной кнопки
        //            }
        //        }
        //    }

        //    // Если мы дошли до нужного индекса, а currentCol > 0, 
        //    // значит мы стоим в новой строке, Y которой уже вычислен верно.

        //    var currentBtn = allButtons[ index ];

        //    if ( currentBtn.IsSeparator )
        //    {
        //        // Если перед разделителем остались кнопки в неполном ряду, 
        //        // разделитель должен прыгнуть под них
        //        if ( currentCol > 0 ) currentY += theme.ButtonStyle.DefaultHeight + Gap;

        //        return (new Point( Gap, currentY ), containerWidth - ( Gap * 2 ));
        //    }
        //    else
        //    {
        //        int x = Gap + ( currentCol * ( btnWidth + Gap ) );
        //        return (new Point( x, currentY ), btnWidth);
        //    }
        //}

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
                var btn = allButtons[ i ];

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
    }
}
