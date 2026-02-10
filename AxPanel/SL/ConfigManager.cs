using AxPanel.Model;
using AxPanel.UI.Themes;
using Microsoft.VisualBasic.Devices;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace AxPanel.SL;

public class ConfigManager
{
    private static readonly object _lock = new();

    private static string ItemsConfigFile = "items-config.json";
    private const string MainConfigFile = "config.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

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
        string path = GetFullPath( MainConfigFile );
        if ( File.Exists( path ) )
        {
            try
            {
                string json = File.ReadAllText( path );
                return JsonSerializer.Deserialize<MainConfig>( json ) ?? CreateDefaultConfig();
            }
            catch { return CreateDefaultConfig(); }
        }

        var defaultConfig = CreateDefaultConfig();
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
                string json = JsonSerializer.Serialize( mainConfig, JsonOptions );
                File.WriteAllText( GetFullPath( MainConfigFile ), json );
            }
            catch ( Exception ex ) { Debug.WriteLine( ex.Message ); }
        }
    }
    
    private static MainConfig CreateDefaultConfig()
    {
        var defaultConfig = new MainConfig
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

    

    [Obsolete]
    public static MainModel ReadItemsConfig()
    {
        return GetModel(); // Перенаправляем на актуальный метод для чистоты
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
                if ( _cachedModel == null )
                {
                    _cachedModel = ReadModelFromDisk();
                }
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
        if ( panelModel == null || panelModel.Containers == null || panelModel.Containers.Count == 0 )
        {
            panelModel = new MainModel { Containers = new List<ContainerItem>() };

            var emptyContainer = new ContainerItem
            {
                Name = "Новая панель",
                Items = new List<LaunchItem>(),
                Type = ContainerType.Normal
            };

            panelModel.Containers.Add( emptyContainer );

            // Сразу сохраняем на диск (метод SaveItemsConfig тоже должен использовать lock)
            SaveItemsConfig( panelModel );
        }

        // 3. Индексация существующих элементов (ID для UI)
        foreach ( var container in panelModel.Containers )
        {
            if ( container.Items == null ) continue;
            for ( int i = 0; i < container.Items.Count; i++ )
            {
                if ( container.Items[ i ] != null )
                    container.Items[ i ].Id = i;
            }
        }

        // 4. Принудительное добавление системного контейнера
        // Мы не сохраняем его в JSON (согласно логике SaveItemsConfig), 
        // поэтому добавляем его каждый раз при чтении в память.
        if ( !panelModel.Containers.Any( c => c.Type == ContainerType.System ) )
        {
            panelModel.Containers.Add( BuildStandatrContainer() );
        }

        return panelModel;
    }

    //public static MainModel GetModel()
    //{
    //    MainModel? panelModel = null;

    //    // 1. Попытка чтения из файла
    //    if ( File.Exists( ItemsConfigFile ) )
    //    {
    //        try
    //        {
    //            string json = File.ReadAllText( ItemsConfigFile );
    //            panelModel = JsonSerializer.Deserialize<MainModel>( json );
    //        }
    //        catch ( Exception ex )
    //        {
    //            Debug.WriteLine( $"Ошибка загрузки: {ex.Message}" );
    //        }
    //    }

    //    // 2. Если файла нет, он пуст или в нем нет контейнеров — создаем один пустой
    //    if ( panelModel == null || panelModel.Containers == null || panelModel.Containers.Count == 0 )
    //    {
    //        panelModel = new MainModel { Containers = new List<ContainerItem>() };

    //        // Создаем абсолютно пустой контейнер (без кнопок)
    //        var emptyContainer = new ContainerItem
    //        {
    //            Name = "Новая панель",
    //            Items = [],
    //            Type = ContainerType.Normal // Предполагаем, что это обычный тип
    //        };

    //        panelModel.Containers.Add( emptyContainer );

    //        // Сохраняем, чтобы файл физически появился на диске
    //        SaveItemsConfig( panelModel );
    //    }

    //    // 3. Индексация существующих элементов (если они есть)
    //    foreach ( var container in panelModel.Containers )
    //    {
    //        if ( container.Items == null ) continue;
    //        for ( int i = 0; i < container.Items.Count; i++ )
    //        {
    //            if ( container.Items[ i ] != null ) container.Items[ i ].Id = i;
    //        }
    //    }

    //    // Принудительное добавление системного контейнера (если ваша логика это требует)
    //    if ( !panelModel.Containers.Any( c => c.Type == ContainerType.System ) )
    //    {
    //        panelModel.Containers.Add( BuildStandatrContainer() );
    //    }

    //    return panelModel;
    //}

    //private static FileInfo GetItemsConfigFileInfo()
    //{
    //    if( string.IsNullOrEmpty( ItemsConfigFile ) )
    //    {
    //        if( MainModel)
    //    }
    //}

    private static ContainerItem BuildStandatrContainer()
    {
        ContainerItem container = new()
        {
            Name = "Системные инструменты",
            Type = ContainerType.System,
            Items = []
        };

        var groups = new Dictionary<string, List<(string Name, string Path)>>
    {
        { "Основные", [
                ( "Проводник", Environment.GetEnvironmentVariable( "windir" ) + "\\explorer.exe" ),
                ( "Блокнот", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\notepad.exe" ),
                ( "Калькулятор", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\calc.exe" ),
                ( "Диспетчер задач", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\taskmgr.exe" )
            ]
        },
        { "Администрирование", [
                ( "Командная строка", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\cmd.exe" ),
                ( "PowerShell", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\WindowsPowerShell\\v1.0\\powershell.exe" ),
                ( "Редактор реестра", Environment.GetEnvironmentVariable( "windir" ) + "\\regedit.exe" ),
                ( "Службы", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\services.msc" ),
                ( "Управление компьютером", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\compmgmt.msc" )
            ]
        },
        { "Сеть и Настройка", [
                ( "Сетевые подключения", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\ncpa.cpl" ),
                ( "Панель управления", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\control.exe" ),
                ( "Удаление программ", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\appwiz.cpl" ),
                ( "Параметры звука", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\mmsys.cpl" )
            ]
        },
        { "Диагностика", [
                ( "Монитор ресурсов", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\resmon.exe" ),
                ( "Управление дисками", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\diskmgmt.msc" ),
                ( "Просмотр событий", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\eventvwr.msc" ),
                ( "Очистка диска", Environment.GetFolderPath( Environment.SpecialFolder.System ) + "\\cleanmgr.exe" )
            ]
        }
    };

        foreach ( var group in groups )
        {
            // Разделитель группы (FilePath пустой — отрисуется как разделитель)
            container.Items.Add( new LaunchItem
            {
                Name = $"--- {group.Key} ---",
                FilePath = string.Empty
            } );

            foreach ( var item in group.Value )
            {
                container.Items.Add( new LaunchItem
                {
                    Name = item.Name,
                    FilePath = item.Path // Теперь здесь ПОЛНЫЙ ПУТЬ
                } );
            }
        }

        for ( int i = 0; i < container.Items.Count; i++ ) 
            container.Items[ i ].Id = i;

        return container;
    }

    public static void SaveItemsConfig( MainModel panelModel )
    {
        lock ( _lock )
        {
            _cachedModel = panelModel;

            MainModel clone = new MainModel
            {
                Containers = panelModel.Containers.Where( c => c.Type != ContainerType.System ).ToList()
            };

            try
            {
                string fileName = GetMainConfig().ItemsConfig ?? "items-config.json";
                string json = JsonSerializer.Serialize( clone, JsonOptions );
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
}
