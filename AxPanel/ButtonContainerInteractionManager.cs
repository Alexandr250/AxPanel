using AxPanel.UI.UserControls;

namespace AxPanel;

public class ButtonContainerInteractionManager
{
    private readonly ButtonContainerView _view;

    public ButtonContainerInteractionManager( ButtonContainerView view )
    {
        _view = view ?? throw new ArgumentNullException( nameof( view ) );
    }

    #region Команды запуска (Execution)

    public void RequestStart( LaunchButtonView button, object? args )
        => _view.NotifyProcessStart( button, args );

    public void RequestAdminStart( LaunchButtonView button )
        => _view.NotifyProcessStartAsAdmin( button );

    public void RequestExplorer( string path )
        => _view.NotifyExplorerOpen( path );

    public void RequestGroupStart( LaunchButtonView separator )
        => _view.StartProcessGroup( separator );

    #endregion

    #region Управление кнопками (Lifecycle & Order)

    public void RequestDelete( LaunchButtonView button )
        => _view.RemoveButton( button );

    public void HandleReorder( LaunchButtonView button )
        => _view.HandleButtonReorder( button );

    #endregion

    #region Состояние мыши и Dragging

    public void OnButtonDragMove()
    {
        _view.HandleButtonDragMove();
    }

    public void OnButtonDragEnd()
    {
        _view.HandleButtonDragEnd();
    }

    #endregion
}
