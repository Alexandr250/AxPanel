using AxPanel.Model;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace AxPanel.SL;

public static class DownloadManager
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromMinutes( 5 ) };

    public static async Task<bool> DownloadAndPrepare( PortableItem item, Action<string>? onStatusChanged = null )
    {
        try
        {
            onStatusChanged?.Invoke( "Загрузка..." );

            string? targetFile = item.FilePath;

            // 1. Определяем базовую директорию программы (Apps\ИмяПрограммы)
            string fullPath = item.FilePath;
            string? targetDir = null;

            // Ищем, где в пути находится "Apps"
            string[] parts = fullPath.Split( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar );
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
            using ( HttpResponseMessage response = await _httpClient.GetAsync( item.DownloadUrl, HttpCompletionOption.ResponseHeadersRead ) )
            {
                response.EnsureSuccessStatusCode();
                await using FileStream fileStream = new( tempFile, FileMode.Create, FileAccess.Write, FileShare.None );
                await response.Content.CopyToAsync( fileStream );
            }

            // 3. Распаковка или перемещение
            if( item.IsArchive )
            {
                onStatusChanged?.Invoke( "Распаковка..." );

                ZipFile.ExtractToDirectory( tempFile, targetDir!, overwriteFiles: true );

                if( !File.Exists( item.FilePath ) )
                    NormalizeDirectoryStructure( targetDir! );

                if( !File.Exists( item.FilePath ) )
                    CheckExeAndEditJsonPath( item, onStatusChanged, targetDir );
            }
            else
            {
                if ( File.Exists( targetFile ) ) 
                    File.Delete( targetFile );

                File.Move( tempFile, targetFile );
            }

            // Чистим временный файл, если он остался (после Move его не будет)
            if ( File.Exists( tempFile ) ) 
                File.Delete( tempFile );

            onStatusChanged?.Invoke( item.Name ); // Возвращаем имя
            return true;
        }
        catch ( Exception ex )
        {
            Debug.WriteLine( $"[DownloadManager] Ошибка: {ex.Message}" );
            onStatusChanged?.Invoke( "Ошибка!" );
            return false;
        }
    }

    private static void CheckExeAndEditJsonPath( PortableItem item, Action<string>? onStatusChanged, string? targetDir )
    {
        if ( !File.Exists( item.FilePath ) )
        {
            onStatusChanged?.Invoke( "Поиск файла..." );
            string fileName = Path.GetFileName( item.FilePath );

            // Ищем файл рекурсивно в папке приложения
            string? foundFile = Directory.GetFiles( targetDir!, fileName, SearchOption.AllDirectories ).FirstOrDefault();

            if ( foundFile != null )
            {
                item.FilePath = foundFile; // Обновляем путь в объекте
                //TODO: -- ConfigManager.SaveItemsConfig( ConfigManager.GetModel() ); // Сохраняем новый путь в JSON
                ConfigManager.UpdatePortableJson( item.Name, foundFile );
            }
        }
    }

    //private static void UpdatePortableJson( string fileName, string itemName, string newPath )
    //{
    //    try
    //    {
    //        string fullPath = Path.Combine( AppDomain.CurrentDomain.BaseDirectory, fileName );
            
    //        if( !File.Exists( fullPath ) ) 
    //            return;

    //        string json = File.ReadAllText( fullPath );
    //        List<PortableItem>? items = JsonSerializer.Deserialize<List<PortableItem>>( json );

    //        PortableItem? target = items?.FirstOrDefault( i => i.Name == itemName );

    //        if( target != null )
    //        {
    //            // Сохраняем путь в ОТНОСИТЕЛЬНОМ виде (как он был в JSON изначально)
    //            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
    //            target.FilePath = Path.GetRelativePath( baseDir, newPath );

    //            string updatedJson = JsonSerializer.Serialize( items, new JsonSerializerOptions { WriteIndented = true } );
    //            File.WriteAllText( fullPath, updatedJson );
    //        }
    //    }
    //    catch( Exception ex )
    //    {
    //        Debug.WriteLine( $"Ошибка обновления JSON: {ex.Message}" );
    //    }
    //}

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
