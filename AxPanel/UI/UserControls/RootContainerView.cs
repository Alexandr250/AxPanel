using AxPanel.Model;
using AxPanel.SL;
using AxPanel.UI.Themes;

namespace AxPanel.UI.UserControls;

public class RootContainerView : Panel
{
    private readonly ContainerService _containerService = new();
    private readonly ProcessMonitor _globalMonitor = new();
    private readonly GlobalAnimator _animator = new();
    private readonly ContainerAnimator _containerAnimator;
    private readonly ButtonContainerFactory _buttonContainerFactory;

    private readonly ITheme _theme;
    private readonly MainConfig _mainConfig;

    private ButtonContainerView _selected;
    
    public IEnumerable<ButtonContainerView> Containers => Controls.OfType<ButtonContainerView>();

    public event Action OnSaveConfigRequered;    

    public Footer Footer { get; }

    public ButtonContainerView Selected
    {
        get => _selected;
        private set
        {
            if ( _selected == value ) 
                return;

            _selected = value;
            _containerAnimator.StartAnimateArrange();
        }
    }

    public RootContainerView( ITheme theme, MainConfig mainConfig )
    {
        _theme = theme ?? throw new ArgumentNullException( nameof( theme ) );
        _mainConfig = mainConfig;
        Footer = new Footer( _theme, 25 );

        InitializeComponent();
        StartMonitoring();

        _containerAnimator = new ContainerAnimator( this, _theme );
        _containerAnimator.HoverRequested += container => Selected = container;

        _buttonContainerFactory = new ButtonContainerFactory( _theme, _mainConfig, this, _containerService, _animator );
        _buttonContainerFactory.SelectedChangedRequested += buttonContainerView => Selected = buttonContainerView;
        _buttonContainerFactory.LayoutUpdateRequested += ArrangeContainers;
        _buttonContainerFactory.MonitorPathsUpdateRequested += UpdateGlobalMonitorPaths;
        _buttonContainerFactory.SaveConfigRequested += () => OnSaveConfigRequered?.Invoke();
    }

    private void InitializeComponent()
    {
        DoubleBuffered = true;
        SetStyle( ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true );
        SetStyle( ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true );
        BackColor = _theme.WindowStyle.BackColor;
    }

    private void StartMonitoring()
    {
        _globalMonitor.StatisticsUpdated += OnGlobalStatsReceived;
        _globalMonitor.Start();
    }

    public ButtonContainerView AddContainer( string name, List<LaunchItem>? items )
    {
        return _buttonContainerFactory.AddContainer( name, items );
    }

    private void UpdateGlobalMonitorPaths()
    {
        HashSet<string?> allPaths = Containers
            .SelectMany( c => c.Buttons )
            .Where( b => !string.IsNullOrEmpty( b.BaseControlPath ) )
            .Select( b => b.BaseControlPath )
            .ToHashSet( StringComparer.OrdinalIgnoreCase );

        _globalMonitor.TargetPaths = allPaths;
    }

    private void OnGlobalStatsReceived( Dictionary<string, ProcessStats> stats )
    {
        foreach ( ButtonContainerView container in Containers )
        {
            if ( container.InvokeRequired )
                container.BeginInvoke( () => container.ApplyStats( stats ) );
            else
                container.ApplyStats( stats );
        }
    }

    public void ArrangeContainers()
    {
        _containerAnimator.StopAnimateArrange();

        int currentTop = 0;
        int selHeight = Height - ( Containers.Count() - 1 ) * _theme.ContainerStyle.HeaderHeight - Footer.Height;

        foreach ( ButtonContainerView container in Containers )
        {
            container.SetBounds( 0, currentTop, Width, container == Selected ? selHeight : _theme.ContainerStyle.HeaderHeight );
            currentTop += container.Height;
        }
        Invalidate();
    }

    protected override void OnMouseClick( MouseEventArgs e )
    {
        base.OnMouseClick( e );
        Invalidate();
    }

    protected override void OnMouseDown( MouseEventArgs e )
    {
        base.OnMouseDown( e );

        FooterButton? targetBtn = Footer.HitTest( Width, Height, e.Location );
        
        if ( targetBtn == null ) 
            return;

        if ( e is { Button: MouseButtons.Left, Clicks: 2 } )
        {
            OnSaveConfigRequered?.Invoke();
            targetBtn.Action?.Invoke();
        }
        else if ( e.Button == MouseButtons.Right )
        {
            ProcessManager.Abort();
            MessageBox.Show( @"Выключение отменено", @"Статус", MessageBoxButtons.OK, MessageBoxIcon.Information );
            Invalidate( Footer.GetFullBounds( Width, Height ) );
        }
    }

    protected override void OnMouseMove( MouseEventArgs e )
    {
        base.OnMouseMove( e );

        if ( e.Y >= Height - Footer.Height ) 
            Invalidate( Footer.GetFullBounds( Width, Height ) );
    }

    protected override void OnMouseLeave( EventArgs e )
    {
        base.OnMouseLeave( e );
        Invalidate( new Rectangle( 0, Height - Footer.Height, Width, Footer.Height ) );
    }

    protected override void OnResize( EventArgs e )
    {
        base.OnResize( e );
        ArrangeContainers();
    }

    protected override void OnPaint( PaintEventArgs e )
    {
        base.OnPaint( e );
        Footer.Draw( e.Graphics, Width, Height, PointToClient( Cursor.Position ) );
    }

    protected override void OnParentChanged( EventArgs e )
    {
        base.OnParentChanged( e );
        if ( Parent != null )
        {
            Dock = DockStyle.None;
            Location = new Point( _mainConfig.BorderWidth, _mainConfig.HeaderHeight );
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            ArrangeContainers();
        }
    }
    
    protected override void Dispose( bool disposing )
    {
        if ( disposing )
        {
            _globalMonitor.Stop();
            _globalMonitor.Dispose();

            _animator.Dispose();
            _containerAnimator.Dispose();
        }
        base.Dispose( disposing );
    }
}