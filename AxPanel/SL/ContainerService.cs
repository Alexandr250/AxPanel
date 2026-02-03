using AxPanel.UI.UserControls;

namespace AxPanel.SL;

public class ContainerService
{
    /// <summary>
    /// Запуск одиночного процесса с обновлением состояния кнопки
    /// </summary>
    public void RunProcess( LaunchButtonView btn )
    {
        RunProcess( btn, false );
    }

    /// <summary>
    /// Запуск одиночного процесса с обновлением состояния кнопки
    /// </summary>
    public void RunProcess( LaunchButtonView btn, bool rusAsAdmin )
    {
        if ( string.IsNullOrWhiteSpace( btn.BaseControlPath ) )
            return;

        if ( ProcessManager.Start( btn.BaseControlPath, rusAsAdmin ) )
        {
            // Мгновенная визуальная отдача
            btn.IsRunning = true;
            btn.Invalidate();
        }
    }

    /// <summary>
    /// Запускает переданный список кнопок
    /// </summary>
    public void RunProcessGroup( IEnumerable<LaunchButtonView> groupButtons )
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
