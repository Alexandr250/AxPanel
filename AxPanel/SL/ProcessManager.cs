using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AxPanel.SL;

public static class ProcessManager
{
    // Для отмены выключения (Abort)
    [DllImport( "advapi32.dll", SetLastError = true )]
    private static extern bool AbortSystemShutdown( string? lpMachineName );

    /// <summary>
    /// Универсальный запуск процесса. 
    /// Поддерживает обычный запуск, от админа и передачу аргументов.
    /// </summary>
    public static bool Start( string filePath, bool asAdmin = false, object? args = null )
    {
        if ( string.IsNullOrWhiteSpace( filePath ) || !File.Exists( filePath ) )
            return false;

        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = filePath,
                Arguments = args?.ToString() ?? string.Empty,
                WorkingDirectory = Path.GetDirectoryName( filePath ),
                UseShellExecute = true // Важно для запуска от админа (verb)
            };

            if ( asAdmin )
            {
                psi.Verb = "runas";
            }

            Process.Start( psi );
            return true;
        }
        catch ( Exception ex )
        {
            Debug.WriteLine( $"[ProcessManager] Ошибка запуска {filePath}: {ex.Message}" );
            return false;
        }
    }

    /// <summary>
    /// Открывает папку с файлом в проводнике и выделяет его
    /// </summary>
    public static void OpenInExplorer( string filePath )
    {
        if ( string.IsNullOrWhiteSpace( filePath ) ) return;

        // Если файла нет, пробуем открыть хотя бы директорию
        string argument = File.Exists( filePath )
            ? $"/select,\"{filePath}\""
            : $"/n,\"{Path.GetDirectoryName( filePath )}\"";

        Process.Start( "explorer.exe", argument );
    }

    public static void OpenFolderInExplorer( string filePath )
    {
        if ( string.IsNullOrWhiteSpace( filePath ) || !Directory.Exists( filePath ) ) 
            return;

        Process.Start( "explorer.exe", filePath );
    }

    public static void Shutdown() =>
        RunCommand( "shutdown", "/s /t 30 /f /c \"Завершение через 30 сек. Нажми ПКМ для отмены\"" );

    public static void Restart() =>
        RunCommand( "shutdown", "/r /t 30 /f /c \"Перезагрузка через 30 сек. Нажми ПКМ для отмены\"" );

    public static void Sleep() =>
        Application.SetSuspendState( PowerState.Suspend, true, true );

    /// <summary>
    /// Отмена запланированного выключения/перезагрузки (вызывается ПКМ в вашем коде)
    /// </summary>
    public static void Abort()
    {
        RunCommand( "shutdown", "/a" ); // Системная команда отмены
        AbortSystemShutdown( null );
    }

    private static void RunCommand( string cmd, string args )
    {
        try
        {
            Process.Start( new ProcessStartInfo
            {
                FileName = cmd,
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false
            } );
        }
        catch ( Exception ex ) { Debug.WriteLine( ex.Message ); }
    }
}