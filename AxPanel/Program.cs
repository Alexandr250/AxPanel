using AxPanel.Model;
using AxPanel.SL;
using AxPanel.UI.UserControls;

namespace AxPanel;

internal static class Program
{
    private static AxPanelTelegramBot? _telegramBot;

    [STAThread]
    private static void Main()
    {
        AppDomain.CurrentDomain.UnhandledException += ( s, e ) => {
            MessageBox.Show( e.ExceptionObject.ToString(), "Необработанная ошибка" );
        };

        ApplicationConfiguration.Initialize();

        MainConfig config = ConfigManager.GetMainConfig();
        MainModel panelModel = ConfigManager.GetModel();

        MainView view = new();
        view.MainModel = panelModel;
        
        ConfigureMainPanelView( view, config, panelModel );
        view.Move += ( sender, args ) =>
        {
            config.Left = view.Left;
            config.Top = view.Top;
            config.Height = view.Height;
            config.Width = view.Width;
            ConfigManager.SaveMainConfig( config );
        };

        try
        {
            _telegramBot = new AxPanelTelegramBot( config );
            _telegramBot.StartAsync().GetAwaiter().GetResult();
        }
        catch ( Exception ex ) { }


        Application.Run( view );

        _telegramBot?.StopAsync().GetAwaiter().GetResult();
    }

    private static void ConfigureMainPanelView( MainView? mainView, MainConfig? config, MainModel panelModel )
    {
        if ( mainView != null && config != null )
        {
            mainView.Padding = new Padding( 
                config.BorderWidth, 
                config.HeaderHeight, 
                config.BorderWidth, 
                config.BorderWidth );
            
            mainView.Width = config.Width;
            mainView.Height = config.Height;
            mainView.Top = config.Top;
            mainView.Left = config.Left;

            foreach ( ContainerItem containerItem in panelModel.Containers )
            {
                ButtonContainerView uiContainer = mainView.MainContainer.AddContainer( containerItem.Name, containerItem.Items );
                
                uiContainer.ButtonContainerEvents.ItemCollectionChanged += list =>
                {
                    // 1. Если list == null, значит это программное изменение 
                    // (например, удаление контейнера или кнопки). Выход без сохранения.
                    if ( list == null ) 
                        return;

                    // 2. Если список получен от UI с изменённым порядком (после MouseUp или DragDrop)
                    // копируем новый список в модель и сохраняем на диск
                    containerItem.Items = list;

                    // 3. Сохраняем изменённую конфигурацию
                    ConfigManager.SaveItemsConfig( panelModel );
                };
            }
        }
    }
}