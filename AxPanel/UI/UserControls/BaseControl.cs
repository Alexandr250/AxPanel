using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AxPanel.UI.UserControls;

public abstract class BaseControl : Control
{
    private string _path;
    private Icon _icon;
    private CancellationTokenSource _cts; // Для отмены старой загрузки

    public BaseControl()
    {
        DoubleBuffered = true;

        SetStyle( ControlStyles.AllPaintingInWmPaint |  // Игнорировать WM_ERASEBKGND для уменьшения мерцания
                       ControlStyles.UserPaint |             // Контрол рисует себя сам
                       ControlStyles.OptimizedDoubleBuffer | // Использовать буфер в памяти
                       ControlStyles.ResizeRedraw, true );   // Перерисовывать при изменении размера
    }

    public string BaseControlPath
    {
        get => _path;
        set
        {
            if ( _path == value ) 
                return;
            _path = value;

            // Запускаем асинхронное обновление
            UpdateIconAsync( _path );
        }
    }

    public Icon Icon
    {
        get => _icon;
        private set // Сеттер приватный, меняется только через Path
        {
            _icon?.Dispose();
            _icon = value;
            Invalidate();
        }
    }

    private async void UpdateIconAsync( string targetPath )
    {
        // 1. Отменяем старую задачу, если она еще идет
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            // 2. Уходим в фоновый поток
            Icon loadedIcon = await Task.Run( () => FetchIcon( targetPath ), token );

            // 3. Проверяем, не изменился ли путь, пока мы ждали
            if ( !token.IsCancellationRequested )
            {
                Icon = loadedIcon;
            }
            else
            {
                loadedIcon?.Dispose(); // Лишняя иконка нам не нужна
            }
        }
        catch ( OperationCanceledException ) { /* Игнорируем отмену */ }
        catch ( Exception ex )
        {
            Debug.WriteLine( $"Icon load error: {ex.Message}" );
            Icon = null;
        }
    }

    private Icon FetchIcon( string path )
    {
        if ( string.IsNullOrWhiteSpace( path ) ) return null;

        // Здесь выполняем «тяжелые» проверки (сетевые задержки будут тут)
        if ( File.Exists( path ) )
        {
            return Icon.ExtractAssociatedIcon( path );
        }

        if ( Directory.Exists( path ) )
        {
            var shinfo = new Win32Api.SHFILEINFO();
            IntPtr hIcon = Win32Api.SHGetFileInfo( path, 0, ref shinfo, ( uint )Marshal.SizeOf( shinfo ), Win32Api.SHGFI_ICON | Win32Api.SHGFI_LARGEICON );

            if ( shinfo.hIcon != IntPtr.Zero )
            {
                Icon result = ( Icon )Icon.FromHandle( shinfo.hIcon ).Clone();
                Win32Api.DestroyIcon( shinfo.hIcon );
                return result;
            }
        }

        // Если путь не найден или это URL — берем иконку браузера
        return DefaultSystemBrowser.GetBrowserIcon();
    }

    protected override void Dispose( bool disposing )
    {
        if ( disposing )
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _icon?.Dispose();
        }
        base.Dispose( disposing );
    }    
}
