using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using AxPanel.Contracts;
using AxPanel.Model;
using AxPanel.UI.Drawers;
using AxPanel.UI.Themes;
using static AxPanel.Win32Api;

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

    public ILayoutEngine LayoutEngine { get; set; }
    public IReadOnlyList<LaunchButtonView> Buttons => _buttons;
    public string PanelName { get; set; }
    public int ScrollValue => _scrollValue;
    ITheme IAnimatable.Theme => _theme;

    // События
    public event Action<ButtonContainerView> ContainerSelected;
    public event Action<List<LaunchItem>> ItemCollectionChanged;
    public event Action<ButtonContainerView> ContainerDeleteRequested;
    public event Action<LaunchButtonView, object?> ProcessStartRequested;
    public event Action<LaunchButtonView> ProcessStartAsAdminRequested;
    public event Action<LaunchButtonView> GroupStartRequested;
    public event Action<string> ExplorerOpenRequested;

    public ButtonContainerView( ITheme theme, MainConfig mainConfig )
    {
        LayoutEngine = mainConfig.LayoutMode == LayoutMode.List ? 
            new ListLayoutEngine() : 
            new GridLayoutEngine();

        _theme = theme ?? throw new ArgumentNullException( nameof( theme ) );
        _containerDrawer = new ContainerDrawer( _theme );

        // Базовые стили применяются автоматически через BasePanelControl
        BackColor = _theme.ContainerStyle.BackColor;

        
        MouseWheel += HandleMouseWheel;

        this.AllowDrop = true;
        this.DragEnter += ( s, e ) => {
            if ( e.Data.GetDataPresent( typeof( LaunchButtonView ) ) )
                e.Effect = DragDropEffects.Move;
            else if ( e.Data.GetDataPresent( DataFormats.FileDrop ) )
                e.Effect = DragDropEffects.Copy;
        };

        this.DragDrop += ( s, e ) => {
            if( e.Data.GetDataPresent( typeof( LaunchButtonView ) ) )
            {
                var droppedBtn = ( LaunchButtonView )e.Data?.GetData( typeof( LaunchButtonView ) );

                // Если бросили в другой контейнер
                if( droppedBtn.Parent != this )
                {
                    MoveButtonToThisContainer( droppedBtn );
                }
            }
            else if ( e.Data.GetDataPresent( DataFormats.FileDrop ) )
            {
                string[] files = ( string[] )e.Data.GetData( DataFormats.FileDrop );
                if ( files != null && files.Length > 0 )
                {
                    // Вызываем твой стандартный метод добавления
                    var items = files.Select( f => new LaunchItem
                    {
                        FilePath = f,
                        Name = Path.GetFileName( f )
                    } ).ToList();

                    AddButtons( items );
                }
            }
        };

        //ElevatedDragDropManager.Instance.EnableDragDrop( Handle );
        //ElevatedDragDropManager.Instance.ElevatedDragDrop += OnElevatedDragDrop;

    }

    protected override void OnHandleCreated( EventArgs e )
    {
        base.OnHandleCreated( e );

        AllowDrop = false;

        CHANGEFILTERSTRUCT cfs = new() { cbSize = ( uint )Marshal.SizeOf( typeof( CHANGEFILTERSTRUCT ) ) };
        
        ChangeWindowMessageFilterEx( Handle, WM_DROPFILES, 1, ref cfs );
        ChangeWindowMessageFilterEx( Handle, WM_COPYGLOBALDATA, 1, ref cfs );
        ChangeWindowMessageFilterEx( Handle, WM_COPYDATA, 1, ref cfs );

        DragAcceptFiles( Handle, true );
    }

    protected override void WndProc( ref Message m )
    {
        if ( m.Msg == WM_DROPFILES )
        {
            HandleNativeDrop( m.WParam );
            return;
        }
        base.WndProc( ref m );
    }

    protected override void OnDragEnter( DragEventArgs drgevent )
    {
        // Принудительно разрешаем, игнорируя стандартные проверки
        if ( drgevent.Data.GetDataPresent( DataFormats.FileDrop ) )
        {
            drgevent.Effect = DragDropEffects.Copy;
        }
        else if ( drgevent.Data.GetDataPresent( typeof( LaunchButtonView ) ) )
        {
            drgevent.Effect = DragDropEffects.Move;
        }
        else
        {
            drgevent.Effect = DragDropEffects.None;
        }
        // Не вызываем base.OnDragEnter(drgevent), если хотим исключить влияние родителя
    }

    protected override void OnDragDrop( DragEventArgs drgevent )
    {
        if ( drgevent.Data.GetDataPresent( DataFormats.FileDrop ) )
        {
            string[] files = ( string[] )drgevent.Data.GetData( DataFormats.FileDrop );
            if ( files?.Length > 0 )
            {
                var items = files.Select( f => new LaunchItem
                {
                    FilePath = f,
                    Name = Path.GetFileName( f )
                } ).ToList();
                AddButtons( items );
                SyncModelOrder();
            }
        }
        // Здесь уже можно вызвать базу для внутренних кнопок
        base.OnDragDrop( drgevent );
    }

    private void HandleNativeDrop( IntPtr hDrop )
    {
        uint count = DragQueryFile( hDrop, 0xFFFFFFFF, null, 0 );
        List<string> files = new();
        for ( uint i = 0; i < count; i++ )
        {
            StringBuilder sb = new StringBuilder( 260 );
            DragQueryFile( hDrop, i, sb, 260 );
            files.Add( sb.ToString() );
        }

        if ( files.Count > 0 )
        {
            var items = files.Select( f => new LaunchItem
            {
                FilePath = f,
                Name = Path.GetFileName( f )
            } ).ToList();

            this.BeginInvoke( new Action( () => {
                AddButtons( items );
                SyncModelOrder();
            } ) );
        }
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
                btn.UpdateState( s );
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
        this.SuspendLayout();

        for ( int i = 0; i < _buttons.Count; i++ )
        {
            LaunchButtonView button = _buttons[ i ];

            if ( button.IsDragging || button.Capture ) 
                continue;

            (Point Location, int Width) layout = LayoutEngine.GetLayout( i, _scrollValue, this.Width, _buttons, _theme );

            if ( button.Location != layout.Location || button.Width != layout.Width )
            {
                button.Location = layout.Location;
                button.Width = layout.Width;
            }
        }

        this.ResumeLayout( false );
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

            if( item is PortableItem portable )
            {
                btn.DownloadUrl = portable.DownloadUrl;
                btn.IsArchive = portable.IsArchive;
            }

            // Остальные подписки
            btn.ButtonLeftClick += ( button, args ) => ProcessStartRequested?.Invoke( button, args );
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
                if( e.Button == MouseButtons.Left )
                {
                    _mouseState.ButtonMoved = true;
                    ReorderButtons();
                }
            };

            btn.MouseUp += ( s, e ) =>
            {
                if ( _mouseState.ButtonMoved )
                {
                    SyncModelOrder();
                    _mouseState.ButtonMoved = false;
                }
            };

            btn.Dragging += ( b ) => {
                // Вычисляем индекс в зависимости от текущего движка
                int targetIndex = ( LayoutEngine is GridLayoutEngine )
                    ? CalculateGridIndex( b )
                    : CalculateListIndex( b );

                int oldIndex = _buttons.IndexOf( b );

                if ( targetIndex != -1 && targetIndex != oldIndex )
                {
                    // МЕНЯЕМ ПОРЯДОК В СПИСКЕ
                    var itemToMove = _buttons[ oldIndex ];
                    _buttons.RemoveAt( oldIndex );
                    _buttons.Insert( targetIndex, itemToMove );

                    // СРАЗУ ПЕРЕРАСЧИТЫВАЕМ ПОЗИЦИИ ВСЕХ ОСТАЛЬНЫХ
                    ReorderButtons();

                    // Оповещаем систему, что порядок изменился (для сохранения в JSON)
                    ItemCollectionChanged?.Invoke( null );
                }
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

    private int CalculateListIndex( LaunchButtonView draggedBtn )
    {
        if ( _buttons.Count <= 1 ) return 0;

        // Начальная точка отсчета (заголовок + отступы)
        int currentY = _theme.ContainerStyle.HeaderHeight;
        int sHeight = _theme.ButtonStyle.SpaceHeight > 0 ? _theme.ButtonStyle.SpaceHeight : 3;

        // Центр перетаскиваемой кнопки (её текущее положение на экране)
        int dragCenterY = draggedBtn.Top + ( draggedBtn.Height / 2 );

        for ( int i = 0; i < _buttons.Count; i++ )
        {
            int btnHeight = _buttons[ i ].Height;

            // Если центр перетаскиваемой кнопки выше середины текущей кнопки в цикле
            if ( dragCenterY < currentY + ( btnHeight / 2 ) )
            {
                return i;
            }

            // Прибавляем высоту текущей кнопки и зазор для следующей итерации
            currentY += btnHeight + sHeight;
        }

        // Если мы ниже всех кнопок — возвращаем последний индекс
        return _buttons.Count - 1;
    }

    private int CalculateGridIndex( LaunchButtonView draggedBtn )
    {
        if ( _buttons.Count <= 1 ) return 0;

        int sWidth = _theme.ButtonStyle.SpaceWidth > 0 ? _theme.ButtonStyle.SpaceWidth : 3;
        int sHeight = _theme.ButtonStyle.SpaceHeight > 0 ? _theme.ButtonStyle.SpaceHeight : 3;
        int targetWidth = _theme.ButtonStyle.DefaultWidth > 0 ? _theme.ButtonStyle.DefaultWidth : 60;
        int columns = Math.Max( 1, this.Width / ( targetWidth + sWidth ) );
        int btnWidth = ( this.Width - ( sWidth * ( columns + 1 ) ) ) / columns;

        // Координаты центра перетаскиваемой кнопки
        int centerX = draggedBtn.Left + draggedBtn.Width / 2;
        int centerY = draggedBtn.Top + draggedBtn.Height / 2 - _scrollValue;

        // Имитируем логику размещения GridLayoutEngine
        int currentY = _theme.ContainerStyle.HeaderHeight + sHeight;
        int currentCol = 0;

        for ( int i = 0; i < _buttons.Count; i++ )
        {
            var btn = _buttons[ i ];
            Rectangle cellRect;

            if ( btn.IsSeparator )
            {
                // Если перед разделителем был неполный ряд — закрываем его
                if ( currentCol > 0 ) currentY += _theme.ButtonStyle.DefaultHeight + sHeight;

                // Прямоугольник разделителя (на всю ширину)
                cellRect = new Rectangle( sWidth, currentY, this.Width - ( sWidth * 2 ), btn.Height );

                // Если мышка в верхней половине разделителя — вставляем ПЕРЕД ним
                if ( centerY < cellRect.Top + cellRect.Height / 2 ) return i;

                currentY += btn.Height + sHeight;
                currentCol = 0;
            }
            else
            {
                // Прямоугольник обычной кнопки в сетке
                int x = sWidth + ( currentCol * ( btnWidth + sWidth ) );
                cellRect = new Rectangle( x, currentY, btnWidth, _theme.ButtonStyle.DefaultHeight );

                // Если центр перетаскиваемой кнопки попал в эту ячейку (или левее/выше неё)
                if ( centerY < cellRect.Bottom && centerX < cellRect.Right ) return i;

                currentCol++;
                if ( currentCol >= columns )
                {
                    currentCol = 0;
                    currentY += _theme.ButtonStyle.DefaultHeight + sHeight;
                }
            }
        }

        return _buttons.Count - 1;
    }

    private void SyncModelOrder()
    {
        // Извлекаем LaunchItem из каждой кнопки в их текущем порядке
        // (Убедись, что у тебя в LaunchButtonView есть ссылка на исходный LaunchItem или все его данные)
        List<LaunchItem> updatedItems = _buttons.Select( ( btn, index ) => new LaunchItem
        {
            Name = btn.Text,
            FilePath = btn.BaseControlPath,
            Height = btn.Height,
            IsSeparator = string.IsNullOrEmpty( btn.BaseControlPath ),
            // ВАЖНО: Перезаписываем ID и обнуляем ClicksCount (или ставим по порядку), 
            // чтобы SortByGroups не разбросал их обратно при загрузке
            Id = index,
            ClicksCount = _buttons.Count - index // Искусственный вес для сохранения порядка
        } ).ToList();

        // Вызываем событие, на которое подписан MainController/MainForm
        ItemCollectionChanged?.Invoke( updatedItems );
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
        // Находим кнопку, которая сейчас в режиме перемещения (Capture или IsDragging)
        var draggedBtn = _buttons.FirstOrDefault( b => b.Capture || b.IsDragging );

        // Передаем её в отрисовщик контейнера
        _containerDrawer.Draw( this, draggedBtn, _mouseState, e );
    }

    void IAnimatable.UpdateVisual() => Invalidate();

    protected override void OnResize( EventArgs e )
    {
        base.OnResize( e );
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

    //private void OnElevatedDragDrop( object sender, ElevatedDragDropArgs e )
    //{
    //    //if ( e.HWnd != Handle ) 
    //    //    return;

    //    //List<LaunchItem> items = e.Files.Select( f => new LaunchItem { FilePath = f, Name = System.IO.Path.GetFileName( f ), Id = _itemsCount++ } ).ToList();

    //    //AddButtons( items );

    //    //SyncModelOrder();

    //    if ( e.HWnd != Handle ) 
    //        return;

    //    if ( e.Files != null && e.Files.Count > 0 )
    //    {
    //        var items = e.Files.Select( f => new LaunchItem
    //        {
    //            FilePath = f,
    //            Name = Path.GetFileName( f ),
    //            Id = _itemsCount++
    //        } ).ToList();

    //        AddButtons( items );
    //    }
    //}

    public void StartProcessGroup( LaunchButtonView separator ) => 
        GroupStartRequested?.Invoke( separator );

    protected override void Dispose( bool disposing )
    {
        if ( disposing )
        {
            //ElevatedDragDropManager.Instance.ElevatedDragDrop -= OnElevatedDragDrop;
        }
        base.Dispose( disposing );
    }

    #endregion
}