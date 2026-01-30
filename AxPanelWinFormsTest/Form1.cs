using AxPanel.Model;
using AxPanel.SL;
using AxPanel.UI.UserControls;

namespace AxPanelWinFormsTest;

public partial class Form1 : Form
{
    private MainModel _model;

    public Form1()
    {
        InitializeComponent();

        MainModel panelModel = ConfigManager.ReadModel();

        foreach ( ContainerItem container in panelModel.Containers )
        {
            AxPanelContainer uiContainer = axPanelMainContainer1.AddContainer( container.Name, container.Items );
            uiContainer.ItemCollectionChanged += list =>
            {
                if( list is { Count: > 0 } )
                    container.Items.AddRange( list );

                ConfigManager.SaveItemsConfig( panelModel );
            };
        }
    }

    private void Form1_KeyDown( object sender, KeyEventArgs e )
    {
        axPanelMainContainer1.RaiseKeyDown( e );
    }

    private void Form1_KeyUp( object sender, KeyEventArgs e )
    {
        axPanelMainContainer1.RaiseKeyUp( e );
    }
}
