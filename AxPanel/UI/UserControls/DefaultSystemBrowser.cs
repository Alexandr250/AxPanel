namespace AxPanel.UI.UserControls;

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