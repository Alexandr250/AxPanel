using AxPanel.UI.UserControls;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace AxPanel.SL;

public static class IconProvider
{
    private enum IconSourceType { Action, Library, Msc, Directory, Image, IconFile, Associated, Browser, Unknown }

    private static readonly Icon _defaultPlaceholder = CreatePlaceholderIcon( "" );

    public static Icon DefaultPlaceholder => _defaultPlaceholder;

    public static Icon? GetIcon( string path )
    {
        if ( string.IsNullOrWhiteSpace( path ) ) return null;

        IconSourceType type = ResolveIconSource( path );

        try
        {
            return type switch
            {
                IconSourceType.Action => CreateActionIcon( path ),
                IconSourceType.Library => ExtractFromLibrary( path ),
                IconSourceType.Msc => GetUniqueMscIcon( GetCleanPath( path ) ) ?? ExtractFromShell( path, true ),
                IconSourceType.Directory => ExtractFromShell( path, false ),
                IconSourceType.Image => ExtractFromImage( path ),
                IconSourceType.IconFile => new Icon( GetCleanPath( path ), 48, 48 ),
                IconSourceType.Associated => Icon.ExtractAssociatedIcon( GetCleanPath( path ) )?.Clone() as Icon,
                IconSourceType.Browser => DefaultSystemBrowser.GetBrowserIcon(),
                _ => null
            };
        }
        catch { return null; }
    }

    private static IconSourceType ResolveIconSource( string path )
    {
        if ( string.IsNullOrWhiteSpace( path ) ) return IconSourceType.Unknown;

        if ( path.StartsWith( "action://", StringComparison.OrdinalIgnoreCase ) )
            return IconSourceType.Action;

        // Для классификации нам нужно понимать расширение "чистого" пути
        string cleanPath = GetCleanPath( path.Contains( "," ) ? path.Split( ',' )[ 0 ] : path );
        string ext = Path.GetExtension( cleanPath ).ToLower();

        if ( path.Contains( "," ) || ext == ".dll" || ext == ".icl" || ext == ".cpl" )
            return IconSourceType.Library;

        if ( ext == ".msc" ) return IconSourceType.Msc;
        if ( Directory.Exists( cleanPath ) ) return IconSourceType.Directory;
        if ( ext == ".ico" ) return IconSourceType.IconFile;

        string[] imgExts = { ".png", ".jpg", ".jpeg", ".bmp" };
        if ( imgExts.Contains( ext ) ) return IconSourceType.Image;

        if ( File.Exists( cleanPath ) ) return IconSourceType.Associated;

        return IconSourceType.Browser;
    }

    private static string GetCleanPath( string rawPath )
    {
        if ( string.IsNullOrWhiteSpace( rawPath ) )
            return string.Empty;

        string trimmed = rawPath.Trim();

        if ( trimmed.StartsWith( "\"" ) )
        {
            int endQuote = trimmed.IndexOf( "\"", 1 );
            if ( endQuote > 0 ) return trimmed.Substring( 1, endQuote - 1 );
        }

        string[] exts = [ ".exe", ".msc", ".cpl", ".bat", ".cmd" ];
        foreach ( string ext in exts )
        {
            int idx = trimmed.ToLower().IndexOf( ext );
            if ( idx > 0 )
            {
                return trimmed.Substring( 0, idx + ext.Length );
            }
        }

        return trimmed.Split( ' ' )[ 0 ];
    }

    private static Icon CreateActionIcon( string actionPath )
    {
        try
        {
            string shellPath = Path.Combine( Environment.SystemDirectory, "shell32.dll" );
            int iconIndex = -1;

            if ( actionPath.EndsWith( "media-toggle", StringComparison.OrdinalIgnoreCase ) )
            {
                iconIndex = 137;
            }

            if ( iconIndex != -1 && File.Exists( shellPath ) )
            {
                IntPtr[] hIconsLarge = new IntPtr[ 1 ];
                uint count = Win32Api.ExtractIconEx( shellPath, iconIndex, hIconsLarge, null, 1 );

                if ( count > 0 && hIconsLarge[ 0 ] != IntPtr.Zero )
                {
                    Icon result = ( Icon )Icon.FromHandle( hIconsLarge[ 0 ] ).Clone();
                    Win32Api.DestroyIcon( hIconsLarge[ 0 ] );
                    return result;
                }
            }
        }
        catch ( Exception ex )
        {
            System.Diagnostics.Debug.WriteLine( $"[BaseControl] Shell32 Icon Error: {ex.Message}" );
        }

        return _defaultPlaceholder;
    }

    private static Icon? ExtractFromLibrary( string path )
    {
        try
        {
            string targetFile = path;
            int iconIndex = 0;

            // 1. Разделяем путь и индекс (например, "shell32.dll, 15")
            if ( path.Contains( "," ) )
            {
                int lastComma = path.LastIndexOf( ',' );
                string potentialPath = path.Substring( 0, lastComma ).Trim( '"', ' ' );
                string potentialIndex = path.Substring( lastComma + 1 ).Trim();

                if ( int.TryParse( potentialIndex, out int index ) )
                {
                    targetFile = GetCleanPath( potentialPath );
                    iconIndex = index;
                }
            }
            else
            {
                targetFile = GetCleanPath( path );
            }

            if ( string.IsNullOrWhiteSpace( targetFile ) ) return null;

            // 2. Разворачиваем системные переменные (%SystemRoot% и т.д.)
            targetFile = Environment.ExpandEnvironmentVariables( targetFile );

            // 3. Если путь не полный, ищем в System32 (для imageres.dll, shell32.dll)
            if ( !Path.IsPathRooted( targetFile ) && !File.Exists( targetFile ) )
            {
                string sysPath = Path.Combine( Environment.SystemDirectory, targetFile );
                if ( File.Exists( sysPath ) ) targetFile = sysPath;
            }

            // 4. Финальное извлечение через Win32 API
            if ( File.Exists( targetFile ) )
            {
                IntPtr[] hIconsLarge = new IntPtr[ 1 ];
                uint count = Win32Api.ExtractIconEx( targetFile, iconIndex, hIconsLarge, null, 1 );

                if ( count > 0 && hIconsLarge[ 0 ] != IntPtr.Zero )
                {
                    Icon result = ( Icon )Icon.FromHandle( hIconsLarge[ 0 ] ).Clone();
                    Win32Api.DestroyIcon( hIconsLarge[ 0 ] );
                    return result;
                }
            }
        }
        catch ( Exception ex )
        {
            System.Diagnostics.Debug.WriteLine( $"[ExtractFromLibrary] Error: {ex.Message}" );
        }

        return null;
    }

    private static Icon? GetUniqueMscIcon( string mscPath )
    {
        try
        {
            if ( !File.Exists( mscPath ) ) return null;
            string content = File.ReadAllText( mscPath );

            Match match = Regex.Match( content,
                @"<Icon[^>]+Index=""(\d+)""[^>]+File=""([^""]+)""| <Icon[^>]+File=""([^""]+)""[^>]+Index=""(\d+)""",
                RegexOptions.IgnoreCase );

            if ( match.Success )
            {
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

    private static Icon? ExtractFromImage( string path )
    {
        try
        {
            string cleanPath = GetCleanPath( path );
            if ( !File.Exists( cleanPath ) ) return null;

            using Bitmap bmp = new( cleanPath );

            // Получаем нативный дескриптор иконки из битмапа
            IntPtr hIcon = bmp.GetHicon();

            // Клонируем, чтобы получить управляемый объект, не зависящий от hIcon
            Icon result = ( Icon )Icon.FromHandle( hIcon ).Clone();

            // Обязательно освобождаем неуправляемый ресурс сразу после клонирования
            Win32Api.DestroyIcon( hIcon );

            return result;
        }
        catch ( Exception ex )
        {
            System.Diagnostics.Debug.WriteLine( $"[ExtractFromImage] Error: {ex.Message}" );
            return null;
        }
    }

    private static Icon? ExtractFromShell( string path, bool isMsc )
    {
        try
        {
            // Очищаем путь от кавычек и лишних пробелов перед системным вызовом
            string cleanPath = GetCleanPath( path );
            if ( string.IsNullOrWhiteSpace( cleanPath ) ) return null;

            Win32Api.SHFILEINFO shinfo = new();
            uint flags = Win32Api.SHGFI_ICON | Win32Api.SHGFI_LARGEICON;

            // Если это оснастка MSC, просим Shell использовать атрибуты файла
            if ( isMsc )
                flags |= Win32Api.SHGFI_USEFILEATTRIBUTES;

            // Вызов Shell API
            IntPtr hImg = Win32Api.SHGetFileInfo( cleanPath, 0, ref shinfo, ( uint )Marshal.SizeOf( shinfo ), flags );

            if ( shinfo.hIcon != IntPtr.Zero )
            {
                // Создаем копию иконки, чтобы безопасно владеть ею в управляемом коде
                Icon result = ( Icon )Icon.FromHandle( shinfo.hIcon ).Clone();

                // ОБЯЗАТЕЛЬНО освобождаем нативный дескриптор, созданный Shell
                Win32Api.DestroyIcon( shinfo.hIcon );

                return result;
            }
        }
        catch ( Exception ex )
        {
            System.Diagnostics.Debug.WriteLine( $"[ExtractFromShell] Error for {path}: {ex.Message}" );
        }

        return null;
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

        IntPtr hIcon = bitmap.GetHicon();
        Icon icon = ( Icon )Icon.FromHandle( hIcon ).Clone();
        Win32Api.DestroyIcon( hIcon );
        return icon;
    }
}
