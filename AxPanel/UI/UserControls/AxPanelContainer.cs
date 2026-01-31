using AxPanel.Model;
using AxPanel.SL;
using AxPanel.UI.Drawers;
using AxPanel.UI.Themes;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace AxPanel.UI.UserControls;

public partial class AxPanelContainer : BasePanelControl
{
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
            .Where( b => !string.IsNullOrEmpty( b.Path ) )
            .Select( b => b.Path )
            .ToHashSet( StringComparer.OrdinalIgnoreCase );
    }

    private void OnStatsReceived( Dictionary<string, ProcessStats> stats )
    {
        bool changed = false;
        foreach ( var btn in _buttons )
        {
            if ( btn.Path != null && stats.TryGetValue( btn.Path, out var s ) )
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
        const int speed = 10;
        foreach ( var btn in _buttons )
        {
            if ( btn.Capture ) continue; // Не трогаем кнопку, которую тянет пользователь

            int targetTop = btn.RequestPosition?.Invoke() ?? btn.Top;
            if ( btn.Top != targetTop )
            {
                int diff = targetTop - btn.Top;
                if ( Math.Abs( diff ) <= speed ) btn.Top = targetTop;
                else btn.Top += diff / 4; // Плавное дотягивание (Lerp)
            }
        }
    }

    public void ReorderButtons()
    {
        // Сортируем визуальный список по текущему Y для поддержки drag-reorder
        _buttons = _buttons.OrderBy( b => b.Top ).ToList();

        for ( int i = 0; i < _buttons.Count; i++ )
        {
            var btn = _buttons[ i ];
            int index = i;

            btn.RequestPosition = () =>
                _theme.ContainerStyle.HeaderHeight +
                ( btn.Height + _theme.ButtonStyle.SpaceHeight ) * index +
                _scrollValue;
        }
    }

    private void HandleMouseWheel( object sender, MouseEventArgs e )
    {
        _scrollValue += e.Delta > 0 ? _theme.ContainerStyle.ScrollValueIncrement : -_theme.ContainerStyle.ScrollValueIncrement;
        _scrollValue = Math.Min( 0, _scrollValue ); // Запрет прокрутки выше заголовка
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
        var ordered = items.OrderByDescending( i => i.ClicksCount ).ThenBy( i => i.Id );

        foreach ( var item in ordered )
        {
            var btn = new LaunchButton( _theme )
            {
                Width = this.Width,
                Height = item.Height > 0 ? item.Height : _theme.ButtonStyle.DefaultHeight,
                Text = item.Name,
                Path = item.FilePath,
            };

            btn.ButtonLeftClick += b => StartProcess( b.Path );
            btn.DeleteButtonClick += b => {
                _buttons.Remove( b );
                Controls.Remove( b );
                b.Dispose();
                SyncState();
            };

            btn.MouseMove += ( s, e ) => { if ( e.Button == MouseButtons.Left ) ReorderButtons(); };

            _buttons.Add( btn );
            Controls.Add( btn );
            btn.Top = btn.RequestPosition?.Invoke() ?? 0;
        }
    }

    private void StartProcess( string path, bool runAsAdmin = false )
    {
        try
        {
            if ( string.IsNullOrEmpty( path ) ) return;
            Process.Start( new ProcessStartInfo( path ) { UseShellExecute = true, Verb = runAsAdmin ? "runas" : "" } );

            // Мгновенная реакция UI
            var btn = _buttons.FirstOrDefault( b => b.Path.Equals( path, StringComparison.OrdinalIgnoreCase ) );
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
        foreach ( Control ctrl in Controls ) ctrl.Width = Width;
    }

    protected override void OnMouseClick( MouseEventArgs e )
    {
        base.OnMouseClick( e );
        ContainerSelected?.Invoke( this );
    }

    public void RaiseKeyDown( KeyEventArgs e ) { foreach ( var b in Controls.OfType<LaunchButton>() ) b.RaiseKeyDown( e ); }
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

//public class AxPanelContainer : BasePanelControl
//{
//    // Сервисы
//    private readonly ProcessMonitor _monitor;
//    private readonly ContainerDrawer _containerDrawer;
//    private readonly ITheme _theme;

//    // UI Состояние
//    private readonly System.Windows.Forms.Timer _animationTimer;
//    private List<LaunchButton> _buttons = new();
//    private int _scrollValue = 0;
//    private int _itemsCount = 0;

//    // События
//    public event Action<AxPanelContainer> ContainerSelected;
//    public event Action<List<LaunchItem>> ItemCollectionChanged;

//    public string PanelName { get; set; }

//    public AxPanelContainer( ITheme theme )
//    {
//        _theme = theme ?? throw new ArgumentNullException( nameof( theme ) );
//        _containerDrawer = new ContainerDrawer( _theme );

//        ConfigureStyles();

//        // Инициализация мониторинга процессов
//        _monitor = new ProcessMonitor();
//        _monitor.StatisticsUpdated += OnStatsReceived;
//        _monitor.Start();

//        // Инициализация анимации (~60 FPS)
//        _animationTimer = new System.Windows.Forms.Timer { Interval = 16 };
//        _animationTimer.Tick += ( s, e ) => AnimateStep();
//        _animationTimer.Start();

//        // Внешние интеграции
//        ElevatedDragDropManager.Instance.EnableDragDrop( Handle );
//        ElevatedDragDropManager.Instance.ElevatedDragDrop += OnElevatedDragDrop;

//        MouseWheel += HandleMouseWheel;
//    }

//    private void ConfigureStyles() => 
//        BackColor = _theme.ContainerStyle.BackColor;

//    private void UpdateMonitorPaths()
//    {
//        _monitor.TargetPaths = _buttons
//            .Where( b => !string.IsNullOrEmpty( b.Path ) )
//            .Select( b => b.Path )
//            .ToHashSet( StringComparer.OrdinalIgnoreCase );
//    }

//    private void OnStatsReceived( Dictionary<string, ProcessStats> stats )
//    {
//        bool needsRefresh = false;
//        foreach ( var btn in _buttons )
//        {
//            if ( btn.Path != null && stats.TryGetValue( btn.Path, out var s ) )
//            {
//                if ( btn.IsRunning != s.IsRunning || Math.Abs( btn.CpuUsage - s.CpuUsage ) > 0.5f )
//                {
//                    btn.UpdateState( s.IsRunning, s.CpuUsage, s.RamMb, s.StartTime );
//                    needsRefresh = true;
//                }
//            }
//        }
//        if ( needsRefresh ) Invalidate( true );
//    }    

//    private void AnimateStep()
//    {
//        bool anyMovement = false;
//        const int speed = 10;

//        foreach ( var btn in _buttons )
//        {
//            // Пропускаем кнопку, если её тянет пользователь
//            if ( btn.Capture ) continue;

//            int targetTop = btn.RequestPosition();
//            if ( btn.Top != targetTop )
//            {
//                anyMovement = true;
//                int diff = targetTop - btn.Top;

//                // Плавное приближение (Lerp-like)
//                if ( Math.Abs( diff ) <= speed )
//                    btn.Top = targetTop;
//                else
//                    btn.Top += ( targetTop - btn.Top ) / 4;
//            }
//        }

//        // Если нужно экономить ресурсы, здесь можно останавливать _animationTimer
//    }

//    public void ReorderButtons()
//    {
//        // Сортируем визуальный список по текущей позиции
//        _buttons = _buttons.OrderBy( b => b.Top ).ToList();

//        for ( int i = 0; i < _buttons.Count; i++ )
//        {
//            var btn = _buttons[ i ];
//            int newIndex = i;

//            // Замыкание для вычисления целевой позиции
//            btn.RequestPosition = () =>
//            {
//                return _theme.ContainerStyle.HeaderHeight +
//                       ( btn.Height + _theme.ButtonStyle.SpaceHeight ) * newIndex +
//                       _scrollValue;
//            };
//        }
//    }

//    private void HandleMouseWheel( object sender, MouseEventArgs e )
//    {
//        int delta = e.Delta > 0 ? _theme.ContainerStyle.ScrollValueIncrement : -_theme.ContainerStyle.ScrollValueIncrement;
//        _scrollValue += delta;

//        foreach ( Control control in Controls )
//        {
//            control.Top += delta;
//        }
//    }

//    public void AddButtons( List<LaunchItem> items )
//    {
//        if ( items == null || items.Count == 0 ) return;
//        CreateButtonControls( items );
//        _itemsCount += items.Count;
//    }

//    private void CreateButtonControls( List<LaunchItem> items )
//    {
//        var orderedItems = items.OrderByDescending( i => i.ClicksCount ).ThenBy( i => i.Id ).ToList();

//        foreach ( var item in orderedItems )
//        {
//            var button = new LaunchButton( _theme )
//            {
//                Width = this.Width,
//                Height = item.Height > 0 ? item.Height : _theme.ButtonStyle.DefaultHeight,
//                Text = item.Name,
//                Path = item.FilePath,
//            };

//            // Остальные подписки
//            button.ButtonLeftClick += btn => StartProcess( btn.Path );
//            button.DeleteButtonClick += btn => {
//                _buttons.Remove( btn ); // Убираем из внутреннего списка
//                Controls.Remove( btn );
//                btn.Dispose();
//                ReorderButtons(); // Пересчитываем позиции остальных
//            };

//            button.MouseMove += ( s, e ) =>
//            {
//                if ( e.Button == MouseButtons.Left )
//                    ReorderButtons();
//            };

//            _buttons.Add( button );
//            Controls.Add( button );
//        }

//        // После добавления всех кнопок один раз пересчитываем их RequestPosition и Top
//        ReorderButtons();

//        // Устанавливаем начальный Top на основе новых правил
//        foreach ( var btn in _buttons ) btn.Top = btn.RequestPosition();
//    }

//    private void StartProcess( string path, bool runAsAdmin = false )
//    {
//        try
//        {
//            if( string.IsNullOrEmpty( path ) || ( !File.Exists( path ) && !Directory.Exists( path ) ) ) return;

//            var psi = new ProcessStartInfo( path ) { UseShellExecute = true };
//            if( runAsAdmin ) psi.Verb = "runas";

//            Process.Start( psi );

//            // МГНОВЕННЫЙ ОТКЛИК: Находим кнопку и помечаем как запущенную, 
//            // не дожидаясь срабатывания таймера (2 сек)
//            var btn = _buttons.FirstOrDefault( b => b.Path.Equals( path, StringComparison.OrdinalIgnoreCase ) );
//            if ( btn != null )
//            {
//                btn.IsRunning = true;
//                btn.Invalidate();
//            }
//        }
//        catch( Exception ex )
//        {
//            Debug.WriteLine( $"Run error: {ex.Message}" );
//        }
//    }

//    protected override void OnPaint( PaintEventArgs e ) => _containerDrawer.Draw( this, new MouseState(), e );

//    protected override void OnResize( EventArgs e )
//    {
//        base.OnResize( e );
//        foreach ( Control control in Controls ) control.Width = Width;
//    }

//    protected override void OnMouseClick( MouseEventArgs e )
//    {
//        base.OnMouseClick( e );
//        ContainerSelected?.Invoke( this );
//    }

//    public void RaiseKeyDown( KeyEventArgs keyArgs )
//    {
//        // Используем OfType<LaunchButton>() для безопасной и быстрой фильтрации
//        foreach ( var button in Controls.OfType<LaunchButton>() )
//        {
//            button.RaiseKeyDown( keyArgs );
//        }
//    }

//    public void RaiseKeyUp( KeyEventArgs keyArgs )
//    {
//        foreach ( var button in Controls.OfType<LaunchButton>() )
//        {
//            button.RaiseKeyUp( keyArgs );
//        }
//    }

//    private void OnElevatedDragDrop( object sender, ElevatedDragDropArgs e )
//    {
//        if ( e.HWnd != Handle ) return;

//        var newItems = e.Files.Select( f => new LaunchItem
//        {
//            FilePath = f,
//            Name = System.IO.Path.GetFileName( f ),
//            Id = _itemsCount++
//        } ).ToList();

//        AddButtons( newItems );
//        ItemCollectionChanged?.Invoke( newItems );
//    }

//    // Важно переопределить Dispose для отписки от глобальных событий
//    protected override void Dispose( bool disposing )
//    {
//        if ( disposing )
//        {
//            ElevatedDragDropManager.Instance.ElevatedDragDrop -= OnElevatedDragDrop;
//            _animationTimer?.Stop();
//            _animationTimer?.Dispose();
//            _monitor?.Stop();
//            _monitor?.Dispose();
//        }
//        base.Dispose( disposing );
//    }
//}