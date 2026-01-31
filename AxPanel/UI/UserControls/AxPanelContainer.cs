using AxPanel.Contracts;
using AxPanel.Model;
using AxPanel.SL;
using AxPanel.UI.Drawers;
using AxPanel.UI.Themes;
using System.Diagnostics;

namespace AxPanel.UI.UserControls;

public partial class AxPanelContainer : BasePanelControl
{
    // Поле для движка (по умолчанию список)
    public ILayoutEngine LayoutEngine { get; set; } = new GridLayoutEngine();

    // Сервисы и отрисовка
    private readonly ProcessMonitor _monitor;
    private readonly ContainerDrawer _containerDrawer;
    private readonly ITheme _theme;

    // UI Состояние
    private readonly System.Windows.Forms.Timer _animationTimer;
    private List<LaunchButton> _buttons = new();
    private int _scrollValue = 0;
    private int _itemsCount = 0;

    // События
    public event Action<AxPanelContainer> ContainerSelected;
    public event Action<List<LaunchItem>> ItemCollectionChanged;

    public string PanelName { get; set; }

    public AxPanelContainer( ITheme theme )
    {
        _theme = theme ?? throw new ArgumentNullException( nameof( theme ) );
        _containerDrawer = new ContainerDrawer( _theme );

        // Базовые стили применяются автоматически через BasePanelControl
        BackColor = _theme.ContainerStyle.BackColor;

        // 1. Инициализация мониторинга (Service Layer)
        _monitor = new ProcessMonitor();
        _monitor.StatisticsUpdated += OnStatsReceived;
        _monitor.Start();

        // 2. Инициализация анимации (~60 FPS)
        _animationTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _animationTimer.Tick += ( s, e ) => AnimateStep();
        _animationTimer.Start();

        // 3. Регистрация внешних интеграций
        ElevatedDragDropManager.Instance.EnableDragDrop( Handle );
        ElevatedDragDropManager.Instance.ElevatedDragDrop += OnElevatedDragDrop;

        MouseWheel += HandleMouseWheel;
    }

    #region Логика обновления состояния

    private void SyncState()
    {
        UpdateMonitorPaths();
        ReorderButtons();
    }

    private void UpdateMonitorPaths()
    {
        _monitor.TargetPaths = _buttons
            .Where( b => !string.IsNullOrEmpty( b.BaseControlPath ) )
            .Select( b => b.BaseControlPath )
            .ToHashSet( StringComparer.OrdinalIgnoreCase );
    }

    private void OnStatsReceived( Dictionary<string, ProcessStats> stats )
    {
        bool changed = false;
        foreach ( var btn in _buttons )
        {
            if ( btn.BaseControlPath != null && stats.TryGetValue( btn.BaseControlPath, out var s ) )
            {
                // Метод UpdateState внутри LaunchButton сам проверит дельту изменений
                btn.UpdateState( s.IsRunning, s.CpuUsage, s.RamMb, s.StartTime );
                changed = true;
            }
        }
        if ( changed ) Invalidate( true );
    }

    #endregion

    #region Анимация и позиционирование

    private void AnimateStep()
    {
        for ( int i = 0; i < _buttons.Count; i++ )
        {
            var btn = _buttons[ i ];
            if ( btn.Capture ) continue;

            // Получаем расчетные данные
            var layout = LayoutEngine.GetLayout( i, _scrollValue, this.Width, btn, _theme );

            // ТЕСТ: Нажмите F5 и посмотрите в окно Output (Вывод) в Visual Studio.
            // Если TargetX там не 0, значит математика работает, и проблема в WinForms.
            if ( i == 1 ) Debug.WriteLine( $"Btn 1: TargetX={layout.Location.X}, CurrentX={btn.Left}" );

            // Плавная интерполяция
            if ( Math.Abs( btn.Top - layout.Location.Y ) > 0.5 )
                btn.Top += ( layout.Location.Y - btn.Top ) / 4;

            if ( Math.Abs( btn.Left - layout.Location.X ) > 0.5 )
                btn.Left += ( layout.Location.X - btn.Left ) / 4;

            if ( Math.Abs( btn.Width - layout.Width ) > 0.5 )
                btn.Width += ( layout.Width - btn.Width ) / 4;
        }
    }

    public void ReorderButtons()
    {
        // Принудительный вызов, если таймер спит
        AnimateStep();
        Invalidate();
    }

    private void HandleMouseWheel( object sender, MouseEventArgs e )
    {
        int delta = e.Delta > 0 ? _theme.ContainerStyle.ScrollValueIncrement : -_theme.ContainerStyle.ScrollValueIncrement;

        int totalHeight = LayoutEngine.GetTotalContentHeight( _buttons.Count, _theme );
        int visibleHeight = this.Height;

        _scrollValue += delta;

        // Ограничение: не крутим выше начала и ниже конца контента
        int maxScroll = Math.Min( 0, visibleHeight - totalHeight );
        _scrollValue = Math.Clamp( _scrollValue, maxScroll, 0 );

        ReorderButtons();
    }
    #endregion

    #region Управление кнопками

    public void AddButtons( List<LaunchItem> items )
    {
        if ( items == null || items.Count == 0 ) return;
        CreateButtonControls( items );
        _itemsCount += items.Count;
        SyncState();
    }

    private void CreateButtonControls( List<LaunchItem> items )
    {
        // Сортируем входящие элементы по кликам и ID
        var ordered = items.OrderByDescending( i => i.ClicksCount ).ThenBy( i => i.Id );

        foreach ( var item in ordered )
        {
            var btn = new LaunchButton( _theme )
            {
                // Начальная ширина (будет скорректирована аниматором)
                Dock = DockStyle.None, // Прямой запрет на стыковку
                Anchor = AnchorStyles.Top | AnchorStyles.Left, // Фиксируем только левый верхний угол
                Width = this.Width,
                Height = item.Height > 0 ? item.Height : _theme.ButtonStyle.DefaultHeight,
                Text = item.Name,
                BaseControlPath = item.FilePath,
            };

            // Остальные подписки
            btn.ButtonLeftClick += b => StartProcess( b.BaseControlPath );
            btn.DeleteButtonClick += b => {
                _buttons.Remove( b ); // Убираем из внутреннего списка
                Controls.Remove( b );
                b.Dispose();
                SyncState(); // Обновляем пути монитора и вызываем пересчет
            };
            // Теперь клик правой кнопкой по ЛЮБОЙ кнопке переключит режим
            //btn.ButtonRightClick += b => {
            //    if ( LayoutEngine is ListLayoutEngine )
            //        LayoutEngine = new GridLayoutEngine { Columns = 3 };
            //    else
            //        LayoutEngine = new ListLayoutEngine();

            //    SyncState();
            //};


            btn.MouseMove += ( s, e ) =>
            {
                // Если кнопку тянут, ReorderButtons позволит аниматору знать, 
                // что нужно пересчитывать позиции
                if ( e.Button == MouseButtons.Left ) ReorderButtons();
            };

            _buttons.Add( btn );
            Controls.Add( btn );

            // Устанавливаем начальные координаты сразу через LayoutEngine,
            // чтобы кнопки не "прыгали" из нулевой точки при создании.
            int currentIndex = _buttons.Count - 1;
            var layout = LayoutEngine.GetLayout( currentIndex, _scrollValue, this.Width, btn, _theme );

            btn.Location = layout.Location;
            btn.Width = layout.Width;
        }

        // После добавления всех кнопок один раз пересчитываем их логическое состояние
        ReorderButtons();
    }

    

    private void StartProcess( string path, bool runAsAdmin = false )
    {
        try
        {
            if ( string.IsNullOrEmpty( path ) ) return;
            Process.Start( new ProcessStartInfo( path ) { UseShellExecute = true, Verb = runAsAdmin ? "runas" : "" } );

            // МГНОВЕННЫЙ ОТКЛИК: Находим кнопку и помечаем как запущенную
            var btn = _buttons.FirstOrDefault( b => b.BaseControlPath.Equals( path, StringComparison.OrdinalIgnoreCase ) );
            if ( btn != null ) { btn.IsRunning = true; btn.Invalidate(); }
        }
        catch ( Exception ex ) { Debug.WriteLine( $"Run error: {ex.Message}" ); }
    }

    #endregion

    #region Переопределения WinForms

    protected override void OnPaint( PaintEventArgs e ) => _containerDrawer.Draw( this, new MouseState(), e );

    protected override void OnResize( EventArgs e )
    {
        base.OnResize( e );
        // Шириной теперь управляет LayoutEngine внутри AnimateStep
        ReorderButtons();
    }

    protected override void OnMouseClick( MouseEventArgs e )
    {
        base.OnMouseClick( e );
        SyncState();

        ContainerSelected?.Invoke( this );
    }

    public void RaiseKeyDown( KeyEventArgs e )
    {
        // ТЕСТ: Нажмите пробел или букву G
        if ( e.KeyCode == Keys.Space || e.KeyCode == Keys.G )
        {
            if ( LayoutEngine is ListLayoutEngine )
                LayoutEngine = new GridLayoutEngine { Columns = 3 };
            else
                LayoutEngine = new ListLayoutEngine();

            SyncState();
            return;
        }

        foreach ( var b in Controls.OfType<LaunchButton>() ) b.RaiseKeyDown( e );
    }
    public void RaiseKeyUp( KeyEventArgs e ) { foreach ( var b in Controls.OfType<LaunchButton>() ) b.RaiseKeyUp( e ); }

    private void OnElevatedDragDrop( object sender, ElevatedDragDropArgs e )
    {
        if ( e.HWnd != Handle ) return;
        var items = e.Files.Select( f => new LaunchItem { FilePath = f, Name = System.IO.Path.GetFileName( f ), Id = _itemsCount++ } ).ToList();
        AddButtons( items );
        ItemCollectionChanged?.Invoke( items );
    }

    protected override void Dispose( bool disposing )
    {
        if ( disposing )
        {
            ElevatedDragDropManager.Instance.ElevatedDragDrop -= OnElevatedDragDrop;
            _animationTimer?.Stop();
            _animationTimer?.Dispose();
            _monitor?.Stop();
            _monitor?.Dispose();
        }
        base.Dispose( disposing );
    }

    #endregion
}