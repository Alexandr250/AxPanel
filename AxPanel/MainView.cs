using AxPanel.UI.UserControls;
using System.Diagnostics;
using AxPanel.Model;
using AxPanel.SL;
using AxPanel.UI.Themes;

namespace AxPanel;

public partial class MainView : Form
{
    private Rectangle _btnMinRect;
    private Rectangle _btnCloseRect;
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

        var theme = new DarkTheme();
        BackColor = theme.WindowStyle.BackColor;

        // Кэшируем настройки, чтобы не читать конфиг при каждом движении окна
        _config = ConfigManager.ReadMainConfig();
        _headerHeight = _config?.HeaderHeight ?? 30;
        _borderWidth = _config?.BorderWidth ?? 5;

        MainContainer = new RootContainerView( theme );
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

        Resize += ( s, e ) => UpdateContainerBounds();

        FormClosed += ( s, e ) =>
        {
            _activeDotBrush?.Dispose();
            _textBrush?.Dispose();
        };
    }

    private void UpdateContainerBounds()
    {
        if ( MainContainer == null ) return;

        // Читаем конфиг( предположим, он доступен через ConfigManager или поле )

        var config = ConfigManager.ReadMainConfig();

        int headerHeight = config?.HeaderHeight ?? 30;
        int borderWidth = config?.BorderWidth ?? 5;

        _btnMinRect = new Rectangle( ClientSize.Width - BtnWidth * 2, 0, BtnWidth, headerHeight );
        _btnCloseRect = new Rectangle( ClientSize.Width - BtnWidth, 0, BtnWidth, headerHeight );


        MainContainer.Top = headerHeight;
        MainContainer.Left = borderWidth;
        MainContainer.Width = ClientSize.Width - ( borderWidth * 2 ); 
        MainContainer.Height = ClientSize.Height - headerHeight - borderWidth;
    }

    // Нативное перемещение (заменяет ваш MouseMove)
    protected override void OnMouseDown( MouseEventArgs e )
    {
        base.OnMouseDown( e );

        if ( e.Button == MouseButtons.Left )
        {
            if ( _hoverBtn == 1 ) { this.WindowState = FormWindowState.Minimized; return; }
            if ( _hoverBtn == 2 ) { Application.Exit(); return; }

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
        else _hoverBtn = 0;

        if ( oldHover != _hoverBtn ) 
            Invalidate( new Rectangle( Width - BtnWidth * 2, 0, BtnWidth * 2, _headerHeight ) );
    }

    protected override void OnMouseLeave( EventArgs e )
    {
        base.OnMouseLeave( e );
        _hoverBtn = 0;
        Invalidate( new Rectangle( Width - BtnWidth * 2, 0, BtnWidth * 2, _headerHeight ) );
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

    protected override void OnResize( EventArgs e )
    {
        SuspendLayout(); // Приостанавливаем логику макета
        base.OnResize( e );
        ResumeLayout();
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
        using ( var headerBrush = new SolidBrush( Color.FromArgb( 45, 45, 48 ) ) )
        {
            g.FillRectangle( headerBrush, 0, 0, this.Width, h );
        }

        // 2. Нижняя граница-разделитель
        using ( var pen = new Pen( Color.FromArgb( 60, 60, 60 ) ) )
        {
            g.DrawLine( pen, 0, h - 1, this.Width, h - 1 );
        }

        // 3. Текст заголовка (Title)
        //TextRenderer.DrawText( g, "AX-PANEL v1.0", this.Font,
        //    new Rectangle( 15, 0, 200, h ), Color.Gray,
        //    TextFormatFlags.VerticalCenter | TextFormatFlags.Left );

        _btnMinRect = new Rectangle( Width - BtnWidth * 2, 0, BtnWidth, h );
        _btnCloseRect = new Rectangle( Width - BtnWidth, 0, BtnWidth, h );

        // Кнопка Свернуть
        if ( _hoverBtn == 1 ) 
            e.Graphics.FillRectangle( new SolidBrush( Color.FromArgb( 40, Color.White ) ), _btnMinRect );

        using ( var p = new Pen( Color.White, 1 ) )
            e.Graphics.DrawLine( p, _btnMinRect.X + 17, h / 2, _btnMinRect.X + 28, h / 2 );

        // Кнопка Закрыть
        if ( _hoverBtn == 2 ) e.Graphics.FillRectangle( Brushes.Crimson, _btnCloseRect );
        
        using ( var p = new Pen( Color.White, 1 ) )
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

            using var tempBrush = new SolidBrush( Color.FromArgb( alpha, Color.OrangeRed ) );
            g.FillEllipse( tempBrush, x + ( i * ( dotSize + gap ) ), y, dotSize, dotSize );
        }

        // Отрисовка текста
        using var tempTextBrush = new SolidBrush( Color.FromArgb( ( int )( 150 * _visualAlpha ), Color.White ) );
        g.DrawString( $"ВЫХОД: {_leftClicksCount}/{LeftClicksToExit}", Font, tempTextBrush, x + 45, y - 4 );
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
            if ( _btnMinRect.Contains( pos ) || _btnCloseRect.Contains( pos ) )
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