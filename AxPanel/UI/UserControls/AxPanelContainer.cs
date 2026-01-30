using AxPanel.Model;
using AxPanel.UI.Drawers;
using AxPanel.UI.Themes;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AxPanel.UI.UserControls;

public class AxPanelContainer : BasePanelControl
{
    [DllImport( "psapi.dll", SetLastError = true )]
    private static extern bool EnumProcesses( [Out] int[] lpidProcess, int cb, out int lpcbNeeded );

    [DllImport( "kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode )]
    private static extern bool QueryFullProcessImageName( IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize );

    [DllImport( "kernel32.dll" )]
    private static extern IntPtr OpenProcess( uint processAccess, bool bInheritHandle, int processId );

    [DllImport( "kernel32.dll" )]
    private static extern bool CloseHandle( IntPtr hObject );

    private System.Windows.Forms.Timer _animationTimer;
    private System.Windows.Forms.Timer _processMonitorTimer;

    private List<LaunchButton> _buttons = new();

    private readonly ITheme _theme;
    private readonly ContainerDrawer _containerDrawer;
    private int _scrollValue = 0;
    private int _itemsCount = 0;

    public event Action<AxPanelContainer> ContainerSelected;
    public event Action<List<LaunchItem>> ItemCollectionChanged;

    private readonly ConcurrentDictionary<string, (TimeSpan Time, DateTime Stamp)> _cpuStats = new();
    private readonly ConcurrentDictionary<string, float> _cpuResults = new();

    public string PanelName { get; set; }

    public AxPanelContainer( ITheme theme )
    {
        _theme = theme ?? throw new ArgumentNullException( nameof( theme ) );
        _containerDrawer = new ContainerDrawer( _theme );

        DoubleBuffered = true;

        this.SetStyle( ControlStyles.AllPaintingInWmPaint |
                       ControlStyles.UserPaint |
                       ControlStyles.OptimizedDoubleBuffer |
                       ControlStyles.ResizeRedraw, true );

        BackColor = _theme.ContainerStyle.BackColor;

        // Подписка на глобальный менеджер (нужен Dispose для отписки!)
        ElevatedDragDropManager.Instance.EnableDragDrop( Handle );
        ElevatedDragDropManager.Instance.ElevatedDragDrop += OnElevatedDragDrop;

        MouseWheel += HandleMouseWheel;

        _animationTimer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60 FPS
        _animationTimer.Tick += ( s, e ) => AnimateStep();
        _animationTimer.Start();

        _processMonitorTimer = new System.Windows.Forms.Timer { Interval = 2000 }; // 2 секунды
        _processMonitorTimer.Tick += ( s, e ) => UpdateRunningState();
        _processMonitorTimer.Start();
    }

    private void UpdateRunningState()
    {
        var buttonsToUpdate = _buttons
            .Where( b => !string.IsNullOrEmpty( b.Path ) )
            .ToList();

        if ( buttonsToUpdate.Count == 0 ) return;

        var targetPaths = buttonsToUpdate.Select( b => b.Path ).ToHashSet( StringComparer.OrdinalIgnoreCase );

        Task.Run( () =>
        {
            var foundProcesses = new Dictionary<string, List<int>>( StringComparer.OrdinalIgnoreCase );

            // 1. Сопоставляем пути с ID процессов (через ваш Win32 API подход)
            int[] processIds = new int[ 2048 ];
            if ( !EnumProcesses( processIds, processIds.Length * sizeof( int ), out int bytesReturned ) )
            {
                // ВАЖНО: Возвращаем пустые коллекции вместо простого return
                return (Running: new HashSet<string>(), Cpu: new ConcurrentDictionary<string, float>(), Ram: new ConcurrentDictionary<string, float>(), Dat: new ConcurrentDictionary<string, DateTime?>() );
            }

            int processCount = bytesReturned / sizeof( int );

            for ( int i = 0; i < processCount; i++ )
            {
                int pid = processIds[ i ];
                if ( pid <= 0 ) continue;

                IntPtr hProcess = OpenProcess( 0x1000, false, pid );
                if ( hProcess != IntPtr.Zero )
                {
                    int capacity = 1024;
                    StringBuilder sb = new StringBuilder( capacity );
                    if ( QueryFullProcessImageName( hProcess, 0, sb, ref capacity ) )
                    {
                        string path = sb.ToString();
                        if ( targetPaths.Contains( path ) )
                        {
                            if ( !foundProcesses.ContainsKey( path ) ) foundProcesses[ path ] = new List<int>();
                            foundProcesses[ path ].Add( pid );
                        }
                    }
                    CloseHandle( hProcess );
                }
            }

            // 2. Расчет загрузки CPU для найденных процессов
            var currentResults = new ConcurrentDictionary<string, float>();
            var currentRamResults = new ConcurrentDictionary<string, float>(); // ДЛЯ RAM (путь -> МБ)
            var currentStartTimes = new ConcurrentDictionary<string, DateTime?>();

            DateTime now = DateTime.Now;

            Parallel.ForEach( foundProcesses, kvp =>
            {
                string path = kvp.Key;
                TimeSpan totalCpuTime = TimeSpan.Zero;
                long totalRamBytes = 0;
                DateTime? earliestStart = null;

                foreach ( int pid in kvp.Value )
                {
                    try
                    {
                        using var p = Process.GetProcessById( pid );
                        p.Refresh();

                        // Берем самое старое время старта из группы процессов
                        DateTime pStart = p.StartTime;
                        if ( earliestStart == null || pStart < earliestStart )
                            earliestStart = pStart;

                        totalCpuTime += p.TotalProcessorTime;
                        totalRamBytes += p.WorkingSet64;
                        //totalRamBytes += p.PrivateMemorySize64;
                    }
                    catch { /* процесс мог закрыться */ }
                }

                currentStartTimes[ path ] = earliestStart;

                if ( _cpuStats.TryGetValue( path, out var lastStats ) )
                {
                    double elapsedMs = ( now - lastStats.Stamp ).TotalMilliseconds;
                    double cpuMs = ( totalCpuTime - lastStats.Time ).TotalMilliseconds;

                    // Процент = (Дельта времени CPU / (Дельта времени часов * Кол-во ядер)) * 100
                    float usage = ( float )( cpuMs / ( elapsedMs * Environment.ProcessorCount ) * 100 );
                    currentResults[ path ] = Math.Clamp( usage, 0, 100 );
                }

                _cpuStats[ path ] = (totalCpuTime, now);

                // Конвертируем в Мегабайты
                float ramMb = totalRamBytes / 1024f / 1024f;
                currentRamResults[ path ] = ramMb;
            } );

            return (Running: foundProcesses.Keys.ToHashSet( StringComparer.OrdinalIgnoreCase ), Cpu: currentResults, Ram: currentRamResults, Dat: currentStartTimes);

        } ).ContinueWith( task =>
        {
            // Проверка на ошибки
            if ( task.IsFaulted || task.Status != TaskStatus.RanToCompletion ) return;

            // Распаковываем кортеж
            var (foundRunning, cpuData, ramData, datData) = task.Result;

            bool needsRefresh = false;

            foreach ( var btn in _buttons )
            {
                if ( string.IsNullOrEmpty( btn.Path ) ) continue;

                bool isNowRunning = foundRunning.Contains( btn.Path );
                // Получаем значение CPU, если оно есть в словаре
                cpuData.TryGetValue( btn.Path, out float currentCpu );
                ramData.TryGetValue( btn.Path, out float currentRam );
                datData.TryGetValue( btn.Path, out DateTime? startData );

                // Обновляем, если изменился статус или нагрузка (с порогом 1%)
                if ( btn.IsRunning != isNowRunning ||
                     Math.Abs( btn.CpuUsage - currentCpu ) > 1f ||
                     Math.Abs( btn.RamUsage - currentRam ) > 5f )
                {
                    btn.IsRunning = isNowRunning;
                    btn.CpuUsage = isNowRunning ? currentCpu : 0;
                    btn.RamUsage = isNowRunning ? currentRam : 0;
                    btn.StartTime = isNowRunning ? startData : null;
                    needsRefresh = true;
                }
            }

            if ( needsRefresh ) Invalidate( true );

        }, TaskScheduler.FromCurrentSynchronizationContext() );
    }

    private string GetProcessPath( Process p )
    {
        // PROCESS_QUERY_LIMITED_INFORMATION (0x1000) — позволяет брать путь даже у защищенных процессов
        IntPtr hProcess = OpenProcess( 0x1000, false, p.Id );
        if ( hProcess != IntPtr.Zero )
        {
            try
            {
                int capacity = 1024;
                StringBuilder sb = new StringBuilder( capacity );
                if ( QueryFullProcessImageName( hProcess, 0, sb, ref capacity ) )
                {
                    return sb.ToString();
                }
            }
            finally
            {
                CloseHandle( hProcess );
            }
        }
        return null;
    }

    private void AnimateStep()
    {
        bool anyMovement = false;
        int speed = 10; // Скорость анимации (пикселей за шаг)

        foreach ( var btn in _buttons )
        {
            // Не анимируем кнопку, которую в данный момент тянет пользователь
            if ( btn.Capture ) continue;

            int targetTop = btn.RequestPosition();
            if ( btn.Top != targetTop )
            {
                anyMovement = true;
                int diff = targetTop - btn.Top;

                // Плавное приближение (Lerp)
                if ( Math.Abs( diff ) <= speed )
                    btn.Top = targetTop;
                else
                    btn.Top += ( targetTop - btn.Top ) / 4; // Math.Sign( diff ) * speed;
            }
        }

        // Оптимизация: можно останавливать таймер, если ни одна кнопка не движется
    }

    public void ReorderButtons()
    {
        // Сортируем список по текущему Top
        _buttons = _buttons.OrderBy( b => b.Top ).ToList();

        for ( int i = 0; i < _buttons.Count; i++ )
        {
            var btn = _buttons[ i ];
            int newIndex = i;

            btn.RequestPosition = () =>
            {
                return _theme.ContainerStyle.HeaderHeight +
                       ( btn.Height + _theme.ButtonStyle.SpaceHeight ) * newIndex +
                       _scrollValue;
            };
        }
    }

    private void HandleMouseWheel( object sender, MouseEventArgs e )
    {
        int delta = e.Delta > 0 ? _theme.ContainerStyle.ScrollValueIncrement : -_theme.ContainerStyle.ScrollValueIncrement;
        _scrollValue += delta;

        foreach ( Control control in Controls )
        {
            control.Top += delta;
        }
    }

    public void AddButtons( List<LaunchItem> items )
    {
        if ( items == null || items.Count == 0 ) return;
        CreateButtonControls( items );
        _itemsCount += items.Count;
    }

    private void CreateButtonControls( List<LaunchItem> items )
    {
        var orderedItems = items.OrderByDescending( i => i.ClicksCount ).ThenBy( i => i.Id ).ToList();

        foreach ( var item in orderedItems )
        {
            var button = new LaunchButton( _theme )
            {
                Width = this.Width,
                Height = item.Height > 0 ? item.Height : _theme.ButtonStyle.DefaultHeight,
                Text = item.Name,
                Path = item.FilePath,
            };

            // Остальные подписки
            button.ButtonLeftClick += btn => StartProcess( btn.Path );
            button.DeleteButtonClick += btn => {
                _buttons.Remove( btn ); // Убираем из внутреннего списка
                Controls.Remove( btn );
                btn.Dispose();
                ReorderButtons(); // Пересчитываем позиции остальных
            };

            button.MouseMove += ( s, e ) =>
            {
                if ( e.Button == MouseButtons.Left )
                    ReorderButtons();
            };

            _buttons.Add( button );
            Controls.Add( button );
        }

        // После добавления всех кнопок один раз пересчитываем их RequestPosition и Top
        ReorderButtons();

        // Устанавливаем начальный Top на основе новых правил
        foreach ( var btn in _buttons ) btn.Top = btn.RequestPosition();
    }

    private void StartProcess( string path, bool runAsAdmin = false )
    {
        try
        {
            if( string.IsNullOrEmpty( path ) || ( !File.Exists( path ) && !Directory.Exists( path ) ) ) return;

            var psi = new ProcessStartInfo( path ) { UseShellExecute = true };
            if( runAsAdmin ) psi.Verb = "runas";

            Process.Start( psi );

            // МГНОВЕННЫЙ ОТКЛИК: Находим кнопку и помечаем как запущенную, 
            // не дожидаясь срабатывания таймера (2 сек)
            var btn = _buttons.FirstOrDefault( b => b.Path.Equals( path, StringComparison.OrdinalIgnoreCase ) );
            if ( btn != null )
            {
                btn.IsRunning = true;
                btn.Invalidate();
            }
        }
        catch( Exception ex )
        {
            Debug.WriteLine( $"Run error: {ex.Message}" );
        }
    }

    protected override void OnPaint( PaintEventArgs e ) => _containerDrawer.Draw( this, new MouseState(), e );

    protected override void OnResize( EventArgs e )
    {
        base.OnResize( e );
        foreach ( Control control in Controls ) control.Width = Width;
    }

    protected override void OnMouseClick( MouseEventArgs e )
    {
        base.OnMouseClick( e );
        ContainerSelected?.Invoke( this );
    }

    public void RaiseKeyDown( KeyEventArgs keyArgs )
    {
        // Используем OfType<LaunchButton>() для безопасной и быстрой фильтрации
        foreach ( var button in Controls.OfType<LaunchButton>() )
        {
            button.RaiseKeyDown( keyArgs );
        }
    }

    public void RaiseKeyUp( KeyEventArgs keyArgs )
    {
        foreach ( var button in Controls.OfType<LaunchButton>() )
        {
            button.RaiseKeyUp( keyArgs );
        }
    }

    private void OnElevatedDragDrop( object sender, ElevatedDragDropArgs e )
    {
        if ( e.HWnd != Handle ) return;

        var newItems = e.Files.Select( f => new LaunchItem
        {
            FilePath = f,
            Name = System.IO.Path.GetFileName( f ),
            Id = _itemsCount++
        } ).ToList();

        AddButtons( newItems );
        ItemCollectionChanged?.Invoke( newItems );
    }

    // Важно переопределить Dispose для отписки от глобальных событий
    protected override void Dispose( bool disposing )
    {
        if ( disposing )
        {
            ElevatedDragDropManager.Instance.ElevatedDragDrop -= OnElevatedDragDrop;
        }
        base.Dispose( disposing );
    }
}