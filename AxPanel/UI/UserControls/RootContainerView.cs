using AxPanel.Model;
using AxPanel.SL;
using AxPanel.UI.Themes;

namespace AxPanel.UI.UserControls;

public class RootContainerView : Panel
{
    private readonly ContainerService _containerService = new();
    private readonly ProcessMonitor _globalMonitor = new();
    private readonly GlobalAnimator _animator = new();

    private readonly System.Windows.Forms.Timer _animationTimer;
    private readonly ITheme _theme;
    private readonly MainConfig _mainConfig;
    private readonly Brush _backBrush;

    private ButtonContainerView _selected;
    private int _targetSelectedHeight;

    private readonly int _footerHeight = 25; // Высота футера
    private readonly Brush _footerBrush;

    public MainModel MainModel { get; set; }

    public event Action OnSaveConfigRequered;

    // Оптимизированный доступ к контейнерам без создания лишних списков
    public IEnumerable<ButtonContainerView> Containers => Controls.OfType<ButtonContainerView>();

    private List<(Rectangle Rect, Action Action, string Tooltip)> _footerButtons = new();

    public ButtonContainerView Selected
    {
        get => _selected;
        private set
        {
            if ( _selected == value ) return;
            _selected = value;
            StartAnimateArrange();
        }
    }

    public RootContainerView( ITheme theme, MainConfig mainConfig )
    {
        _theme = theme ?? throw new ArgumentNullException( nameof( theme ) );
        _mainConfig = mainConfig;
        _backBrush = new SolidBrush( _theme.WindowStyle.BackColor );
        _footerBrush = new SolidBrush( _theme.WindowStyle.FooterColor );

        InitFooterButtons();

        DoubleBuffered = true;
        SetStyle( ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true );
        SetStyle( ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true );
        BackColor = _theme.WindowStyle.BackColor;

        // Использование UI-таймера исключает ошибки потоков и Invoke
        _animationTimer = new System.Windows.Forms.Timer { Interval = 15 };
        _animationTimer.Tick += ( s, e ) => AnimateStep();

        _globalMonitor.StatisticsUpdated += OnGlobalStatsReceived;
        _globalMonitor.Start();
    }

    private void InitFooterButtons()
    {
        int btnWidth = 30;
        int x = Width - 10; // Начинаем справа

        // Кнопка Выключения
        x -= btnWidth;
        _footerButtons.Add( (new Rectangle( x, Height - _footerHeight, btnWidth, _footerHeight ), ProcessManager.Shutdown, "Выключение") );

        // Кнопка Перезагрузки
        x -= btnWidth;
        _footerButtons.Add( (new Rectangle( x, Height - _footerHeight, btnWidth, _footerHeight ), ProcessManager.Restart, "Перезагрузка") );

        // Кнопка Спящего режима
        x -= btnWidth;
        _footerButtons.Add( (new Rectangle( x, Height - _footerHeight, btnWidth, _footerHeight ), ProcessManager.Sleep, "Спящий режим") );
    }


    public ButtonContainerView AddContainer( string name, List<LaunchItem>? items )
    {
        var container = new ButtonContainerView( _theme, _mainConfig )
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

        if ( Selected == null ) 
            Selected = container;
        else
            ArrangeContainers();

        // Привязываем запуск одиночного процесса
        container.ProcessStartRequested += ( btn, args ) =>
            _containerService.RunProcess( btn, false, args );

        // Привязываем запуск одиночного процесса от имени администратора
        container.ProcessStartAsAdminRequested += btn =>
            _containerService.RunProcess( btn, true );

        // Привязываем открытие проводника
        container.ExplorerOpenRequested += path =>
            _containerService.OpenLocation( path );

        // Привязываем групповой запуск
        container.GroupStartRequested += separator =>
        {
            // Берем кнопки прямо из контролов панели, чтобы типы совпали
            var allButtons = container.Controls.OfType<LaunchButtonView>().ToList();
            int startIndex = allButtons.IndexOf( separator );

            if ( startIndex != -1 )
            {
                var group = allButtons
                    .Skip( startIndex + 1 )
                    .TakeWhile( b => !string.IsNullOrEmpty( b.BaseControlPath ) );

                _containerService.RunProcessGroup( group );
            }
        };

        container.ItemCollectionChanged += ( items ) =>
        {
            if ( items != null )
            {
                // 1. Получаем живую ссылку на модель из кэша (тот самый static _cachedModel)
                var model = ConfigManager.GetModel();

                // 2. Находим нужный контейнер по имени
                var target = model.Containers.FirstOrDefault( c => c.Name == container.PanelName );
                if ( target != null )
                {
                    target.Items = items; // Заменяем старые данные новыми

                    // 3. Вызываем сохранение
                    OnSaveConfigRequered?.Invoke();
                }
            }
            UpdateGlobalMonitorPaths();
        };

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

    private void DeleteContainer( ButtonContainerView container )
    {
        // Не удаляем, если это последний контейнер
        if ( Controls.OfType<ButtonContainerView>().Count() <= 1 )
            return;

        // Если удаляем выбранный контейнер, выбираем другой
        if ( Selected == container )
        {
            var otherContainer = Controls.OfType<ButtonContainerView>()
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
        _targetSelectedHeight = Height -
                                ( ( Controls.OfType<ButtonContainerView>().Count() - 1 ) * _theme.ContainerStyle.HeaderHeight ) -
                                _footerHeight;
        //_targetSelectedHeight = Height - ( Controls.Count - 1 ) * _theme.ContainerStyle.HeaderHeight;
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
            container.Width = this.Width;

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
        _animationTimer.Stop();
        int currentTop = 0;
        int selHeight = Height - ( ( Containers.Count() - 1 ) * _theme.ContainerStyle.HeaderHeight ) - _footerHeight;

        foreach ( var container in Containers )
        {
            container.Width = this.Width;
            container.Top = currentTop;
            container.Height = ( container == Selected ) ? selHeight : _theme.ContainerStyle.HeaderHeight;
            currentTop += container.Height;
        }
        Invalidate(); // Чтобы перерисовать футер
    }

    private Rectangle GetFooterBtnRect( int index )
    {
        int count = 3;
        int btnWidth = Width / count; // Базовая ширина кнопки
        int x = index * btnWidth;

        // Если это последняя кнопка, растягиваем её до самого края (убираем щель от деления)
        int actualWidth = ( index == count - 1 ) ? Width - x : btnWidth;

        return new Rectangle( x, Height - _footerHeight, actualWidth, _footerHeight );
    }

    protected override void OnMouseClick( MouseEventArgs e )
    {
        base.OnMouseClick( e );
        Invalidate(); // Обновить визуальное состояние
    }

    protected override void OnMouseDown( MouseEventArgs e )
    {
        base.OnMouseDown( e );

        // e.Clicks == 2 означает двойной клик в ту же точку
        if ( e.Button == MouseButtons.Left && e.Clicks == 2 )
        {
            for ( int i = 0; i < 3; i++ )
            {
                if ( GetFooterBtnRect( i ).Contains( e.Location ) )
                {
                    ExecuteCommand( i );
                    break;
                }
            }
        }
        else if ( e.Button == MouseButtons.Right )
        {
            ProcessManager.Abort(); // Отмена
            MessageBox.Show( "Выключение отменено", "Выключение отменено", MessageBoxButtons.OK, MessageBoxIcon.Information );
            Invalidate();
        }
    }

    private void ExecuteCommand( int index )
    {
        OnSaveConfigRequered?.Invoke();

        switch ( index )
        {
            case 0: ProcessManager.Shutdown(); break;
            case 1: ProcessManager.Restart(); break;
            case 2: ProcessManager.Sleep(); break;
        }
    }

    protected override void OnMouseMove( MouseEventArgs e )
    {
        base.OnMouseMove( e );

        // Проверяем, находится ли курсор над любой из кнопок футера
        bool isOverAnyBtn = false;
        for ( int i = 0; i < 3; i++ )
        {
            if ( GetFooterBtnRect( i ).Contains( e.Location ) )
            {
                isOverAnyBtn = true;
                break;
            }
        }

        // Если мышь в зоне футера — перерисовываем его для обновления подсветки
        if ( e.Y >= Height - _footerHeight )
        {
            // Перерисовываем только область футера, чтобы не грузить систему
            Invalidate( new Rectangle( 0, Height - _footerHeight, Width, _footerHeight ) );
        }
    }

    // Также важно сбросить подсветку, когда мышь уходит с панели
    protected override void OnMouseLeave( EventArgs e )
    {
        base.OnMouseLeave( e );
        Invalidate( new Rectangle( 0, Height - _footerHeight, Width, _footerHeight ) );
    }

    protected override void OnResize( EventArgs e )
    {
        base.OnResize( e );
        ArrangeContainers();
    }

    protected override void OnPaint( PaintEventArgs e )
    {
        base.OnPaint( e );
        var g = e.Graphics;
        var mousePos = PointToClient( Cursor.Position );

        string[] icons = { "\uE7E8", "\uE777", "\uE708" };
        string[] texts = { "Завершение работы", "Перезагрузка", "Спящий режим" };

        for ( int i = 0; i < 3; i++ )
        {
            Rectangle btnRect = GetFooterBtnRect( i );
            bool isHovered = btnRect.Contains( mousePos );

            // 1. Подсветка фона
            if ( isHovered )
            {
                Color hoverColor = ( i == 0 ) ? _theme.WindowStyle.FooterCloseBtnHoverColor : _theme.WindowStyle.FooterBtnHoverColor;
                using var brush = new SolidBrush( hoverColor );
                g.FillRectangle( brush, btnRect );

                g.DrawLine( _theme.WindowStyle.FooterBtnBorderDarkPen, btnRect.X, btnRect.Y, btnRect.Right - 1, btnRect.Y );
                g.DrawLine( _theme.WindowStyle.FooterBtnBorderDarkPen, btnRect.X, btnRect.Y, btnRect.X, btnRect.Bottom - 1 );
                // Свет снизу-справа
                g.DrawLine( _theme.WindowStyle.FooterBtnBorderLightPen, btnRect.X, btnRect.Bottom - 1, btnRect.Right - 1, btnRect.Bottom - 1 );
                g.DrawLine( _theme.WindowStyle.FooterBtnBorderLightPen, btnRect.Right - 1, btnRect.Y, btnRect.Right - 1, btnRect.Bottom - 1 );
            }

            // 2. Отрисовка контента
            if ( isHovered )
            {
                // Рисуем текст по центру (иконку можно скрыть или сдвинуть влево)
                TextRenderer.DrawText( g, texts[ i ], _theme.WindowStyle.FooterTextFont, btnRect, _theme.WindowStyle.FooterTextColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis );
            }
            else
            {
                // Рисуем только иконку, когда мышь далеко
                TextRenderer.DrawText( g, icons[ i ], _theme.WindowStyle.FooterIconFont, btnRect, _theme.WindowStyle.FooterTextColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter );
            }

            // 3. Разделитель
            if ( i < 2 )
            {
                using var linePen = new Pen( _theme.WindowStyle.FooterSeparatorColor );
                g.DrawLine( linePen, btnRect.Right - 1, btnRect.Y + 5, btnRect.Right - 1, btnRect.Bottom - 5 );
            }
        }
    }

    protected override void OnParentChanged( EventArgs e )
    {
        base.OnParentChanged( e );
        if ( Parent != null )
        {
            this.Dock = DockStyle.None;
            // Задаем начальные координаты
            this.Location = new Point( _mainConfig.BorderWidth, _mainConfig.HeaderHeight );
            // Включаем авто-растяжение
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
