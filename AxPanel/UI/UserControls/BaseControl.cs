using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace AxPanel.UI.UserControls;

public abstract class BaseControl : Control
{
    private static readonly Icon _defaultPlaceholder = CreatePlaceholderIcon( "" );

    private string? _path;
    private Icon? _icon;
    private CancellationTokenSource _cts; // Для отмены старой загрузки
    private readonly object _lock = new();

    private string _latestTarget = ""; // Флаг актуальности задачи

    public BaseControl() 
    {
        DoubleBuffered = true;

        SetStyle( ControlStyles.AllPaintingInWmPaint | // Игнорировать WM_ERASEBKGND для уменьшения мерцания
                  ControlStyles.UserPaint | // Контрол рисует себя сам
                  ControlStyles.OptimizedDoubleBuffer | // Использовать буфер в памяти
                  ControlStyles.ResizeRedraw, true ); // Перерисовывать при изменении размера

        _icon = _defaultPlaceholder;
    }

    public string? DownloadUrl { get; set; }

    public bool IsArchive { get; set; }

    public string? Arguments { get; set; }

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
        lock ( _lock )
        {
            _latestTarget = targetPath ?? "";
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }

        CancellationToken localCts = _cts.Token;

        try
        {
            await Task.Delay( 10, localCts );

            Icon? loadedIcon = await Task.Run( () => {
                // Пробуем 2 раза, если Shell вернул null
                Icon? res = FetchIcon( targetPath );
                if ( res == null && !localCts.IsCancellationRequested )
                {
                    Thread.Sleep( 50 );
                    res = FetchIcon( targetPath );
                }
                return res;
            }, localCts );

            if ( !localCts.IsCancellationRequested && _latestTarget == targetPath )
            {
                // Используем BeginInvoke, чтобы GDI+ не ругался на разные потоки
                BeginInvoke( () => {
                    if ( _latestTarget == targetPath ) 
                        Icon = loadedIcon;
                    else 
                        loadedIcon?.Dispose();
                } );
            }
            else loadedIcon?.Dispose();
        }
        catch { /* отмена или ошибка */ }
    }

    private Icon? FetchIcon( string path )
    {
        try
        {
            string cleanPath = GetCleanPath( path );
            if ( string.IsNullOrWhiteSpace( cleanPath ) ) return null;

            string ext = Path.GetExtension( cleanPath ).ToLower();

            // 1. ПРИОРИТЕТ: Уникальные иконки .msc через парсинг XML
            if ( ext == ".msc" )
            {
                Icon? uniqueMsc = GetUniqueMscIcon( cleanPath );
                if ( uniqueMsc != null ) return uniqueMsc;
            }

            // 2. СИСТЕМНЫЕ ОБЪЕКТЫ (.cpl, .msc fallback, папки)
            if ( Directory.Exists( cleanPath ) || ext == ".cpl" || ext == ".msc" || ext == ".dll" )
            {
                Win32Api.SHFILEINFO shinfo = new();

                // Базовые флаги: Иконка + Большая
                uint flags = Win32Api.SHGFI_ICON | Win32Api.SHGFI_LARGEICON;
                uint dwAttr = 0;

                // Только для .msc добавляем флаг "виртуального" поиска по атрибутам
                if ( ext == ".msc" )
                {
                    flags |= Win32Api.SHGFI_USEFILEATTRIBUTES; // 0x000000010; // SHGFI_USEFILEATTRIBUTES
                    dwAttr = 0x80;        // FILE_ATTRIBUTE_NORMAL
                }

                IntPtr hIcon = Win32Api.SHGetFileInfo( cleanPath, dwAttr, ref shinfo, ( uint )Marshal.SizeOf( shinfo ), flags );

                if ( shinfo.hIcon != IntPtr.Zero )
                {
                    // Копируем дескриптор, чтобы он стал полностью нашим
                    IntPtr hIconCopy = Win32Api.CopyIcon( shinfo.hIcon );
                    Icon result = Icon.FromHandle( hIconCopy );

                    // Освобождаем СИСТЕМНЫЙ дескриптор, наш hIconCopy будет жить в объекте Icon
                    Win32Api.DestroyIcon( shinfo.hIcon );
                    return result;
                }
            }

            // 3. ОБЫЧНЫЕ EXE И LNK
            if ( File.Exists( cleanPath ) )
            {
                using Icon? tempIcon = Icon.ExtractAssociatedIcon( cleanPath );
                return ( Icon )tempIcon?.Clone(); // Клонируем, чтобы отвязаться от ресурсов Shell
            }

            return DefaultSystemBrowser.GetBrowserIcon();
        }
        catch { return _defaultPlaceholder; }
    }

    private Icon? GetUniqueMscIcon( string mscPath )
    {
        try
        {
            if ( !File.Exists( mscPath ) ) return null;
            string content = File.ReadAllText( mscPath );

            // Улучшенная регулярка для поиска блока иконки в любом порядке атрибутов
            Match match = Regex.Match( content,
                @"<Icon[^>]+Index=""(\d+)""[^>]+File=""([^""]+)""| <Icon[^>]+File=""([^""]+)""[^>]+Index=""(\d+)""",
                RegexOptions.IgnoreCase );

            if ( match.Success )
            {
                // Определяем, в какой группе оказался путь и индекс
                string rawIndex = !string.IsNullOrEmpty( match.Groups[ 1 ].Value ) ? match.Groups[ 1 ].Value : match.Groups[ 4 ].Value;
                string rawPath = !string.IsNullOrEmpty( match.Groups[ 2 ].Value ) ? match.Groups[ 2 ].Value : match.Groups[ 3 ].Value;

                int iconIndex = int.Parse( rawIndex );
                string resFile = Environment.ExpandEnvironmentVariables( rawPath );

                if ( File.Exists( resFile ) )
                {
                    IntPtr[] hIcons = new IntPtr[ 1 ];
                    uint count = Win32Api.ExtractIconEx( resFile, iconIndex, hIcons, null, 1 );

                    if ( count > 0 && hIcons[ 0 ] != IntPtr.Zero )
                    {
                        Icon result = ( Icon )Icon.FromHandle( hIcons[ 0 ] ).Clone();
                        Win32Api.DestroyIcon( hIcons[ 0 ] );
                        return result;
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private string GetCleanPath( string rawPath )
    {
        if ( string.IsNullOrWhiteSpace( rawPath ) ) 
            return string.Empty;

        string trimmed = rawPath.Trim();

        // Случай А: Путь в кавычках ("C:\Program Files\app.exe" --arg)
        if ( trimmed.StartsWith( "\"" ) )
        {
            int endQuote = trimmed.IndexOf( "\"", 1 );
            if ( endQuote > 0 ) return trimmed.Substring( 1, endQuote - 1 );
        }

        // Случай Б: Путь без кавычек, но с пробелом и аргументами (control.exe ncpa.cpl)
        // Ищем расширения .exe, .msc, .cpl, .bat, .cmd
        string[] exts = [ ".exe", ".msc", ".cpl", ".bat", ".cmd" ];
        foreach ( string ext in exts )
        {
            int idx = trimmed.ToLower().IndexOf( ext );
            if ( idx > 0 )
            {
                // Возвращаем путь включая само расширение
                return trimmed.Substring( 0, idx + ext.Length );
            }
        }

        // Случай В: Обычный путь без аргументов или неизведанный формат
        return trimmed.Split( ' ' )[ 0 ];
    }

    private static Icon CreatePlaceholderIcon( string path )
    {
        const int size = 32;
        using Bitmap bitmap = new( size, size );
        using ( Graphics g = Graphics.FromImage( bitmap ) )
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using SolidBrush brush = new( Color.LightGray );
            g.FillEllipse( brush, 2, 2, size - 4, size - 4 );

            string letter = string.IsNullOrWhiteSpace( path ) ? "?" : path.TrimStart( '\\' ).Substring( 0, 1 ).ToUpper();
            using Font font = new( "Segoe UI", 12, FontStyle.Bold );
            SizeF textSize = g.MeasureString( letter, font );
            g.DrawString( letter, font, Brushes.White, ( size - textSize.Width ) / 2, ( size - textSize.Height ) / 2 );
        }

        // ВАЖНО: hIcon нужно удалять вручную!
        IntPtr hIcon = bitmap.GetHicon();
        Icon icon = ( Icon )Icon.FromHandle( hIcon ).Clone();
        Win32Api.DestroyIcon( hIcon ); // Обязательно освобождаем системный ресурс
        return icon;
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
