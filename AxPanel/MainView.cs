using AxPanel.UI.UserControls;
using System.Diagnostics;
using AxPanel.Model;
using AxPanel.SL;
using AxPanel.UI.Themes;
using static AxPanel.Win32Api;
using System.Runtime.InteropServices;

namespace AxPanel;

public partial class MainView : Form
{
    private ITheme _theme;
    private Rectangle _btnMinRect;
    private Rectangle _btnCloseRect;
    private Rectangle _btnTopMostRect;
    private bool _isTopMost = false;
    private int _hoverBtn = 0; // 0 - нет, 1 - сворачивание, 2 - закрытие
    private const int BtnWidth = 45;

    private readonly int _headerHeight;
    private readonly int _borderWidth;

    private const int LeftClicksToExit = 3;
    private const int GripSideWidth = 16;
    private int _leftClicksCount = 0;
    private float _visualAlpha = 0f; // Прозрачность индикатора

    private bool _mouseDown;
    private Point _lastLocation;

    private SolidBrush _activeDotBrush = new SolidBrush( Color.OrangeRed );
    private SolidBrush _inactiveDotBrush = new SolidBrush( Color.FromArgb( 60, Color.OrangeRed ) );
    private SolidBrush _textBrush = new SolidBrush( Color.White );

    private MainConfig? _config;

    public MainModel MainModel { get; set; }

    public MainView()
    {
        InitializeComponent();
        DoubleBuffered = true; // Убирает мерцание

        //this.BackColor = Color.Black; // DWM использует черный как "ключ" для прозрачности Mica
        //this.AllowTransparency = true; // Только для Form

        // Кэшируем настройки, чтобы не читать конфиг при каждом движении окна
        _config = ConfigManager.GetMainConfig();
        _headerHeight = _config?.HeaderHeight ?? 30;
        _borderWidth = _config?.BorderWidth ?? 5;

        _theme = ConfigManager.LoadTheme( _config.ThemeFileName );
        BackColor = _theme.WindowStyle.BackColor;

        MainContainer = new RootContainerView( _theme, _config );
        MainContainer.OnSaveConfigRequered += () =>
        {
            ConfigManager.SaveMainConfig( _config );
            if( MainModel != null )
            {
                ConfigManager.SaveItemsConfig( MainModel );
            }
        };

        UpdateContainerBounds();
        MainContainer.Visible = true;
        Controls.Add( MainContainer );

        // Resize += ( s, e ) => UpdateContainerBounds();

        FormClosed += ( s, e ) =>
        {
            _activeDotBrush?.Dispose();
            _textBrush?.Dispose();
        };
    }

    private void UpdateContainerBounds()
    {
        if ( MainContainer == null ) 
            return;

        var config = ConfigManager.GetMainConfig();

        int headerHeight = config?.HeaderHeight ?? 30;
        int borderWidth = config?.BorderWidth ?? 5;

        _btnTopMostRect = new Rectangle( Width - BtnWidth * 3, 0, BtnWidth, headerHeight );
        _btnMinRect = new Rectangle( ClientSize.Width - BtnWidth * 2, 0, BtnWidth, headerHeight );
        _btnCloseRect = new Rectangle( ClientSize.Width - BtnWidth, 0, BtnWidth, headerHeight );
        
        MainContainer.Top = headerHeight;
        MainContainer.Left = borderWidth;
        MainContainer.Width = ClientSize.Width - borderWidth * 2; 
        MainContainer.Height = ClientSize.Height - headerHeight - borderWidth;
    }

    protected override void OnMouseDown( MouseEventArgs e )
    {
        base.OnMouseDown( e );

        if ( e.Button == MouseButtons.Left )
        {
            if ( _hoverBtn == 1 ) { this.WindowState = FormWindowState.Minimized; return; }
            if ( _hoverBtn == 2 ) { Application.Exit(); return; }
            if ( _hoverBtn == 3 )
            {
                _isTopMost = !_isTopMost;
                TopMost = _isTopMost;
                Invalidate( new Rectangle( Width - BtnWidth * 3, 0, BtnWidth * 3, _headerHeight ) );
                return;
            }

            HandleExitClick(); // Тройной клик

            Capture = false;

            // Имитируем стандартное поведение Windows: "захват" окна за заголовок.
            // Message.Create создает системное сообщение:
            // 1. this.Handle — указатель на текущее окно.
            // 2. 0xA1 (WM_NCLBUTTONDOWN) — сообщает системе, что нажата левая кнопка мыши в неклиентской (служебной) области.
            // 3. new IntPtr(2) (HTCAPTION) — уточняет, что клик пришелся именно на заголовок окна.
            // 4. IntPtr.Zero — дополнительные параметры (здесь не требуются).
            // После передачи этого сообщения в WndProc(ref m), Windows берет на себя 
            // всю логику перемещения окна, включая плавность, прилипание к краям и работу на нескольких мониторах.
            Message m = Message.Create( Handle, 0xA1, new IntPtr( 2 ), IntPtr.Zero );
            WndProc( ref m );
        }
    }

    protected override void OnMouseUp( MouseEventArgs e )
    {
        base.OnMouseUp( e );
        _mouseDown = false;
    }

    protected override void OnMouseMove( MouseEventArgs e )
    {
        base.OnMouseMove( e );

        // Проверка наведения на кнопки
        int oldHover = _hoverBtn;
        if ( _btnMinRect.Contains( e.Location ) ) _hoverBtn = 1;
        else if ( _btnCloseRect.Contains( e.Location ) ) _hoverBtn = 2;
        else if ( _btnTopMostRect.Contains( e.Location ) ) _hoverBtn = 3;
        else _hoverBtn = 0;

        if ( oldHover != _hoverBtn ) 
            Invalidate( new Rectangle( Width - BtnWidth * 3, 0, BtnWidth * 3, _headerHeight ) );
    }

    protected override void OnMouseLeave( EventArgs e )
    {
        base.OnMouseLeave( e );
        _hoverBtn = 0;
        Invalidate( new Rectangle( Width - BtnWidth * 3, 0, BtnWidth * 3, _headerHeight ) );
    }

    protected override void OnKeyDown( KeyEventArgs e )
    {
        base.OnKeyDown( e );
        //MainContainer?.RaiseKeyDown( e );
    }

    protected override void OnKeyUp( KeyEventArgs e )
    {
        base.OnKeyUp( e );
        //MainContainer?.RaiseKeyUp( e );
    }

    //protected override void OnResize( EventArgs e )
    //{
    //    SuspendLayout(); // Приостанавливаем логику макета
    //    base.OnResize( e );
    //    ResumeLayout();

    //    if( _config != null )
    //        ConfigManager.SaveMainConfig( _config );
    //}

    protected override void OnResize( EventArgs e )
    {
        // 1. Сначала даем базовому классу обработать изменение размера
        base.OnResize( e );

        // 2. Выполняем пересчет размеров контейнера и кнопок
        if ( MainContainer != null && _config != null )
        {
            // Используем значения строго из конфига, как вы и просили
            int h = _config.HeaderHeight;
            int bw = _config.BorderWidth;

            // Обновляем прямоугольники кнопок (для отрисовки и кликов)
            _btnTopMostRect = new Rectangle( ClientSize.Width - BtnWidth * 3, 0, BtnWidth, h );
            _btnMinRect = new Rectangle( ClientSize.Width - BtnWidth * 2, 0, BtnWidth, h );
            _btnCloseRect = new Rectangle( ClientSize.Width - BtnWidth, 0, BtnWidth, h );

            // Устанавливаем положение и размер контейнера за один проход
            // Это предотвращает двойную перерисовку дочерних элементов
            MainContainer.Bounds = new Rectangle(
                bw,
                h,
                Math.Max( 0, ClientSize.Width - bw * 2 ),
                Math.Max( 0, ClientSize.Height - h - bw )
            );

            Invalidate( new Rectangle( 0, 0, Width, _headerHeight ) );
        }

        // 3. Сохраняем конфиг (если в этом есть логика сохранения размеров самого окна)
        if ( _config != null )
            ConfigManager.SaveMainConfig( _config );
    }

    private void HandleExitClick()
    {
        TimerClicksCounter.Stop();
        _leftClicksCount++;
        _visualAlpha = 1.0f;

        // Перерисовываем только область индикатора
        Invalidate( new Rectangle( 0, 0, Width, _headerHeight ) );

        if( _leftClicksCount >= LeftClicksToExit )
            Application.Exit();
        else
            TimerClicksCounter.Start();
    }

    protected override void OnMouseClick( MouseEventArgs e )
    {
        // Сначала вызываем базовый метод для корректной работы событий
        base.OnMouseClick( e );

        if ( e.Button == MouseButtons.Right )
        {
            string dir = Path.GetDirectoryName( AppDomain.CurrentDomain.BaseDirectory );
            if ( dir != null ) Process.Start( "explorer.exe", dir );
        }
    }

    private void TimerClicksCounter_Tick( object sender, EventArgs e )
    {
        _leftClicksCount = 0;
        _visualAlpha = 0f; // Скрываем индикатор
        Invalidate( new Rectangle( 0, 0, Width, MainContainer.Top ) );
        TimerClicksCounter.Stop();
    }

    protected override void OnPaint( PaintEventArgs e )
    {
        base.OnPaint( e );
        var g = e.Graphics;
        int h = _headerHeight;

        // 1. Рисуем фон заголовка (чуть светлее основного фона для объема)
        using ( var headerBrush = new SolidBrush( _theme.WindowStyle.HeaderColor /*Color.FromArgb( 45, 45, 48 ) )*/ ) )
        {
            g.FillRectangle( headerBrush, 0, 0, this.Width, h );
        }

        // 2. Нижняя граница-разделитель
        using ( var pen = new Pen( /* Color.FromArgb( 60, 60, 60 ) */ _theme.WindowStyle.SeparatorColor ) )
        {
            g.DrawLine( pen, 0, h - 1, this.Width, h - 1 );
        }

        // 3. Текст заголовка (Title)
        //TextRenderer.DrawText( g, "AX-PANEL v1.0", this.Font,
        //    new Rectangle( 15, 0, 200, h ), Color.Gray,
        //    TextFormatFlags.VerticalCenter | TextFormatFlags.Left );

        _btnCloseRect = new Rectangle( Width - BtnWidth, 
            0, BtnWidth, h );
        _btnMinRect = new Rectangle( Width - BtnWidth * 2, 
            0, BtnWidth, h );
        _btnTopMostRect = new Rectangle( Width - BtnWidth * 3, 
            0, BtnWidth, h ); // Новая кнопка

        // --- ОТРИСОВКА КНОПКИ TOPMOST ---
        if ( _hoverBtn == 3 /*|| _isTopMost */ )
        {
            g.FillRectangle( _theme.WindowStyle.MinBtnHoverBrush, _btnTopMostRect );
            DrawPressedBorder( g, _btnTopMostRect, _theme ); // Метод для отрисовки впадины
        }

        using ( Pen p = new( _theme.WindowStyle.ControlIconColor, 1 ) )
        {
            int cx = _btnTopMostRect.X + BtnWidth / 2;
            int cy = h / 2;

            if ( _isTopMost )
            {
                g.DrawLine( p, cx, cy - 2, cx, cy + 8 );
                g.DrawRectangle( p, cx - 4, cy - 5, 8, 3 );
                g.DrawLine( p, cx - 6, cy - 6, cx + 6, cy - 6 );
            }
            else
            {
                g.DrawLine( p, cx - 1.4f, cy - 1.4f, cx + 5.6f, cy + 5.6f );

                PointF[] headPoints =
                [
                    new( cx - 6.3f, cy - 0.7f ),
                    new( cx - 0.7f, cy - 6.3f ),
                    new( cx + 1.4f, cy - 4.2f ),
                    new( cx - 4.2f, cy + 1.4f )
                ];
                g.DrawPolygon( p, headPoints );

                g.DrawLine( p, cx - 8.48f, cy, cx, cy - 8.48f );
            }
        }

        // Кнопка Свернуть
        if ( _hoverBtn == 1 )
        {
            g.FillRectangle( _theme.WindowStyle.MinBtnHoverBrush, _btnMinRect );
            DrawPressedBorder( g, _btnMinRect, _theme ); // Метод для отрисовки впадины
        }

        using ( var p = new Pen( _theme.WindowStyle.ControlIconColor, 1 ) )
            e.Graphics.DrawLine( p, _btnMinRect.X + 17, h / 2, _btnMinRect.X + 28, h / 2 );

        // Кнопка Закрыть
        if ( _hoverBtn == 2 )
        {
            g.FillRectangle( _theme.WindowStyle.CloseBtnHoverBrush, _btnCloseRect );
            DrawPressedBorder( g, _btnCloseRect, _theme );
        }

        using ( var p = new Pen( _theme.WindowStyle.ControlIconColor, 1 ) )
        {
            int cx = _btnCloseRect.X + BtnWidth / 2;
            int cy = h / 2;
            e.Graphics.DrawLine( p, cx - 5, cy - 5, cx + 5, cy + 5 );
            e.Graphics.DrawLine( p, cx + 5, cy - 5, cx - 5, cy + 5 );
        }

        // Если визуальный альфа-канал близок к нулю, ничего не рисуем (экономия ресурсов)
        if ( _leftClicksCount <= 0 || _visualAlpha < 0.05f ) 
            return;
        
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        const int dotSize = 6;
        const int gap = 8;
        int y = ( _headerHeight - dotSize ) / 2; // Используем закешированное поле
        int x = 15;

        // Отрисовка индикаторов
        for ( int i = 0; i < LeftClicksToExit; i++ )
        {
            bool isActive = i < _leftClicksCount;
            int alpha = ( int )( ( isActive ? 255 : 60 ) * _visualAlpha );

            using var tempBrush = new SolidBrush( Color.FromArgb( alpha, _theme.WindowStyle.ExitIndicatorColor ) );
            g.FillEllipse( tempBrush, x + ( i * ( dotSize + gap ) ), y, dotSize, dotSize );
        }

        // Отрисовка текста
        using var tempTextBrush = new SolidBrush( Color.FromArgb( ( int )( 150 * _visualAlpha ), _theme.WindowStyle.TitleColor ) );
        g.DrawString( $"ВЫХОД: {_leftClicksCount}/{LeftClicksToExit}", Font, tempTextBrush, x + 45, y - 4 );
        
        if ( _theme.WindowStyle.BorderWidth > 0 )
        {
            // 1. Внешний светлый контур (Блик)
            // Рисуем сверху и слева
            e.Graphics.DrawLine( _theme.WindowStyle.WindowBorderLightPen, 0, 0, Width, 0 );
            e.Graphics.DrawLine( _theme.WindowStyle.WindowBorderLightPen, 0, 0, 0, Height );

            // 2. Внешний темный контур (Тень)
            // Рисуем снизу и справа
            e.Graphics.DrawLine( _theme.WindowStyle.WindowBorderDarkPen, 0, Height - 1, Width, Height - 1 );
            e.Graphics.DrawLine( _theme.WindowStyle.WindowBorderDarkPen, Width - 1, 0, Width - 1, Height );
        }
    }

    private void DrawPressedBorder( Graphics g, Rectangle rect, ITheme theme )
    {
        // Тень сверху-слева (вдавленность)
        g.DrawLine( theme.WindowStyle.ControlBtnBorderDarkPen, rect.X, rect.Y, rect.Right - 1, rect.Y );
        g.DrawLine( theme.WindowStyle.ControlBtnBorderDarkPen, rect.X, rect.Y, rect.X, rect.Bottom - 1 );
        // Свет снизу-справа
        g.DrawLine( theme.WindowStyle.ControlBtnBorderLightPen, rect.X, rect.Bottom - 1, rect.Right - 1, rect.Bottom - 1 );
        g.DrawLine( theme.WindowStyle.ControlBtnBorderLightPen, rect.Right - 1, rect.Y, rect.Right - 1, rect.Bottom - 1 );
    }

    protected override void WndProc( ref Message m )
    {
        if ( m.Msg == Win32Api.WM_NCHITTEST )
        {
            // Извлекаем экранные координаты и переводим их в локальные (относительно окна)
            int x = ( int )( short )( m.LParam.ToInt32() & 0xFFFF );
            int y = ( int )( short )( ( m.LParam.ToInt32() >> 16 ) & 0xFFFF );
            Point pos = PointToClient( new Point( x, y ) );

            // Приоритет 1: Если мышь над кнопками управления — отдаем управление кнопкам
            if ( _btnMinRect.Contains( pos ) || _btnCloseRect.Contains( pos ) || _btnTopMostRect.Contains( pos ) )
            {
                m.Result = ( IntPtr )Win32Api.HTCLIENT;
                return;
            }

            // Приоритет 2: Если мышь в нижнем правом углу — включаем изменение размера
            if ( pos.X >= ClientSize.Width - GripSideWidth && pos.Y >= ClientSize.Height - GripSideWidth )
            {
                m.Result = ( IntPtr )Win32Api.HTBOTTOMRIGHT;
                return;
            }
        }

        base.WndProc( ref m );
    }
}