using System.Diagnostics;
using AxPanel;
using AxPanel.UI.Themes;
using System.Text.Json;
using System.Text.Json.Serialization;
using AxPanel.SL;
using AxPanel.UI.ElementStyles;
using AxPanel.Model;
using System.IO;

namespace AxPanelWinFormsTest;

internal static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        /*var c = ConfigManager.ReadItemsConfig();
        return;*/

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        //ApplicationConfiguration.Initialize();
        //Application.Run( new Form1() );

        //var options = new JsonSerializerOptions
        //{
        //    WriteIndented = true
        //};

        //DarkTheme theme1 = new DarkTheme();
        //OldTheme theme2 = new OldTheme();

        //string jsonString = JsonSerializer.Serialize( theme1, options );
        //File.WriteAllText( "dark-theme.json", jsonString );

        //jsonString = JsonSerializer.Serialize( theme2, options );
        //File.WriteAllText( "old-theme.json", jsonString );

        //ProcessMonitor processMonitor = new();
        //processMonitor.StatisticsUpdated += dic =>
        //{
        //    //Debug. // Очищаем консоль, чтобы статистика обновлялась на одном месте
        //    Debug.WriteLine( $"{"Process Path",-40} | {"Status",-10} | {"CPU %",-7} | {"RAM MB",-8} | {"Windows",-7}" );

        //    Console.WriteLine( new string( '-', 85 ) );

        //    foreach ( var kvp in dic )
        //    {
        //        string path = Path.GetFileName( kvp.Key ); // Берем только имя файла для краткости
        //        var s = kvp.Value;

        //        if ( s.IsRunning )
        //        {
        //            Debug.WriteLine( $"{path,-40} | {"Running",-10} | {s.CpuUsage,6:F1}% | {s.RamMb,8} | {s.WindowCount,7}" );
        //        }
        //        else
        //        {
        //            Debug.WriteLine( $"{path,-40} | {"Stopped",-10} | {"-",6} | {"-",8} | {"-",7}" );
        //        }
        //    }
        //};
        //processMonitor.TargetPaths = new HashSet<string>( [ "Vivaldi.exe" ] );
        //processMonitor.Start();

        //while ( true )
        //{

        //}

        var portableItems = LoadPortableItems( "portable-apps.json" );
    }

    public static List<PortableItem> LoadPortableItems( string fileName )
    {
        try
        {
            string path = Path.Combine( AppDomain.CurrentDomain.BaseDirectory, fileName );
            if ( !File.Exists( path ) ) return new List<PortableItem>();

            string json = File.ReadAllText( path );

            // Прямая десериализация в список портативок
            var items = JsonSerializer.Deserialize<List<PortableItem>>( json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            } );

            if ( items != null )
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                foreach ( var item in items )
                {
                    // Приводим относительный путь Apps\... к абсолютному
                    if ( !string.IsNullOrEmpty( item.FilePath ) )
                        item.FilePath = Path.GetFullPath( Path.Combine( baseDir, item.FilePath ) );
                }
            }

            return items ?? new List<PortableItem>();
        }
        catch ( Exception ex )
        {
            Debug.WriteLine( $"Ошибка загрузки PortableItems: {ex.Message}" );
            return new List<PortableItem>();
        }
    }
}