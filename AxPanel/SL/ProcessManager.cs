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

    public static void Shutdown()
    {
        int timeToShutdown = 30;

        Thread.Sleep( 200 );
        if( MessageBox.Show( $"Выключить компьютер? Будет дано {timeToShutdown} секунд на сохранение файлов.", "Выключение компьютера", MessageBoxButtons.YesNo, MessageBoxIcon.Question ) == DialogResult.Yes )
        {
            Process.Start( new ProcessStartInfo( "shutdown", $"/s /t {timeToShutdown} /c \"Сохраните работу! Автоматическое выключение через {timeToShutdown} секунд.\"" )
            {
                CreateNoWindow = true,
                UseShellExecute = false
            } );
        }
    }

    public static void Restart()
    {
        int timeToShutdown = 30;

        Thread.Sleep( 200 );

        if ( MessageBox.Show( "Перезагрузить компьютер копьютер?", "Перезагрузка компьютера", MessageBoxButtons.YesNo, MessageBoxIcon.Question ) == DialogResult.Yes )
        {
            Process.Start( new ProcessStartInfo( "shutdown", $"/r /t {timeToShutdown} /c \"Перезагрузка через {timeToShutdown} секунд. Сохраните важные документы!\"" )
            {
                CreateNoWindow = true,
                UseShellExecute = false
            } );
        }
    }

    public static void Sleep()
    {
        //Application.SetSuspendState( PowerState.Suspend, true, true );
        Thread.Sleep( 200 );

        bool supportsHibernate = SystemInformation.PowerStatus.BatteryFullLifetime != -1;

        if( !supportsHibernate )
        {
            MessageBox.Show( "Спящий режим недоступен." );
        }
        else
        {
            if( MessageBox.Show( "Перевести компьютер в спящий режим? \r\n Любое приложение может прервать процесс (например, если открыт несохраненный документ).", "Спящий режим", MessageBoxButtons.YesNo, MessageBoxIcon.Question ) == DialogResult.Yes )
            {
                Application.SetSuspendState( PowerState.Suspend, false, false );
            }
        }
    }

    public static void Abort()
    {
        // Экстренная отмена, если нажали случайно
        Process.Start( new ProcessStartInfo( "shutdown", "/a" ) { CreateNoWindow = true } );
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
