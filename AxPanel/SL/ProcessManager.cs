using System.Diagnostics;

namespace AxPanel.SL;

public static class ProcessManager
{
    /// <summary>
    /// Запускает процесс и возвращает результат запуска
    /// </summary>
    public static bool Start( string path, bool runAsAdmin = false )
    {
        if ( string.IsNullOrWhiteSpace( path ) ) return false;

        try
        {
            var info = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = runAsAdmin ? "runas" : ""
            };

            Process.Start( info );
            return true;
        }
        catch ( Exception ex )
        {
            Debug.WriteLine( $"[ProcessRunner] Ошибка запуска {path}: {ex.Message}" );
            return false;
        }
    }

    /// <summary>
    /// Открывает проводник с выделением файла или переходом в папку
    /// </summary>
    public static void OpenInExplorer( string path )
    {
        if ( string.IsNullOrWhiteSpace( path ) ) return;

        try
        {
            if ( File.Exists( path ) )
            {
                Process.Start( "explorer.exe", $"/select,\"{path}\"" );
            }
            else
            {
                // Если файл не найден, ищем ближайшую существующую директорию
                string dir = Directory.Exists( path ) ? path : GetExistingParent( path );
                if ( dir != null ) Process.Start( "explorer.exe", $"\"{dir}\"" );
            }
        }
        catch ( Exception ex )
        {
            Debug.WriteLine( $"[ProcessRunner] Ошибка проводника: {ex.Message}" );
        }
    }

    private static string GetExistingParent( string path )
    {
        string dir = Path.GetDirectoryName( path );
        while ( !string.IsNullOrEmpty( dir ) && !Directory.Exists( dir ) )
        {
            dir = Path.GetDirectoryName( dir );
        }
        return dir;
    }
}
