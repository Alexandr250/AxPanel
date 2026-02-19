using AxPanel.SL;

namespace AxPanel.UI.UserControls;

public abstract class BaseControl : Control
{
    private string? _path;
    private Icon? _icon;
    private string? _iconPath;
    private bool _isLoading = false;
    private string _latestTarget = "";
    
    private readonly object _lock = new();
    
    public string? DownloadUrl { get; set; }

    public bool IsArchive { get; set; }

    public string? Arguments { get; set; }

    public Icon Icon
    {
        get
        {
            lock ( _lock )
            {
                if ( _icon == null && !_isLoading )
                {
                    _isLoading = true;
                    _icon = IconProvider.DefaultPlaceholder;

                    Task.Run( () => LoadIconAsync() );
                }
                return _icon ?? IconProvider.DefaultPlaceholder;
            }
        }
    }

    public string? IconPath
    {
        get => _iconPath;
        set { if ( _iconPath == value ) return; _iconPath = value; ResetIcon(); }
    }

    public string? BaseControlPath
    {
        get => _path;
        set { if ( _path == value ) return; _path = value; ResetIcon(); }
    }

    public BaseControl() 
    {
        DoubleBuffered = true;

        SetStyle( ControlStyles.AllPaintingInWmPaint |
                  ControlStyles.UserPaint |
                  ControlStyles.OptimizedDoubleBuffer |
                  ControlStyles.ResizeRedraw, true );

        _icon = IconProvider.DefaultPlaceholder;
    }

    private void LoadIconAsync()
    {
        string? currentTarget = !string.IsNullOrWhiteSpace( IconPath ) ? IconPath : BaseControlPath;

        lock ( _lock ) { _latestTarget = currentTarget ?? ""; }

        try
        {
            Icon? loadedIcon = IconProvider.GetIcon( currentTarget );

            if ( IsDisposed || Disposing ) { loadedIcon?.Dispose(); return; }

            BeginInvoke( () =>
            {
                lock ( _lock )
                {
                    if ( _latestTarget == currentTarget )
                    {
                        if ( loadedIcon != null ) _icon = loadedIcon;
                    }
                    else
                    {
                        loadedIcon?.Dispose();
                    }
                    _isLoading = false;
                }
                Invalidate();
            } );
        }
        catch
        {
            lock ( _lock ) { _isLoading = false; }
        }
    }

    private void ResetIcon()
    {
        lock ( _lock )
        {
            if ( _icon != null && _icon != IconProvider.DefaultPlaceholder )
                _icon.Dispose();

            _icon = null;
            _isLoading = false; 
        }
        Invalidate();
    }

    protected override void Dispose( bool disposing )
    {
        if ( disposing )
        {
            if ( _icon != IconProvider.DefaultPlaceholder ) 
                _icon?.Dispose();
        }
        base.Dispose( disposing );
    }    
}
