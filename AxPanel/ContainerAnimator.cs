using AxPanel.UI.Themes;
using AxPanel.UI.UserControls;

namespace AxPanel;
public class ContainerAnimator : IDisposable
{
    private readonly System.Windows.Forms.Timer _animationTimer;

    private readonly RootContainerView _targetContainer;
    private readonly ITheme _theme;
    private int _targetSelectedHeight;

    private const int _step = 40; // Скорость раскрытия

    public event Action<ButtonContainerView>? HoverRequested;

    public ContainerAnimator( RootContainerView targetContainer, ITheme theme )
    {
        _animationTimer = new System.Windows.Forms.Timer { Interval = 15 };
        _animationTimer.Tick += ( s, e ) => AnimateStep();

        _targetContainer = targetContainer;
        _theme = theme;
    }

    private void AnimateStep()
    {
        bool stillAnimating = false;
        int currentTop = 0;

        foreach ( ButtonContainerView container in _targetContainer.Containers )
        {
            container.Top = currentTop;
            container.Width = _targetContainer.Width;

            int targetH = ( container == _targetContainer.Selected ) ? _targetSelectedHeight : _theme.ContainerStyle.HeaderHeight;

            if ( container.Height != targetH )
            {
                stillAnimating = true;
                int diff = targetH - container.Height;

                if ( Math.Abs( diff ) <= _step ) container.Height = targetH;
                else container.Height += Math.Sign( diff ) * _step;
            }

            currentTop += container.Height;
        }

        if ( !stillAnimating )
            StopAnimateArrange();

        // Проверка: если над свернутой панелью что-то тащат — раскрываем
        foreach ( ButtonContainerView container in _targetContainer.Containers )
        {
            Point clientPos = container.PointToClient( Cursor.Position );
            if ( container.DisplayRectangle.Contains( clientPos ) && _targetContainer.Selected != container )
            {
                // Раскрываем панель "на лету"
                HoverRequested?.Invoke( container );
            }
        }
    }

    public void StartAnimateArrange()
    {
        _targetSelectedHeight = _targetContainer.Height - ( _targetContainer.Controls.OfType<ButtonContainerView>().Count() - 1 ) * _theme.ContainerStyle.HeaderHeight - _targetContainer.Footer.Height;
        _animationTimer.Start();
    }

    public void StopAnimateArrange()
    {
        _animationTimer.Stop();
    }

    public void Dispose() => _animationTimer?.Dispose();
}
