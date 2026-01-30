// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

using AxPanel.UI.Drawers;
using AxPanel.UI.Themes;

namespace AxPanel.UI.UserControls;

public class LaunchButton : BaseControl
{
    private readonly ITheme _theme;
    private readonly ButtonDrawer _buttonDrawer;
    private MouseState _mouseState = new();
    private KeyboardState _keyboardState = new();
    private Point _lastLocation;

    public bool IsRunning { get; set; }
    public float CpuUsage { get; set; }
    public float RamUsage { get; set; }
    public DateTime? StartTime { get; set; }

    // Используем события вместо публичных Action
    public event Action<LaunchButton> ButtonLeftClick;
    public event Action<LaunchButton> ButtonMiddleClick;
    public event Action<LaunchButton> ButtonRightClick;
    public event Action<LaunchButton> DeleteButtonClick;

    public Func<int> RequestPosition;

    public LaunchButton( ITheme theme )
    {
        _theme = theme ?? throw new ArgumentNullException( nameof( theme ) );
        _buttonDrawer = new ButtonDrawer( _theme );

        DoubleBuffered = true;
        this.SetStyle( ControlStyles.AllPaintingInWmPaint |
                       ControlStyles.UserPaint |
                       ControlStyles.OptimizedDoubleBuffer |
                       ControlStyles.ResizeRedraw, true );
    }

    protected override void OnParentChanged( EventArgs e )
    {
        base.OnParentChanged( e );
        if ( Parent != null ) Width = Parent.Width;
    }

    protected override void OnPaint( PaintEventArgs e )
    {
        _buttonDrawer.Draw( this, _mouseState, _keyboardState, e, IsRunning ? CpuUsage : 0 );
    }

    protected override void OnMouseEnter( EventArgs e )
    {
        _mouseState.MouseInControl = true;
        Invalidate();
        base.OnMouseEnter( e );
    }

    protected override void OnMouseDown( MouseEventArgs e )
    {
        _lastLocation = e.Location;
        if ( e.Button == MouseButtons.Left ) BringToFront();
        if ( e.Button == MouseButtons.Left ) this.Capture = true;
        base.OnMouseDown( e );
    }

    protected override void OnMouseMove( MouseEventArgs e )
    {
        // Проверка зоны кнопки удаления
        bool isInsideDeleteZone = e.X > Width - _theme.ButtonStyle.DeleteButtonWidth;
        if ( _mouseState.MouseInDeleteButton != isInsideDeleteZone )
        {
            _mouseState.MouseInDeleteButton = isInsideDeleteZone;
            Invalidate();
        }

        if ( e.Button == MouseButtons.Left )
        {
            int deltaY = e.Y - _lastLocation.Y;
            if ( deltaY != 0 )
            {
                _mouseState.ButtonMoved = true;
                this.Top += deltaY;
            }
        }
        base.OnMouseMove( e );
    }

    protected override void OnMouseUp( MouseEventArgs e )
    {
        this.Capture = false;

        if ( e.Button == MouseButtons.Left )
        {
            // 1. Сначала обрабатываем клик, если смещения почти не было
            if ( !_mouseState.ButtonMoved )
            {
                if ( _mouseState.MouseInDeleteButton )
                    DeleteButtonClick?.Invoke( this );
                else
                    ButtonLeftClick?.Invoke( this );
            }

            // 2. ВАЖНО: Возвращаем кнопку на "законное" место
            // Это сбросит результат перетаскивания (this.Top += deltaY)
            if ( RequestPosition != null && !_mouseState.ButtonMoved )
            {
                // Если клик был без движения, принудительно возвращаем (или даем таймеру довести)
                // Но лучше оставить для мгновенного отклика при клике:
                this.Top = RequestPosition();
            }
        }
        else if ( e.Button == MouseButtons.Right )
        {
            ButtonRightClick?.Invoke( this );
        }
        else if ( e.Button == MouseButtons.Middle )
        {
            ButtonMiddleClick?.Invoke( this );
        }

        _mouseState.ButtonMoved = false;
        base.OnMouseUp( e );
    }

    protected override void OnMouseLeave( EventArgs e )
    {
        _mouseState.MouseInControl = false;
        _mouseState.MouseInDeleteButton = false;
        Invalidate();
        base.OnMouseLeave( e );
    }

    public void RaiseKeyDown( KeyEventArgs keyArgs )
    {
        if ( keyArgs.Alt && _mouseState.MouseInControl )
        {
            _keyboardState.AltPressed = true;
            Invalidate();
        }
    }

    public void RaiseKeyUp( KeyEventArgs keyArgs )
    {
        // Keys.Menu — это системный код клавиши Alt
        if ( keyArgs.KeyCode == Keys.Menu || keyArgs.KeyCode == Keys.Alt )
        {
            _keyboardState.AltPressed = false;
            Invalidate();
        }
    }
}