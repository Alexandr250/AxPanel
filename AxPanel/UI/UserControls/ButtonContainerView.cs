using AxPanel.Contracts;
using AxPanel.Model;
using AxPanel.UI.Drawers;
using AxPanel.UI.Themes;

namespace AxPanel.UI.UserControls;

public partial class ButtonContainerView : BasePanelControl, IAnimatable
{
    // Сервисы и отрисовка
    private readonly ContainerDrawer _containerDrawer;
    private readonly ITheme _theme;

    // UI Состояние
    private List<LaunchButtonView> _buttons = [];
    private int _scrollValue = 0;
    private int _itemsCount = 0;

    private MouseState _mouseState = new();

    public ILayoutEngine LayoutEngine { get; set; } = new GridLayoutEngine(); // new ListLayoutEngine();
    public IReadOnlyList<LaunchButtonView> Buttons => _buttons;
    public string PanelName { get; set; }
    public int ScrollValue => _scrollValue;
    ITheme IAnimatable.Theme => _theme;

    // События
    public event Action<ButtonContainerView> ContainerSelected;
    public event Action<List<LaunchItem>> ItemCollectionChanged;
    public event Action<ButtonContainerView> ContainerDeleteRequested;
    public event Action<LaunchButtonView> ProcessStartRequested;
    public event Action<LaunchButtonView> ProcessStartAsAdminRequested;
    public event Action<LaunchButtonView> GroupStartRequested;
    public event Action<string> ExplorerOpenRequested;

    public ButtonContainerView( ITheme theme )
    {
        _theme = theme ?? throw new ArgumentNullException( nameof( theme ) );
        _containerDrawer = new ContainerDrawer( _theme );

        // Базовые стили применяются автоматически через BasePanelControl
        BackColor = _theme.ContainerStyle.BackColor;

        // 3. Регистрация внешних интеграций
        ElevatedDragDropManager.Instance.EnableDragDrop( Handle );
        ElevatedDragDropManager.Instance.ElevatedDragDrop += OnElevatedDragDrop;

        MouseWheel += HandleMouseWheel;

        this.AllowDrop = true;
        this.DragEnter += ( s, e ) => {
            if ( e.Data.GetDataPresent( typeof( LaunchButtonView ) ) )
                e.Effect = DragDropEffects.Move;
        };

        this.DragDrop += ( s, e ) => {
            var droppedBtn = ( LaunchButtonView )e.Data?.GetData( typeof( LaunchButtonView ) );

            // Если бросили в другой контейнер
            if ( droppedBtn.Parent != this )
            {
                MoveButtonToThisContainer( droppedBtn );
            }
        };
    }

    private void MoveButtonToThisContainer( LaunchButtonView btn )
    {
        var oldParent = btn.Parent as ButtonContainerView;

        // 1. Удаляем из старой логики
        oldParent?._buttons.Remove( btn );
        oldParent?.Controls.Remove( btn );

        // 2. Добавляем в новую логику
        _buttons.Add( btn );
        this.Controls.Add( btn );

        // 3. Обновляем иерархию и мониторинг
        SyncState();
        oldParent?.SyncState();

        // 4. Генерируем событие для сохранения конфига (если нужно)
        ItemCollectionChanged?.Invoke( null );
    }

    #region Логика обновления состояния

    public void ApplyStats( Dictionary<string, ProcessStats> stats )
    {
        bool changed = false;
        foreach ( var btn in _buttons )
        {
            if ( btn.BaseControlPath != null && stats.TryGetValue( btn.BaseControlPath, out var s ) )
            {
                btn.UpdateState( s.IsRunning, s.CpuUsage, s.RamMb, s.StartTime );
                changed = true;
            }
        }
        if ( changed ) Invalidate( true );
    }

    private void SyncState()
    {
        ReorderButtons();
        ItemCollectionChanged?.Invoke( null ); // Оповещаем MainContainer об изменении путей
    }

    #endregion

    #region Анимация и позиционирование

    public void ReorderButtons()
    {
        // Принудительный вызов, если таймер спит
        Invalidate();
    }

    private void HandleMouseWheel( object sender, MouseEventArgs e )
    {
        int delta = e.Delta > 0 ? _theme.ContainerStyle.ScrollValueIncrement : -_theme.ContainerStyle.ScrollValueIncrement;

        int totalHeight = LayoutEngine.GetTotalContentHeight( Buttons, Width, _theme );
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
        var ordered = SortByGroups( items ); // items.OrderByDescending( i => i.ClicksCount ).ThenBy( i => i.Id );

        foreach ( var item in ordered )
        {
            var btn = new LaunchButtonView( _theme )
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
            btn.ButtonLeftClick += ( button ) => ProcessStartRequested?.Invoke( button );
            btn.ButtonRightClick += b => ExplorerOpenRequested?.Invoke( b.BaseControlPath );
            btn.ButtonMiddleClick += ( button ) => ProcessStartAsAdminRequested?.Invoke( button );

            btn.DeleteButtonClick += b => {
                _buttons.Remove( b ); // Убираем из внутреннего списка
                Controls.Remove( b );
                b.Dispose();
                SyncState(); // Обновляем пути монитора и вызываем пересчет
            };

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
            var layout = LayoutEngine.GetLayout( currentIndex, _scrollValue, this.Width, _buttons, _theme );

            btn.Location = layout.Location;
            btn.Width = layout.Width;
        }

        // После добавления всех кнопок один раз пересчитываем их логическое состояние
        ReorderButtons();
    }

    private List<LaunchItem> SortByGroups( List<LaunchItem> items )
    {
        var result = new List<LaunchItem>();
        var currentGroup = new List<LaunchItem>();

        foreach ( var item in items )
        {
            if ( item.IsSeparator )
            {
                if ( currentGroup.Count > 0 )
                {
                    result.AddRange( currentGroup.OrderByDescending( i => i.ClicksCount ) );
                    currentGroup.Clear();
                }
                result.Add( item );
            }
            else
            {
                currentGroup.Add( item );
            }
        }

        result.AddRange( currentGroup.OrderByDescending( i => i.ClicksCount ) );

        return result;
    }

    #endregion

    #region Переопределения WinForms

    protected override void OnMouseMove( MouseEventArgs e )
    {
        base.OnMouseMove( e );

        // Определяем прямоугольник кнопки удаления
        Rectangle deleteRect = DeleteButtonRect;

        // Обновляем MouseState
        bool wasInDeleteButton = _mouseState.MouseInDeleteButton;
        _mouseState.MouseInDeleteButton = deleteRect.Contains( e.Location );

        if ( wasInDeleteButton != _mouseState.MouseInDeleteButton )
        {
            // Перерисовываем только заголовок
            Invalidate( new Rectangle( 0, 0, Width, _theme.ContainerStyle.HeaderHeight ) );
        }
    }

    protected override void OnMouseLeave( EventArgs e )
    {
        base.OnMouseLeave( e );

        if ( _mouseState.MouseInDeleteButton )
        {
            _mouseState.MouseInDeleteButton = false;
            Invalidate( new Rectangle( 0, 0, Width, _theme.ContainerStyle.HeaderHeight ) );
        }
    }

    protected override void OnPaint( PaintEventArgs e )
    {
        //_containerDrawer.Draw( this, _movingButtonRectangle, _mouseState, e );

        // Находим кнопку, которая сейчас в режиме перемещения (Capture или IsDragging)
        var draggedBtn = _buttons.FirstOrDefault( b => b.Capture || b.IsDragging );

        // Передаем её в отрисовщик контейнера
        _containerDrawer.Draw( this, draggedBtn, _mouseState, e );
    }

    void IAnimatable.UpdateVisual() => Invalidate();

    protected override void OnResize( EventArgs e )
    {
        base.OnResize( e );
        // Шириной теперь управляет LayoutEngine внутри AnimateStep
        ReorderButtons();
    }

    protected override void OnMouseClick( MouseEventArgs e )
    {
        base.OnMouseClick( e );
        
        Rectangle deleteRect = DeleteButtonRect;

        if ( e.Button == MouseButtons.Left && deleteRect.Contains( e.Location ) )
        {
            OnDeleteRequested();
            return; // Не вызываем ContainerSelected при клике на удаление
        }

        SyncState();

        ContainerSelected?.Invoke( this );
    }

    private Rectangle DeleteButtonRect => new(
        Width - _theme.ContainerStyle.ButtonSize - _theme.ContainerStyle.ButtonMargin,
        ( _theme.ContainerStyle.HeaderHeight - _theme.ContainerStyle.ButtonSize ) / 2,
        _theme.ContainerStyle.ButtonSize,
        _theme.ContainerStyle.ButtonSize
    );

    private void OnDeleteRequested() => 
        ContainerDeleteRequested?.Invoke( this );

    private void OnElevatedDragDrop( object sender, ElevatedDragDropArgs e )
    {
        if ( e.HWnd != Handle ) return;
        var items = e.Files.Select( f => new LaunchItem { FilePath = f, Name = System.IO.Path.GetFileName( f ), Id = _itemsCount++ } ).ToList();
        AddButtons( items );
        ItemCollectionChanged?.Invoke( items );
    }

    public void StartProcessGroup( LaunchButtonView separator ) => 
        GroupStartRequested?.Invoke( separator );

    protected override void Dispose( bool disposing )
    {
        if ( disposing )
        {
            ElevatedDragDropManager.Instance.ElevatedDragDrop -= OnElevatedDragDrop;
        }
        base.Dispose( disposing );
    }

    #endregion
}