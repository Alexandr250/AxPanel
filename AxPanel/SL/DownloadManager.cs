using AxPanel.Model;
using System.Diagnostics;
using System.IO.Compression;

namespace AxPanel.SL;

public static class DownloadManager
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromMinutes( 5 ) };

    public static async Task<bool> DownloadAndPrepare( PortableItem item, Action<string>? onStatusChanged = null )
    {
        try
        {
            onStatusChanged?.Invoke( "Загрузка..." );

            // 1. Создаем дерево директорий (например, Apps\Notepad++)
            string? targetFile = item.FilePath;
            string? targetDir = Path.GetDirectoryName( targetFile );
            if ( !string.IsNullOrEmpty( targetDir ) ) Directory.CreateDirectory( targetDir );

            // 2. Скачиваем во временный файл, чтобы не "мусорить" при обрыве связи
            string tempFile = Path.GetTempFileName();
            using ( var response = await _httpClient.GetAsync( item.DownloadUrl, HttpCompletionOption.ResponseHeadersRead ) )
            {
                response.EnsureSuccessStatusCode();
                using var fs = new FileStream( tempFile, FileMode.Create, FileAccess.Write, FileShare.None );
                await response.Content.CopyToAsync( fs );
            }

            // 3. Распаковка или перемещение
            if ( item.IsArchive )
            {
                onStatusChanged?.Invoke( "Распаковка..." );
                // Извлекаем ZIP прямо в папку назначения
                ZipFile.ExtractToDirectory( tempFile, targetDir!, overwriteFiles: true );
            }
            else
            {
                // Если это одиночный EXE (как 7-zip в твоем списке)
                if ( File.Exists( targetFile ) ) File.Delete( targetFile );
                File.Move( tempFile, targetFile );
            }

            // Чистим временный файл, если он остался (после Move его не будет)
            if ( File.Exists( tempFile ) ) File.Delete( tempFile );

            onStatusChanged?.Invoke( item.Name ); // Возвращаем имя
            return true;
        }
        catch ( Exception ex )
        {
            System.Diagnostics.Debug.WriteLine( $"[DownloadManager] Ошибка: {ex.Message}" );
            onStatusChanged?.Invoke( "Ошибка!" );
            return false;
        }
    }
}
