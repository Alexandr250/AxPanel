// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

using AxPanel.UI.Drawers;
using AxPanel.UI.Themes;

namespace AxPanel.UI.UserControls;

public class LaunchButtonView : BaseControl
{
    private readonly ITheme _theme;
    private readonly ButtonDrawer _buttonDrawer;
    private MouseState _mouseState = new();
    private KeyboardState _keyboardState = new();
    private Point _lastLocation;
    private Point _lastScreenLocation;
    private Point _dragStartMousePos; // Позиция мыши в момент нажатия (экранная)
    private Point _dragStartControlPos; // Позиция кнопки в момент нажатия (локальная)

    // Свойства состояния процесса
    public bool IsRunning { get; set; }
    public float CpuUsage { get; set; }
    public float RamUsage { get; set; }
    public DateTime? StartTime { get; set; }
    public bool IsDragging { get; set; } // Флаг для аниматора
    //public string Path { get; set; } // Ключ для мониторинга
    public int WindowCount { get; set; }

    public bool IsSeparator => string.IsNullOrEmpty( BaseControlPath );

    // События
    public event Action<LaunchButtonView> ButtonLeftClick;
    public event Action<LaunchButtonView> ButtonMiddleClick;
    public event Action<LaunchButtonView> ButtonRightClick;
    public event Action<LaunchButtonView> DeleteButtonClick;
    public event Action<Rectangle?> MovingChanged;

    // Устаревшее, теперь позицией управляет LayoutEngine через контейнер
    public Func<int> RequestPosition;

    public LaunchButtonView() : this( new DarkTheme() ) {}

    public LaunchButtonView( ITheme theme )
    {
        _theme = theme ?? throw new ArgumentNullException( nameof( theme ) );
        _buttonDrawer = new ButtonDrawer( _theme );

        Dock = DockStyle.None;      // Категорически отключаем Dock
        Anchor = AnchorStyles.None;  // Отключаем Anchor, чтобы Left/Top работали свободно
        AutoSize = false;
    }

    protected override void OnParentChanged( EventArgs e )
    {
        base.OnParentChanged( e );
        // Удалено принудительное растягивание на всю ширину для поддержки сетки

        if ( InvokeRequired )
            BeginInvoke( Invalidate );
        else
            Invalidate();
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
        _lastScreenLocation = Cursor.Position;
        if ( e.Button == MouseButtons.Left )
        {
            _dragStartMousePos = Cursor.Position; // Запоминаем экранную позицию мыши
            _dragStartControlPos = this.Location; // Запоминаем где стояла кнопка
            BringToFront();
            Capture = true;
        }
        base.OnMouseDown( e );
    }

    protected override void OnMouseMove( MouseEventArgs e )
    {
        // 1. ЗОНА УДАЛЕНИЯ (для обычных кнопок)
        if ( !IsSeparator )
        {
            bool isInsideDeleteZone = e.X > Width - _theme.ButtonStyle.DeleteButtonWidth;
            if ( _mouseState.MouseInDeleteButton != isInsideDeleteZone )
            {
                _mouseState.MouseInDeleteButton = isInsideDeleteZone;
                Invalidate();
            }
        }

        // 2. ЗОНА ГРУППОВОГО ЗАПУСКА (для разделителей)
        if ( IsSeparator )
        {
            bool isInsidePlayZone = e.X > Width - 40;
            if ( _mouseState.MouseInGroupPlay != isInsidePlayZone )
            {
                _mouseState.MouseInGroupPlay = isInsidePlayZone;
                Invalidate();
                // Меняем курсор для визуального отклика
                Cursor = isInsidePlayZone ? Cursors.Hand : Cursors.Default;
            }
        }
        else
        {
            // Сброс курсора, если ушли с зоны Play на обычную кнопку
            if ( Cursor == Cursors.Hand ) Cursor = Cursors.Default;
        }

        // 3. ЛОГИКА ПЕРЕМЕЩЕНИЯ (Drag & Drop)
        if ( e.Button == MouseButtons.Left && Capture )
        {
            Point currentMousePos = Cursor.Position;
            int deltaX = currentMousePos.X - _dragStartMousePos.X;
            int deltaY = currentMousePos.Y - _dragStartMousePos.Y;

            // Если сдвинули хоть немного — двигаем физически
            if ( Math.Abs( deltaX ) > 2 || Math.Abs( deltaY ) > 2 )
            {
                if( !_mouseState.ButtonMoved )
                    MovingChanged?.Invoke( new Rectangle( Left, Top, Width, Height ) );

                _mouseState.ButtonMoved = true;
                

                IsDragging = true;

                // Устанавливаем позицию напрямую от стартовой точки
                // Это исключает "накопление ошибки" при быстрых рывках
                this.Left = _dragStartControlPos.X + deltaX;
                this.Top = _dragStartControlPos.Y + deltaY;
            }
        }

        base.OnMouseMove( e );
    }

    protected override void OnMouseUp( MouseEventArgs e )
    {
        IsDragging = false;

        if ( e.Button == MouseButtons.Left )
        {
            Capture = false;

            if ( !_mouseState.ButtonMoved )
            {
                if( false && _mouseState.MouseInDeleteButton )
                {
                    DeleteButtonClick?.Invoke( this );
                }
                else
                    ButtonLeftClick?.Invoke( this );
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
        _mouseState.MouseInGroupPlay = false;
        Invalidate();
        base.OnMouseLeave( e );
    }

    protected override void OnMouseClick( MouseEventArgs e )
    {
        base.OnMouseClick( e );

        // Если это разделитель и клик был в правой части
        if ( IsSeparator && e.X > Width - 40 )
        {
            // Вызываем специальное событие или действие
            StartGroupLaunch();
        }
    }

    private void StartGroupLaunch()
    {
        // Находим родительский контейнер и просим его запустить группу
        if ( Parent is ButtonContainerView container )
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

    public void UpdateState( bool isRunning, float cpuUsage, float ramMb, int windowCount, DateTime? startTime )
    {
        // Добавляем проверку WindowCount != windowCount в общий флаг изменений
        bool changed = IsRunning != isRunning ||
                       WindowCount != windowCount ||
                       Math.Abs( CpuUsage - cpuUsage ) > 0.5f ||
                       Math.Abs( RamUsage - ramMb ) > 0.5f ||
                       StartTime != startTime;

        if ( changed )
        {
            IsRunning = isRunning;
            CpuUsage = isRunning ? cpuUsage : 0;
            RamUsage = isRunning ? ramMb : 0;
            WindowCount = isRunning ? windowCount : 0; // Сбрасываем в 0, если не запущен
            StartTime = isRunning ? startTime : null;

            if ( InvokeRequired )
                BeginInvoke( ( Action )Invalidate );
            else
                Invalidate();
        }
    }
}