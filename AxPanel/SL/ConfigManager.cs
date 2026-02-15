using AxPanel.Model;
using AxPanel.UI.Themes;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace AxPanel.SL;

public class ConfigManager
{
    private static readonly object _lock = new();

    private static string _itemsConfigFile = "items-config.json";
    private const string _mainConfigFile = "config.json";
    private const string _portableAppsFile = "portable-apps.json";
    private const string _systemAppsFile = "system-apps.json";

    private static readonly JsonSerializerOptions _jsonOptions = new() { 
        WriteIndented = true, 
        Encoder = JavaScriptEncoder.Create( UnicodeRanges.All ) 
    };

    private static MainConfig? _cachedMainConfig;
    private static MainModel? _cachedModel;

    private static string GetFullPath( string fileName ) =>
        Path.Combine( AppDomain.CurrentDomain.BaseDirectory, fileName );

    public static MainConfig GetMainConfig()
    {
        if ( _cachedMainConfig == null )
        {
            lock ( _lock )
            {
                _cachedMainConfig ??= ReadMainConfigFromDisk();
            }
        }
        return _cachedMainConfig;
    }

    private static MainConfig ReadMainConfigFromDisk()
    {
        string path = GetFullPath( _mainConfigFile );
        if ( File.Exists( path ) )
        {
            try
            {
                string json = File.ReadAllText( path );
                return JsonSerializer.Deserialize<MainConfig>( json ) ?? CreateDefaultConfig();
            }
            catch { return CreateDefaultConfig(); }
        }

        MainConfig defaultConfig = CreateDefaultConfig();
        SaveMainConfig( defaultConfig );
        return defaultConfig;
    }

    public static void SaveMainConfig( MainConfig mainConfig )
    {
        lock ( _lock )
        {
            _cachedMainConfig = mainConfig;
            try
            {
                string json = JsonSerializer.Serialize( mainConfig, _jsonOptions );
                File.WriteAllText( GetFullPath( _mainConfigFile ), json );
            }
            catch( Exception ex )
            {
                Debug.WriteLine( ex.Message );
            }
        }
    }
    
    private static MainConfig CreateDefaultConfig()
    {
        MainConfig defaultConfig = new()
        {
            Width = 400,
            Height = 600,
            HeaderHeight = 30,
            BorderWidth = 5,
            ItemsConfig = "items-config.json",
            ThemeFileName = "default-theme.json"
        };
        return defaultConfig;
    }

    /// <summary>
    /// Потокобезопасное получение модели. 
    /// Читает с диска только при первом вызове или после сброса кэша.
    /// </summary>
    public static MainModel GetModel()
    {
        // Первая проверка без блокировки для производительности
        if ( _cachedModel == null )
        {
            lock ( _lock )
            {
                _cachedModel ??= ReadModelFromDisk();
            }
        }
        return _cachedModel;
    }

    private static MainModel ReadModelFromDisk()
    {
        MainModel? panelModel = null;
        string fileName = GetMainConfig().ItemsConfig ?? "items-config.json";
        string path = GetFullPath( fileName );

        // 1. Попытка чтения из файла
        if ( File.Exists( path ) )
        {
            try
            {
                string json = File.ReadAllText( path );
                panelModel = JsonSerializer.Deserialize<MainModel>( json );
            }
            catch ( Exception ex )
            {
                Debug.WriteLine( $"[ConfigManager] Ошибка загрузки: {ex.Message}" );
            }
        }

        // 2. Если файла нет или он пустой — создаем структуру с нуля
        if ( panelModel?.Containers == null || panelModel.Containers.Count == 0 )
        {
            panelModel = new MainModel { Containers = [] };

            ContainerItem emptyContainer = new()
            {
                Name = "Новая панель",
                Items = [],
                Type = ContainerType.Normal
            };

            panelModel.Containers.Add( emptyContainer );

            // Сразу сохраняем на диск (метод SaveItemsConfig тоже должен использовать lock)
            SaveItemsConfig( panelModel );
        }

        // 3. Индексация существующих элементов (ID для UI)
        foreach ( ContainerItem container in panelModel.Containers )
        {
            if ( container.Items == null ) 
                continue;

            for ( int i = 0; i < container.Items.Count; i++ )
            {
                if ( container.Items[ i ] != null )
                    container.Items[ i ].Id = i;
            }
        }

        // 4. Принудительное добавление системного контейнера
        if ( !panelModel.Containers.Any( c => c.Type == ContainerType.System ) ) 
            panelModel.Containers.Add( BuildStandartContainer() );

        panelModel.Containers.Add( BuildPortableContainer() );

        return panelModel;
    }

    private static ContainerItem? BuildStandartContainer()
    {
        // Регистрируем переменную для корректного ExpandEnvironmentVariables
        Environment.SetEnvironmentVariable( "SystemDirectory", Environment.SystemDirectory );

        try
        {
            string jsonPath = Path.Combine( AppDomain.CurrentDomain.BaseDirectory, _systemAppsFile );
            
            if ( !File.Exists( jsonPath ) ) 
                return null;

            string jsonContent = File.ReadAllText( jsonPath );

            ContainerItem? container = JsonSerializer.Deserialize<ContainerItem>( jsonContent );

            if ( container == null ) 
                return null;

            // Фильтруем элементы: раскрываем пути и проверяем наличие файлов
            container.Items = container.Items
                .Where( item =>
                {
                    if ( item.IsSeparator ) 
                        return true; // Разделители оставляем

                    // Раскрываем %windir%, %SystemDirectory% и т.д.
                    string expandedPath = Environment.ExpandEnvironmentVariables( item.FilePath );

                    if ( File.Exists( expandedPath ) )
                    {
                        item.FilePath = expandedPath;
                        return true;
                    }
                    return false;
                } )
                .ToList();

            // Пересчитываем ID, чтобы после фильтрации (например, без gpedit) они шли по порядку
            for ( int i = 0; i < container.Items.Count; i++ )
                container.Items[ i ].Id = i;

            return container;
        }
        catch ( Exception ex )
        {
            Debug.WriteLine( $"Ошибка загрузки SystemApps: {ex.Message}" );
            return null;
        }
    }

    private static ContainerItem BuildPortableContainer()
    {
        List<PortableItem> portableItems = LoadPortableItems( _portableAppsFile );

        ContainerItem container = new()
        {
            Name = "Портативный софт",
            Type = ContainerType.System,
            Items = []
        };

        if ( portableItems is not { Count: not 0 } )
            return container;

        foreach ( PortableItem item in portableItems ) 
            container.Items.Add( item );

        return container;
    }

    public static List<PortableItem> LoadPortableItems( string fileName )
    {
        try
        {
            string path = GetFullPath( fileName );
            
            if ( !File.Exists( path ) ) 
                return [];

            string json = File.ReadAllText( path );

            // Прямая десериализация в список портативок
            List<PortableItem>? items = JsonSerializer.Deserialize<List<PortableItem>>( json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            } );

            if ( items != null )
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                foreach ( PortableItem item in items )
                {
                    // Приводим относительный путь Apps\... к абсолютному
                    if ( !string.IsNullOrEmpty( item.FilePath ) )
                        item.FilePath = Path.GetFullPath( Path.Combine( baseDir, item.FilePath ) );
                }
            }

            return items ?? [];
        }
        catch ( Exception ex )
        {
            Debug.WriteLine( $"Ошибка загрузки PortableItems: {ex.Message}" );
            return [];
        }
    }

    public static void SaveItemsConfig( MainModel panelModel )
    {
        lock ( _lock )
        {
            _cachedModel = panelModel;

            MainModel clone = new()
            {
                Containers = panelModel.Containers.Where( c => c.Type != ContainerType.System ).ToList()
            };

            try
            {
                string fileName = GetMainConfig().ItemsConfig ?? "items-config.json";
                string json = JsonSerializer.Serialize( clone, _jsonOptions );
                File.WriteAllText( GetFullPath( fileName ), json );
            }
            catch( Exception ex )
            {
                Debug.WriteLine( ex.Message );
            }
        }
    }

    public static ITheme? LoadTheme( string fileName )
    {
        string path = Path.Combine( AppDomain.CurrentDomain.BaseDirectory, fileName );
        if ( !File.Exists( path ) ) 
            return null;

        string json = File.ReadAllText( path );
        return JsonSerializer.Deserialize<Theme>( json );
    }

    public static void UpdatePortableJson( string itemName, string newPath )
    {
        lock( _lock )
        {
            try
            {
                string fullPath = GetFullPath( _portableAppsFile );

                if( !File.Exists( fullPath ) )
                    return;

                string json = File.ReadAllText( fullPath );
                List<PortableItem>? items = JsonSerializer.Deserialize<List<PortableItem>>( json );

                PortableItem? target = items?.FirstOrDefault( i => i.Name == itemName );

                if( target != null )
                {
                    if ( _cachedModel != null )
                    {
                        var itemInCache = _cachedModel.Containers
                            .SelectMany( c => c.Items )
                            .OfType<PortableItem>()
                            .FirstOrDefault( i => i.Name.Equals( itemName, StringComparison.OrdinalIgnoreCase ) );

                        if ( itemInCache != null )
                            itemInCache.FilePath = newPath;
                    }

                    // Сохраняем путь в ОТНОСИТЕЛЬНОМ виде (как он был в JSON изначально)
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    target.FilePath = Path.GetRelativePath( baseDir, newPath );

                    string updatedJson = JsonSerializer.Serialize( items, new JsonSerializerOptions { WriteIndented = true } );
                    File.WriteAllText( fullPath, updatedJson );
                }
            }
            catch( Exception ex )
            {
                Debug.WriteLine( $"Ошибка обновления JSON: {ex.Message}" );
            }
        }
    }
}
