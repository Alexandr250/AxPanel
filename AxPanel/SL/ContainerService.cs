using AxPanel.UI.UserControls;

namespace AxPanel.SL;

public class ContainerService
{
    /// <summary>
    /// Запуск одиночного процесса с обновлением состояния кнопки
    /// </summary>
    public void RunProcess( LaunchButton btn )
    {
        if ( string.IsNullOrWhiteSpace( btn.BaseControlPath ) ) return;

        if ( ProcessManager.Start( btn.BaseControlPath ) )
        {
            // Мгновенная визуальная отдача
            btn.IsRunning = true;
            btn.Invalidate();
        }
    }

    /// <summary>
    /// Запускает переданный список кнопок
    /// </summary>
    public void RunProcessGroup( IEnumerable<LaunchButton> groupButtons )
    {
        foreach ( var btn in groupButtons )
        {
            RunProcess( btn );
        }
    }

    /// <summary>
    /// Открытие расположения файла
    /// </summary>
    public void OpenLocation( string path )
    {
        ProcessManager.OpenInExplorer( path );
    }
}
