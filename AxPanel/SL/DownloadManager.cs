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

            //// 1. Создаем дерево директорий (например, Apps\Notepad++)
            //string? targetFile = item.FilePath;
            //string? targetDir = Path.GetDirectoryName( targetFile );
            //if ( !string.IsNullOrEmpty( targetDir ) ) Directory.CreateDirectory( targetDir );

            string? targetFile = item.FilePath;

            // 1. Определяем базовую директорию программы (Apps\ИмяПрограммы)
            string fullPath = item.FilePath;
            string? targetDir = null;

            // Ищем, где в пути находится "Apps"
            var parts = fullPath.Split( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar );
            int appsIndex = Array.FindIndex( parts, p => p.Equals( "Apps", StringComparison.OrdinalIgnoreCase ) );

            if ( appsIndex != -1 && appsIndex + 1 < parts.Length )
            {
                // Собираем путь до: ...\Apps\ИмяПрограммы\
                targetDir = string.Join( Path.DirectorySeparatorChar.ToString(), parts.Take( appsIndex + 2 ) );
            }
            else
            {
                // Если "Apps" не нашли, берем папку, где лежит сам EXE
                targetDir = Path.GetDirectoryName( fullPath );
            }

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

                // Поднимаем файлы, если всё упаковано в одну подпапку
                NormalizeDirectoryStructure( targetDir! );
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

    private static void NormalizeDirectoryStructure( string targetDir )
    {
        // Получаем список всех объектов в папке
        string[] subDirs = Directory.GetDirectories( targetDir );
        string[] files = Directory.GetFiles( targetDir );

        // Если в папке только ОДНА поддиректория и НЕТ файлов
        if ( subDirs.Length == 1 && files.Length == 0 )
        {
            string rootSubDir = subDirs[ 0 ];

            // Перемещаем все файлы и папки из вложенной папки на уровень выше
            foreach ( string dir in Directory.GetDirectories( rootSubDir ) )
            {
                string dest = Path.Combine( targetDir, Path.GetFileName( dir ) );
                if ( Directory.Exists( dest ) ) Directory.Delete( dest, true );
                Directory.Move( dir, dest );
            }

            foreach ( string file in Directory.GetFiles( rootSubDir ) )
            {
                string dest = Path.Combine( targetDir, Path.GetFileName( file ) );
                if ( File.Exists( dest ) ) File.Delete( dest );
                File.Move( file, dest );
            }

            // Удаляем теперь уже пустую подпапку
            Directory.Delete( rootSubDir, true );
        }
    }
}
