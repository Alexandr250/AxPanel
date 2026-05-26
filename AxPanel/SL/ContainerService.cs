using AxPanel.Model;
using AxPanel.UI.UserControls;

namespace AxPanel.SL;

public class ContainerService
{
    /// <summary>
    /// Основной метод запуска. Поддерживает асинхронную загрузку портативок.
    /// </summary>
    public async void RunProcess( LaunchButtonView btn, bool runAsAdmin, object? args = null )
    {
        if ( string.IsNullOrWhiteSpace( btn.BaseControlPath ) )
            return;

        if ( btn.BaseControlPath.StartsWith( "action://", StringComparison.OrdinalIgnoreCase ) )
        {
            HandleInternalAction( btn.BaseControlPath );
            return;
        }

        if ( !File.Exists( btn.BaseControlPath ) && !string.IsNullOrEmpty( btn.DownloadUrl ) )
        {
            string originalText = btn.Text;

            PortableItem portable = new() { DownloadUrl = btn.DownloadUrl, FilePath = btn.BaseControlPath, Name = btn.Text, IsArchive = btn.IsArchive };

            bool success = await DownloadManager.DownloadAndPrepare( portable, status =>
            {
                btn.BeginInvoke( () => {
                    btn.Text = status;
                    btn.Invalidate();
                } );
            } );

            if ( !success )
            {
                MessageBox.Show( $"Ошибка при подготовке {portable.Name}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error );
                btn.Text = originalText;
                return;
            }

            btn.Text = originalText;
        }

        if ( File.Exists( btn.BaseControlPath ) )
        {
            if ( ProcessManager.Start( btn.BaseControlPath, runAsAdmin, args ) )
            {
                ProcessStats currentStats = btn.Stats;
                currentStats.IsRunning = true;
                btn.Stats = currentStats;
                btn.Invalidate();
            }
        }
        else if( Directory.Exists( btn.BaseControlPath ) )
        {
            ProcessManager.OpenFolderInExplorer( btn.BaseControlPath );
        }
        else
        {
            MessageBox.Show( $"Файл не найден: {btn.BaseControlPath}", "Ошибка запуска", MessageBoxButtons.OK, MessageBoxIcon.Warning );
        }
    }

    private void HandleInternalAction( string actionPath )
    {
        if ( actionPath.Equals( "action://media-toggle", StringComparison.OrdinalIgnoreCase ) )
        {
            MediaInteractionService.TogglePlayPauseAsync();
        }
    }

    /// <summary>
    /// Групповой запуск (например, всех утилит под разделителем)
    /// </summary>
    public void RunProcessGroup( IEnumerable<LaunchButtonView> groupButtons )
    {
        Task.Run( () =>
        {
            foreach ( LaunchButtonView btn in groupButtons )
            {
                RunProcess( btn, false );
            }
        } );
    }

    /// <summary>
    /// Простая обертка для запуска без параметров
    /// </summary>
    public void RunProcess( LaunchButtonView btn ) => 
        RunProcess( btn, false, null );

    /// <summary>
    /// Открытие расположения файла в проводнике
    /// </summary>
    public void OpenLocation( string path )
    {
        ProcessManager.OpenInExplorer( path );
    }
}
