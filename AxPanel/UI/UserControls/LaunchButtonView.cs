// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

using AxPanel.Model;
using AxPanel.UI.Drawers;
using AxPanel.UI.Themes;
using System.Diagnostics;

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

    public ProcessStats Stats { get; set; }

    public bool IsDragging { get; set; } // Флаг для аниматора

    public bool IsSeparator => string.IsNullOrEmpty( BaseControlPath );

    // События
    public event Action<LaunchButtonView, object?> ButtonLeftClick;
    public event Action<LaunchButtonView> ButtonMiddleClick;
    public event Action<LaunchButtonView> ButtonRightClick;
    public event Action<LaunchButtonView> DeleteButtonClick;
    public event Action<Rectangle?> MovingChanged;
    public event Action<LaunchButtonView> Dragging;

    // Устаревшее, теперь позицией управляет LayoutEngine через контейнер
    public Func<int> RequestPosition;

    public LaunchButtonView() : this( /*new DarkTheme()*/ new OldTheme() ) {}

    public LaunchButtonView( ITheme theme )
    {
        _theme = theme ?? throw new ArgumentNullException( nameof( theme ) );
        _buttonDrawer = new ButtonDrawer( _theme );

        Dock = DockStyle.None; 
        Anchor = AnchorStyles.None; 
        AutoSize = false;

        AllowDrop = true;

        this.GiveFeedback += OnGiveFeedback;
    }

    private void OnGiveFeedback( object sender, GiveFeedbackEventArgs e )
    {
        // Система просит обратную связь - мы используем этот момент, чтобы подвинуть кнопку
        e.UseDefaultCursors = true;

        if ( IsDragging && Parent != null )
        {
            // Получаем текущую позицию мыши (экранную) и переводим в координаты родителя
            Point clientPos = Parent.PointToClient( Cursor.Position );

            // Смещаем кнопку так, чтобы курсор был там же, где в начале захвата
            this.Left = clientPos.X - _lastLocation.X;
            this.Top = clientPos.Y - _lastLocation.Y;

            // Оповещаем контейнер, чтобы он раздвигал другие кнопки
            Dragging?.Invoke( this );
        }
    }

    protected override void OnDragEnter( DragEventArgs dragEventArgs )
    {
        base.OnDragEnter( dragEventArgs );

        if ( dragEventArgs.Data.GetDataPresent( DataFormats.FileDrop ) && !IsSeparator )
        {
            dragEventArgs.Effect = DragDropEffects.Link; // Иконка связи
            _mouseState.IsDragOver = true; // Установим флаг для отрисовки
            Invalidate();
        }
        else
        {
            dragEventArgs.Effect = DragDropEffects.None;
        }
    }

    protected override void OnDragLeave( EventArgs e )
    {
        base.OnDragLeave( e );
        _mouseState.IsDragOver = false;
        Invalidate();
    }

    protected override void OnDragDrop( DragEventArgs dragEventArgs )
    {
        _mouseState.IsDragOver = false;
        Invalidate();

        base.OnDragDrop( dragEventArgs );

        // Получаем массив путей (даже если бросили один файл)
        string[] files = ( string[] )dragEventArgs.Data.GetData( DataFormats.FileDrop );

        if ( files is { Length: > 0 } && !IsSeparator )
        {
            string arguments = string.Join( " ", files.Select( f => $"\"{f}\"" ) );

            try
            {
                ButtonLeftClick?.Invoke( this, arguments );
            }
            catch ( Exception ex )
            {
                MessageBox.Show( $"Ошибка запуска: {ex.Message}" );
            }
        }
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
        _buttonDrawer.Draw( this, _mouseState, _keyboardState, e, Stats.IsRunning ? Stats.CpuUsage : 0 );
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
                //if( !_mouseState.ButtonMoved )
                //    MovingChanged?.Invoke( new Rectangle( Left, Top, Width, Height ) );

                //_mouseState.ButtonMoved = true;

                //if ( !IsDragging )
                //{
                //    IsDragging = true;
                //}

                //// Устанавливаем позицию напрямую от стартовой точки
                //// Это исключает "накопление ошибки" при быстрых рывках
                //this.Left = _dragStartControlPos.X + deltaX;
                //this.Top = _dragStartControlPos.Y + deltaY;

                //Dragging?.Invoke( this );

                //this.Capture = false;

                //// 3. Запускаем системный DragDrop. 
                //// ВНИМАНИЕ: код ПАУЗИТСЯ здесь до момента отпускания мыши (Drop)
                //var effect = this.DoDragDrop( this, DragDropEffects.Move );

                //// 4. После того как Drop произошел, сбрасываем флаг
                //IsDragging = false;
                //Invalidate();

                _mouseState.ButtonMoved = true;
                IsDragging = true;
                this.Capture = false;

                // Запускаем - поток замрет здесь, но OnGiveFeedback будет "простреливать" изнутри
                this.DoDragDrop( this, DragDropEffects.Move );

                // Сюда попадем только когда отпустим мышь
                IsDragging = false;
                _mouseState.ButtonMoved = false;
                Invalidate();
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
                {
                    string? args = string.IsNullOrEmpty( Arguments ) ? null : Arguments;
                    ButtonLeftClick?.Invoke( this, args );
                }
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

    public void UpdateState( ProcessStats stats )
    {
        // Добавляем проверку WindowCount != windowCount в общий флаг изменений
        bool changed = Stats.IsRunning != stats.IsRunning ||
                       Stats.WindowCount != stats.WindowCount ||
                       Math.Abs( Stats.CpuUsage - stats.CpuUsage ) > 0.5f ||
                       Math.Abs( Stats.RamMb - stats.RamMb ) > 0.5f ||
                       Stats.StartTime != stats.StartTime;

        if ( changed )
        {
            Stats = new ProcessStats()
            {
                IsRunning = stats.IsRunning,
                CpuUsage = stats.IsRunning ? stats.CpuUsage : 0,
                RamMb = stats.IsRunning ? stats.RamMb : 0,
                WindowCount = stats.IsRunning ? stats.WindowCount : 0, // Сбрасываем в 0, если не запущен
                StartTime = stats.IsRunning ? stats.StartTime : null
            };

            if ( InvokeRequired )
                BeginInvoke( ( Action )Invalidate );
            else
                Invalidate();
        }
    }
}