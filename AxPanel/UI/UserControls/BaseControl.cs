using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AxPanel.UI.UserControls;

public abstract class BaseControl : Control
{
    private string _path;
    private Icon _icon;
    private CancellationTokenSource _cts; // Для отмены старой загрузки

    public string Path
    {
        get => _path;
        set
        {
            if ( _path == value ) return;
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
                this.Icon = loadedIcon;
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
            this.Icon = null;
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
            var shinfo = new WinApi.SHFILEINFO();
            IntPtr hIcon = WinApi.SHGetFileInfo( path, 0, ref shinfo, ( uint )Marshal.SizeOf( shinfo ), WinApi.SHGFI_ICON | WinApi.SHGFI_LARGEICON );

            if ( shinfo.hIcon != IntPtr.Zero )
            {
                Icon result = ( Icon )System.Drawing.Icon.FromHandle( shinfo.hIcon ).Clone();
                WinApi.DestroyIcon( shinfo.hIcon );
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

    // WinApi хелпер (DestroyIcon важен!)
    private static class WinApi
    {
        // Добавьте эти константы:
        public const uint SHGFI_ICON = 0x100;
        public const uint SHGFI_LARGEICON = 0x0;

        [StructLayout( LayoutKind.Sequential, CharSet = CharSet.Auto )]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs( UnmanagedType.ByValTStr, SizeConst = 260 )]
            public string szDisplayName;
            [MarshalAs( UnmanagedType.ByValTStr, SizeConst = 80 )]
            public string szTypeName;
        }

        [DllImport( "shell32.dll", CharSet = CharSet.Auto )]
        public static extern IntPtr SHGetFileInfo( string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags );

        [DllImport( "user32.dll" )]
        public static extern bool DestroyIcon( IntPtr hIcon );
    }

    public static class DefaultSystemBrowser
    {
        private static Icon _cachedIcon;
        private static string _cachedPath;

        public static Icon GetBrowserIcon()
        {
            // Кэшируем иконку, чтобы не дергать диск и реестр постоянно
            if ( _cachedIcon != null ) return _cachedIcon;

            Determine();

            if ( !string.IsNullOrEmpty( _cachedPath ) && System.IO.File.Exists( _cachedPath ) )
            {
                try { _cachedIcon = Icon.ExtractAssociatedIcon( _cachedPath ); }
                catch { /* игнорируем ошибки доступа */ }
            }
            return _cachedIcon;
        }

        private static void Determine()
        {
            if ( !string.IsNullOrEmpty( _cachedPath ) ) return;

            try
            {
                using ( var key = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey( @"HTTP\shell\open\command" ) )
                {
                    if ( key?.GetValue( null ) is string rawPath )
                    {
                        // Очищаем путь от кавычек и аргументов
                        string path = rawPath.Trim().Replace( "\"", "" );
                        int exeIdx = path.IndexOf( ".exe", StringComparison.OrdinalIgnoreCase );
                        if ( exeIdx != -1 )
                        {
                            _cachedPath = path.Substring( 0, exeIdx + 4 );
                        }
                    }
                }
            }
            catch { _cachedPath = string.Empty; }
        }
    }
}