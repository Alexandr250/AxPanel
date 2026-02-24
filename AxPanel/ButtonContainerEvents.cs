using AxPanel.Model;
using AxPanel.UI.UserControls;

namespace AxPanel;

public class ButtonContainerEvents : IDisposable
{
    public event Action<ButtonContainerView> ContainerSelected;
    public event Action<List<LaunchItem>> ItemCollectionChanged;
    public event Action<ButtonContainerView> ContainerDeleteRequested;
    public event Action<LaunchButtonView, object?> ProcessStartRequested;
    public event Action<LaunchButtonView> ProcessStartAsAdminRequested;
    public event Action<LaunchButtonView> GroupStartRequested;
    public event Action<string> ExplorerOpenRequested;
    public event Action<ButtonContainerView> DragHoverActivated;

    // Вспомогательные методы для вызова (чтобы не писать ?.Invoke везде во View)
    public void RaiseSelected( ButtonContainerView v ) => ContainerSelected?.Invoke( v );
    public void RaiseCollectionChanged( List<LaunchItem> i ) => ItemCollectionChanged?.Invoke( i );
    public void RaiseDelete( ButtonContainerView v ) => ContainerDeleteRequested?.Invoke( v );
    public void RaiseProcessStart( LaunchButtonView b, object? a ) => ProcessStartRequested?.Invoke( b, a );
    public void RaiseProcessAdminStart( LaunchButtonView b ) => ProcessStartAsAdminRequested?.Invoke( b );
    public void RaiseGroupStart( LaunchButtonView b ) => GroupStartRequested?.Invoke( b );
    public void RaiseExplorer( string p ) => ExplorerOpenRequested?.Invoke( p );
    public void RaiseDragHover( ButtonContainerView v ) => DragHoverActivated?.Invoke( v );

    public void Dispose()
    {
        // Принудительно отписываем всех слушателей
        ContainerSelected = null;
        ItemCollectionChanged = null;
        ContainerDeleteRequested = null;
        ProcessStartRequested = null;
        ProcessStartAsAdminRequested = null;
        GroupStartRequested = null;
        ExplorerOpenRequested = null;
        DragHoverActivated = null;
    }
}
