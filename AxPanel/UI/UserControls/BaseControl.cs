using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace AxPanel.UI.UserControls;

public abstract class BaseControl : Control
{
    private static readonly Icon _defaultPlaceholder = CreatePlaceholderIcon( "" );

    private string? _path;
    private Icon? _icon;
    private CancellationTokenSource _cts; // Для отмены старой загрузки
    private readonly object _lock = new();

    public BaseControl()
    {
        DoubleBuffered = true;

        SetStyle( ControlStyles.AllPaintingInWmPaint | // Игнорировать WM_ERASEBKGND для уменьшения мерцания
                  ControlStyles.UserPaint | // Контрол рисует себя сам
                  ControlStyles.OptimizedDoubleBuffer | // Использовать буфер в памяти
                  ControlStyles.ResizeRedraw, true ); // Перерисовывать при изменении размера

        _icon = _defaultPlaceholder;
    }

    public string? BaseControlPath
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

    public Icon? Icon
    {
        get => _icon ?? _defaultPlaceholder; // Гарантируем не-null
        private set
        {
            if ( ReferenceEquals( _icon, value ) ) 
                return;

            if ( _icon != null && _icon != _defaultPlaceholder ) 
                _icon.Dispose();

            _icon = value;
            Invalidate();
        }
    }

    private async Task UpdateIconAsync( string targetPath )
    {
        // 1. Отменяем старую задачу, если она еще идет
        _cts?.Cancel();
        _cts?.Dispose();

        if ( string.IsNullOrWhiteSpace( targetPath ) )
        {
            Icon = null;
            return;
        }

        CancellationTokenSource cts = new();
        _cts = cts;

        try
        {
            // 2. Уходим в фоновый поток
            Icon? loadedIcon = await Task.Run( () => FetchIcon( targetPath ), cts.Token );

            // 3. Проверяем, не изменился ли путь, пока мы ждали
            if ( !cts.Token.IsCancellationRequested )
            {
                Icon = loadedIcon ?? _defaultPlaceholder;
            }
            else
            {
                if ( loadedIcon != _defaultPlaceholder )
                    loadedIcon?.Dispose();
            }
        }
        catch ( OperationCanceledException ) { /* Игнорируем отмену */ }
        catch ( Exception ex )
        {
            Debug.WriteLine( $"Icon load error: {ex.Message}" );
            Icon = _defaultPlaceholder;
        }
    }

    private Icon? FetchIcon( string path )
    {
        try
        {
            // Здесь выполняем «тяжелые» проверки (сетевые задержки будут тут)
            if( File.Exists( path ) )
            {
                return Icon.ExtractAssociatedIcon( path );
            }

            if( Directory.Exists( path ) )
            {
                Win32Api.SHFILEINFO shinfo = new();
                IntPtr hIcon = Win32Api.SHGetFileInfo( path, 0, ref shinfo, ( uint )Marshal.SizeOf( shinfo ), Win32Api.SHGFI_ICON | Win32Api.SHGFI_LARGEICON );

                if( shinfo.hIcon != IntPtr.Zero )
                {
                    Icon result = ( Icon )Icon.FromHandle( shinfo.hIcon ).Clone();
                    Win32Api.DestroyIcon( shinfo.hIcon );
                    return result;
                }
            }

            // Если путь не найден или это URL — берем иконку браузера
            return DefaultSystemBrowser.GetBrowserIcon();
        }
        catch
        {
            return null;
        }
    }

    private static Icon CreatePlaceholderIcon( string path )
    {
        // Размер стандартной большой иконки
        const int size = 32;
        using Bitmap bitmap = new Bitmap( size, size );
        using ( Graphics g = Graphics.FromImage( bitmap ) )
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using SolidBrush brush = new SolidBrush( Color.LightGray );
            g.FillEllipse( brush, 2, 2, size - 4, size - 4 );

            using Pen pen = new Pen( Color.DimGray, 1 );
            g.DrawEllipse( pen, 2, 2, size - 4, size - 4 );

            string letter = string.IsNullOrWhiteSpace( path ) ? "?" : Path.GetFileName( path ).Take( 1 ).ToString().ToUpper();
            using Font font = new Font( "Segoe UI", 12, FontStyle.Bold );
            SizeF textSize = g.MeasureString( letter, font );
            g.DrawString( letter, font, Brushes.White, ( size - textSize.Width ) / 2, ( size - textSize.Height ) / 2 );
        }

        // Превращаем Bitmap в иконку. 
        // Важно: Icon.FromHandle создает объект, владеющий дескриптором.
        return Icon.FromHandle( bitmap.GetHicon() );
    }

    protected override void Dispose( bool disposing )
    {
        if ( disposing )
        {
            _cts?.Cancel();
            _cts?.Dispose();

            if ( _icon != _defaultPlaceholder ) 
                _icon?.Dispose();
        }
        base.Dispose( disposing );
    }    
}
