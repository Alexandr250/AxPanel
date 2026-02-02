using AxPanel.Model;
using AxPanel.SL;
using AxPanel.UI.Themes;

namespace AxPanel.UI.UserControls;

public class AxPanelMainContainer : Panel
{
    private readonly ContainerService _containerService = new();
    private readonly ProcessMonitor _globalMonitor = new();
    private readonly GlobalAnimator _animator = new();

    private readonly System.Windows.Forms.Timer _animationTimer;
    private readonly ITheme _theme;
    private readonly Brush _backBrush;

    private AxPanelContainer _selected;
    private int _targetSelectedHeight;

    // Оптимизированный доступ к контейнерам без создания лишних списков
    public IEnumerable<AxPanelContainer> Containers => Controls.OfType<AxPanelContainer>();

    public AxPanelContainer Selected
    {
        get => _selected;
        private set
        {
            if ( _selected == value ) return;
            _selected = value;
            StartAnimateArrange();
        }
    }

    public AxPanelMainContainer( ITheme theme )
    {
        _theme = theme ?? throw new ArgumentNullException( nameof( theme ) );
        _backBrush = new SolidBrush( _theme.WindowStyle.BackColor );

        DoubleBuffered = true;
        SetStyle( ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true );
        BackColor = _theme.WindowStyle.BackColor;

        // Использование UI-таймера исключает ошибки потоков и Invoke
        _animationTimer = new System.Windows.Forms.Timer { Interval = 15 };
        _animationTimer.Tick += ( s, e ) => AnimateStep();

        _globalMonitor.StatisticsUpdated += OnGlobalStatsReceived;
        _globalMonitor.Start();
    }

    public AxPanelContainer AddContainer( string name, List<LaunchItem>? items )
    {
        var container = new AxPanelContainer( _theme )
        {
            PanelName = name,
            BaseControlPath = name,
            Width = this.Width,
            Height = _theme.ContainerStyle.HeaderHeight
        };

        container.ContainerSelected += panel => Selected = panel;

        // Добавляем обработчик удаления
        container.ContainerDeleteRequested += DeleteContainer;

        container.AddButtons( items );

        Controls.Add( container );

        if ( Selected == null ) Selected = container;
        else ArrangeContainers(); // Мгновенная расстановка при добавлении

        // Привязываем запуск одиночного процесса
        container.ProcessStartRequested += btn =>
            _containerService.RunProcess( btn );

        // Привязываем открытие проводника
        container.ExplorerOpenRequested += path =>
            _containerService.OpenLocation( path );

        // Привязываем групповой запуск
        container.GroupStartRequested += separator =>
        {
            // Берем кнопки прямо из контролов панели, чтобы типы совпали
            var allButtons = container.Controls.OfType<LaunchButton>().ToList();
            int startIndex = allButtons.IndexOf( separator );

            if ( startIndex != -1 )
            {
                var group = allButtons
                    .Skip( startIndex + 1 )
                    .TakeWhile( b => !string.IsNullOrEmpty( b.BaseControlPath ) );

                _containerService.RunProcessGroup( group );
            }
        };

        container.ItemCollectionChanged += _ => UpdateGlobalMonitorPaths();

        // Регистрируем контейнер в аниматоре прямо здесь
        _animator.Register( container );

        // Не забываем отписать при удалении
        container.ContainerDeleteRequested += c => _animator.Unregister( c );

        return container;
    }

    private void UpdateGlobalMonitorPaths()
    {
        // Собираем пути со всех кнопок во всех контейнерах
        var allPaths = Containers
            .SelectMany( c => c.Buttons )
            .Where( b => !string.IsNullOrEmpty( b.BaseControlPath ) )
            .Select( b => b.BaseControlPath )
            .ToHashSet( StringComparer.OrdinalIgnoreCase );

        _globalMonitor.TargetPaths = allPaths;
    }

    private void OnGlobalStatsReceived( Dictionary<string, ProcessStats> stats )
    {
        // Рассылаем статистику всем панелям
        foreach ( var container in Containers )
        {
            // Используем Invoke, так как монитор работает в фоновом потоке
            if ( container.InvokeRequired )
                container.BeginInvoke( new Action( () => container.ApplyStats( stats ) ) );
            else
                container.ApplyStats( stats );
        }
    }

    private void DeleteContainer( AxPanelContainer container )
    {
        // Не удаляем, если это последний контейнер
        if ( Controls.OfType<AxPanelContainer>().Count() <= 1 )
            return;

        // Если удаляем выбранный контейнер, выбираем другой
        if ( Selected == container )
        {
            var otherContainer = Controls.OfType<AxPanelContainer>()
                .FirstOrDefault( c => c != container );
            Selected = otherContainer;
        }

        Controls.Remove( container );

        // Отписываемся от событий
        container.ContainerDeleteRequested -= DeleteContainer;

        container.Dispose();
        ArrangeContainers();
    }

    private void StartAnimateArrange()
    {
        _targetSelectedHeight = Height - ( Controls.Count - 1 ) * _theme.ContainerStyle.HeaderHeight;
        _animationTimer.Start();
    }

    private void AnimateStep()
    {
        bool stillAnimating = false;
        int step = 40; // Скорость раскрытия
        int currentTop = 0;

        foreach ( var container in Containers )
        {
            container.Top = currentTop;
            //container.Width = this.Width;

            int targetH = ( container == Selected ) ? _targetSelectedHeight : _theme.ContainerStyle.HeaderHeight;

            if ( container.Height != targetH )
            {
                stillAnimating = true;
                int diff = targetH - container.Height;

                if ( Math.Abs( diff ) <= step ) container.Height = targetH;
                else container.Height += Math.Sign( diff ) * step;
            }

            currentTop += container.Height;
        }

        if ( !stillAnimating ) _animationTimer.Stop();

        // Проверка: если над свернутой панелью что-то тащат — раскрываем
        foreach ( var container in Containers )
        {
            Point clientPos = container.PointToClient( Cursor.Position );
            if ( container.DisplayRectangle.Contains( clientPos ) && Selected != container )
            {
                // Раскрываем панель "на лету"
                Selected = container;
            }
        }
    }

    public void ArrangeContainers()
    {
        _animationTimer.Stop(); // Прерываем анимацию при жесткой расстановке
        int currentTop = 0;
        int selHeight = Height - ( Controls.Count - 1 ) * _theme.ContainerStyle.HeaderHeight;

        foreach ( var container in Containers )
        {
            container.Top = currentTop;
            container.Height = ( container == Selected ) ? selHeight : _theme.ContainerStyle.HeaderHeight;
            //container.Width = this.Width;
            currentTop += container.Height;
        }
    }

    protected override void OnResize( EventArgs e )
    {
        base.OnResize( e );
        ArrangeContainers();
    }

    protected override void OnPaint( PaintEventArgs e )
    {
        e.Graphics.FillRectangle( _backBrush, ClientRectangle );
    }

    protected override void OnParentChanged( EventArgs e )
    {
        base.OnParentChanged( e );

        if ( Parent != null )
        {
            int headerHeight = 30;
            int borderWidth = 5;

            try
            {
                var config = ConfigManager.ReadMainConfig();
                if ( config != null )
                {
                    headerHeight = config.HeaderHeight;
                    borderWidth = config.BorderWidth;
                }
            }
            catch { /* дефолтные значения при ошибке */ }

            // Отключаем Dock, чтобы ручные координаты работали
            this.Dock = DockStyle.None;

            // Установка координат с учетом всех отступов
            this.Left = borderWidth;
            this.Top = headerHeight;

            // Ширина: ширина родителя минус левый и правый отступы
            this.Width = Parent.Width - ( borderWidth * 2 );

            // Высота: высота родителя минус верхний (Header) и нижний (Border) отступы
            this.Height = Parent.Height - headerHeight - borderWidth;

            // Привязки для автоматического ресайза при растягивании окна
            this.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            ArrangeContainers();
        }
    }

    protected override void Dispose( bool disposing )
    {
        if ( disposing )
        {
            _animationTimer?.Dispose();
            _backBrush?.Dispose();

            _globalMonitor.Stop();
            _globalMonitor.Dispose();

            _animator.Dispose();
        }
        base.Dispose( disposing );
    }
}
