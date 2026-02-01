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

    // Свойства состояния процесса
    public bool IsRunning { get; set; }
    public float CpuUsage { get; set; }
    public float RamUsage { get; set; }
    public DateTime? StartTime { get; set; }
    //public string Path { get; set; } // Ключ для мониторинга

    // События
    public event Action<LaunchButton> ButtonLeftClick;
    public event Action<LaunchButton> ButtonMiddleClick;
    public event Action<LaunchButton> ButtonRightClick;
    public event Action<LaunchButton> DeleteButtonClick;

    // Устаревшее, теперь позицией управляет LayoutEngine через контейнер
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

        this.Dock = DockStyle.None;      // Категорически отключаем Dock
        this.Anchor = AnchorStyles.None;  // Отключаем Anchor, чтобы Left/Top работали свободно
        this.AutoSize = false;
    }

    protected override void OnParentChanged( EventArgs e )
    {
        base.OnParentChanged( e );
        // Удалено принудительное растягивание на всю ширину для поддержки сетки
    }

    protected override void OnPaint( PaintEventArgs e )
    {
        // Передаем текущий размер кнопки (Width/Height) в отрисовщик
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
        if ( e.Button == MouseButtons.Left )
        {
            BringToFront();
            this.Capture = true;
        }
        base.OnMouseDown( e );
    }

    protected override void OnMouseMove( MouseEventArgs e )
    {
        // Проверка зоны кнопки удаления (адаптирована под динамическую ширину)
        bool isInsideDeleteZone = e.X > Width - _theme.ButtonStyle.DeleteButtonWidth;
        if ( _mouseState.MouseInDeleteButton != isInsideDeleteZone )
        {
            _mouseState.MouseInDeleteButton = isInsideDeleteZone;
            Invalidate();
        }

        if ( e.Button == MouseButtons.Left && this.Capture )
        {
            int deltaX = e.X - _lastLocation.X;
            int deltaY = e.Y - _lastLocation.Y;

            if ( deltaX != 0 || deltaY != 0 )
            {
                _mouseState.ButtonMoved = true;
                this.Left += deltaX;
                this.Top += deltaY;
            }
        }

        // Если это разделитель (нет пути)
        if ( string.IsNullOrEmpty( this.BaseControlPath ) )
        {
            // Проверяем попадание в зону кнопки Play (правый край)
            bool isInsidePlayZone = e.X > Width - 40;
            if ( _mouseState.MouseInGroupPlay != isInsidePlayZone )
            {
                _mouseState.MouseInGroupPlay = isInsidePlayZone;
                Invalidate(); // Перерисовываем для эффекта подсветки
            }
        }

        if ( e.Button == MouseButtons.Left )
        {
            // Если протащили кнопку достаточно далеко (например, 10 пикселей)
            if ( Math.Abs( e.Y - _lastLocation.Y ) > 10 || Math.Abs( e.X - _lastLocation.X ) > 10 )
            {
                _mouseState.ButtonMoved = true;
                // Начинаем системный Drag&Drop. Передаем саму кнопку как данные.
                this.DoDragDrop( this, DragDropEffects.Move );
            }
        }

        base.OnMouseMove( e );
    }

    protected override void OnMouseUp( MouseEventArgs e )
    {
        if ( e.Button == MouseButtons.Left )
        {
            this.Capture = false;

            if ( !_mouseState.ButtonMoved )
            {
                if ( _mouseState.MouseInDeleteButton )
                    DeleteButtonClick?.Invoke( this );
                else
                    ButtonLeftClick?.Invoke( this );
            }

            // ВАЖНО: Мы больше не вызываем RequestPosition здесь вручную.
            // Контейнер AxPanelContainer.AnimateStep сам плавно вернет кнопку 
            // в позицию, рассчитанную текущим LayoutEngine.
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

    protected override void OnMouseClick( MouseEventArgs e )
    {
        base.OnMouseClick( e );

        // Если это разделитель и клик был в правой части
        if ( string.IsNullOrEmpty( this.BaseControlPath ) && e.X > Width - 40 )
        {
            // Вызываем специальное событие или действие
            StartGroupLaunch();
        }
    }

    private void StartGroupLaunch()
    {
        // Находим родительский контейнер и просим его запустить группу
        if ( Parent is AxPanelContainer container )
        {
            container.StartProcessGroup( this );
        }
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
        if ( keyArgs.KeyCode == Keys.Menu || keyArgs.KeyCode == Keys.Alt )
        {
            _keyboardState.AltPressed = false;
            Invalidate();
        }
    }

    public void UpdateState( bool isRunning, float cpuUsage, float ramMb, DateTime? startTime )
    {
        bool changed = IsRunning != isRunning ||
                       Math.Abs( CpuUsage - cpuUsage ) > 0.5f ||
                       Math.Abs( RamUsage - ramMb ) > 0.5f ||
                       StartTime != startTime;

        if ( changed )
        {
            this.IsRunning = isRunning;
            this.CpuUsage = isRunning ? cpuUsage : 0;
            this.RamUsage = isRunning ? ramMb : 0;
            this.StartTime = isRunning ? startTime : null;

            if ( this.InvokeRequired )
                this.BeginInvoke( new Action( Invalidate ) );
            else
                this.Invalidate();
        }
    }
}