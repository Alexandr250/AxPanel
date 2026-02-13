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

        if ( !File.Exists( btn.BaseControlPath ) && !string.IsNullOrEmpty( btn.DownloadUrl ) )
        {
            // Визуальный фидбек: можно временно изменить текст или включить флаг загрузки
            string originalText = btn.Text;

            PortableItem portable = new() { DownloadUrl = btn.DownloadUrl, FilePath = btn.BaseControlPath, Name = btn.Text, IsArchive = btn.IsArchive };

            bool success = await DownloadManager.DownloadAndPrepare( portable, status =>
            {
                // Обновляем статус прямо на кнопке через Invoke (т.к. асинхронно)
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

            btn.Text = originalText; // Возвращаем название после загрузки
        }

        // 2. Если файл теперь существует (или был сразу) — запускаем
        if ( File.Exists( btn.BaseControlPath ) )
        {
            if ( ProcessManager.Start( btn.BaseControlPath, runAsAdmin, args ) )
            {
                // Обновляем статистику "запущенности" (монитор подхватит остальное)
                ProcessStats currentStats = btn.Stats;
                currentStats.IsRunning = true;
                btn.Stats = currentStats;
                btn.Invalidate();
            }
        }
        else
        {
            MessageBox.Show( $"Файл не найден: {btn.BaseControlPath}", "Ошибка запуска", MessageBoxButtons.OK, MessageBoxIcon.Warning );
        }
    }

    /// <summary>
    /// Групповой запуск (например, всех утилит под разделителем)
    /// </summary>
    public void RunProcessGroup( IEnumerable<LaunchButtonView> groupButtons )
    {
        // Групповой запуск делаем через Task.Run, чтобы не фризить UI, если файлов много
        Task.Run( () =>
        {
            foreach ( LaunchButtonView btn in groupButtons )
            {
                // Вызываем обычный запуск для каждой кнопки
                // (BeginInvoke внутри RunProcess позаботится о потокобезопасности UI)
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
