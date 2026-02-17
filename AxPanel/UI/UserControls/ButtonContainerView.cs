using AxPanel.Contracts;
using AxPanel.Model;
using AxPanel.UI.Drawers;
using AxPanel.UI.ElementStyles;
using AxPanel.UI.Themes;

namespace AxPanel.UI.UserControls;

public class ButtonContainerView : BasePanelControl, IAnimatable
{
    private readonly System.Windows.Forms.Timer _dragHoverTimer = new() { Interval = 400 }; // 400мс — оптимально

    private ContainerDrawer _containerDrawer;
    private readonly IButtonFactory _buttonFactory;
    private NativeDropHandler _nativeDrop;
    private readonly ITheme _theme;

    private readonly List<LaunchButtonView> _buttons = [];
    private int _scrollValue = 0;

    private MouseState _mouseState = new();

    public ILayoutEngine LayoutEngine { get; set; }
    public IReadOnlyList<LaunchButtonView> Buttons => _buttons;
    public string PanelName { get; set; }
    public bool IsWaitingForExpand { get; set; }
    public int ScrollValue => _scrollValue;
    ITheme IAnimatable.Theme => _theme;

    public ButtonContainerEvents ButtonContainerEvents { get; } = new();

    public ButtonContainerView()
    {
        _theme = new Theme();
        ( ( Theme )_theme ).ContainerStyle = new ContainerStyle();
        ( ( Theme )_theme ).ButtonStyle = new ButtonStyle();
        ( ( Theme )_theme ).WindowStyle = new WindowStyle();

        LayoutEngine = new GridLayoutEngine();

        _containerDrawer = new ContainerDrawer( _theme );

        _buttonFactory = new ButtonFactory( _theme );

        InitContainer();
    }

    public ButtonContainerView( ITheme theme, MainConfig mainConfig )
    {
        _theme = theme ?? throw new ArgumentNullException( nameof( theme ) );

        LayoutEngine = mainConfig.LayoutMode == LayoutMode.List ?
            new ListLayoutEngine() :
            new GridLayoutEngine();

        _buttonFactory = new ButtonFactory( _theme );

        InitContainer();
    }

    public void NotifyProcessStart( LaunchButtonView button, object? args )
        => ButtonContainerEvents.RaiseProcessStart( button, args );

    public void NotifyExplorerOpen( string path )
        => ButtonContainerEvents.RaiseExplorer( path );

    public void NotifyProcessStartAsAdmin( LaunchButtonView button )
        => ButtonContainerEvents.RaiseProcessAdminStart( button );

    public void HandleButtonDragMove()
    {
        _mouseState.ButtonMoved = true;
        ReorderButtons();
    }

    public void HandleButtonDragEnd()
    {
        if ( _mouseState.ButtonMoved )
        {
            SyncModelOrder();
            _mouseState.ButtonMoved = false;
        }
    }

    public void RemoveButton( LaunchButtonView b )
    {
        _buttons.Remove( b );
        Controls.Remove( b );
        b.Dispose();
        SyncState();
    }

    public void HandleButtonReorder( LaunchButtonView b )
    {
        Point center = new( b.Left + b.Width / 2, b.Top + b.Height / 2 );
        int targetIndex = LayoutEngine.GetIndexAt( center, _scrollValue, Width, _buttons, _theme );
        int oldIndex = _buttons.IndexOf( b );

        if ( targetIndex != -1 && targetIndex != oldIndex )
        {
            _buttons.RemoveAt( oldIndex );
            _buttons.Insert( targetIndex, b );
            ReorderButtons();
            ButtonContainerEvents.RaiseCollectionChanged( null );
        }
    }

    private void InitContainer()
    {
        _containerDrawer = new ContainerDrawer( _theme );

        BackColor = _theme.ContainerStyle.BackColor;
        MouseWheel += HandleMouseWheel;
        AllowDrop = true;

        _dragHoverTimer.Tick += ( s, e ) =>
        {
            _dragHoverTimer.Stop();
            ButtonContainerEvents.RaiseDragHover( this );
        };
    }

    protected override void OnHandleCreated( EventArgs e )
    {
        base.OnHandleCreated( e );

        _nativeDrop = new NativeDropHandler( Handle, files => {
            List<LaunchItem> items = files.Select( f => new LaunchItem
            {
                FilePath = f,
                Name = Path.GetFileName( f )
            } ).ToList();

            BeginInvoke( () =>
            {
                AddButtons( items ); 
                SyncModelOrder();
            } );
        } );
    }

    protected override void WndProc( ref Message m )
    {
        if ( _nativeDrop != null && _nativeDrop.HandleWndProc( ref m ) ) 
            return;

        base.WndProc( ref m );
    }

    protected override void OnDragEnter( DragEventArgs drgevent )
    {
        if( _nativeDrop != null )
            drgevent.Effect = _nativeDrop.GetDragEffect( drgevent );
    }

    protected override void OnDragDrop( DragEventArgs drgevent )
    {
        List<LaunchItem> items = _nativeDrop.ParseDroppedData( drgevent );

        if ( items.Count > 0 )
        {
            AddButtons( items );
            SyncModelOrder();
            return;
        }

        base.OnDragDrop( drgevent );
    }
    
    public void MoveButtonToThisContainer( LaunchButtonView btn )
    {
        ButtonContainerView? oldParent = btn.Parent as ButtonContainerView;

        // 1. Удаляем из старой логики
        oldParent?._buttons.Remove( btn );
        oldParent?.Controls.Remove( btn );

        // 2. Добавляем в новую логику
        _buttons.Add( btn );
        Controls.Add( btn );

        // 3. Обновляем иерархию и мониторинг
        SyncState();
        oldParent?.SyncState();

        // 4. Генерируем событие для сохранения конфига (если нужно)
        ButtonContainerEvents.RaiseCollectionChanged( null );
    }

    public void ApplyStats( Dictionary<string, ProcessStats> stats )
    {
        bool changed = false;
        foreach ( LaunchButtonView btn in _buttons )
        {
            if ( btn.BaseControlPath != null && stats.TryGetValue( btn.BaseControlPath, out ProcessStats s ) )
            {
                btn.UpdateState( s );
                changed = true;
            }
        }
        if ( changed ) Invalidate( true );
    }

    public void SyncState()
    {
        ReorderButtons();
        ButtonContainerEvents.RaiseCollectionChanged( null );
    }

    public void ReorderButtons()
    {
        SuspendLayout();

        for ( int i = 0; i < _buttons.Count; i++ )
        {
            LaunchButtonView button = _buttons[ i ];

            if ( button.IsDragging || button.Capture ) 
                continue;

            (Point Location, int Width) layout = LayoutEngine.GetLayout( i, _scrollValue, Width, _buttons, _theme );

            if ( button.Location != layout.Location || button.Width != layout.Width )
            {
                button.Location = layout.Location;
                button.Width = layout.Width;
            }
        }

        ResumeLayout( false );
        Invalidate();
    }

    private void HandleMouseWheel( object sender, MouseEventArgs e )
    {
        int delta = e.Delta > 0 ? _theme.ContainerStyle.ScrollValueIncrement : -_theme.ContainerStyle.ScrollValueIncrement;

        int totalHeight = LayoutEngine.GetTotalContentHeight( Buttons, Width, _theme );
        int visibleHeight = Height;

        _scrollValue += delta;

        // Ограничение: не крутим выше начала и ниже конца контента
        int maxScroll = Math.Min( 0, visibleHeight - totalHeight );
        _scrollValue = Math.Clamp( _scrollValue, maxScroll, 0 );

        ReorderButtons();
    }
    
    public void AddButtons( List<LaunchItem> items )
    {
        if ( items is not { Count: not 0 } ) 
            return;

        List<LaunchButtonView> newButtons = _buttonFactory.CreateAll( items, this );

        foreach ( LaunchButtonView btn in newButtons )
        {
            _buttons.Add( btn );
            Controls.Add( btn );
        }

        ReorderButtons();
        SyncState();
    }

    public void SyncModelOrder()
    {
        List<LaunchItem> updatedItems = _buttons.Select( ( btn, index ) => new LaunchItem
        {
            Name = btn.Text,
            FilePath = btn.BaseControlPath,
            Height = btn.Height,
            IsSeparator = string.IsNullOrEmpty( btn.BaseControlPath ),
            Id = index,
            ClicksCount = _buttons.Count - index // Искусственный вес для сохранения порядка
        } ).ToList();

        ButtonContainerEvents.RaiseCollectionChanged( updatedItems ); 
    }

    public void StartProcessGroup( LaunchButtonView separator ) =>
        ButtonContainerEvents.RaiseGroupStart( separator ); 

    public void StartDragHoverTimer()
    {
        _dragHoverTimer.Start();
        IsWaitingForExpand = true;
        Invalidate();
    }

    public void StopDragHoverTimer()
    {
        _dragHoverTimer.Stop();
        IsWaitingForExpand = false;
        Invalidate();
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
        LaunchButtonView? draggedBtn = _buttons.FirstOrDefault( b => b.Capture || b.IsDragging );
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
        SyncState();
        ButtonContainerEvents.RaiseSelected( this );
    }

    protected override void Dispose( bool disposing )
    {
        if( disposing )
        {
            ButtonContainerEvents?.Dispose();

            _dragHoverTimer?.Stop();
            _dragHoverTimer?.Dispose();

            foreach ( LaunchButtonView btn in _buttons ) 
                btn.Dispose();

            _buttons.Clear();
        }
        base.Dispose( disposing );
    }
}