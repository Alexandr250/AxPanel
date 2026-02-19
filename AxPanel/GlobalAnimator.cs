using AxPanel.Contracts;
using AxPanel.UI.UserControls;
using Timer = System.Windows.Forms.Timer;

namespace AxPanel;

public class GlobalAnimator : IDisposable
{
    // Поля для хранения скорости изменения (velocity)
    private static float _velocity = 0f;
    public static float SpringStiffness = 0.15f; // Жесткость
    public static float SpringDamping = 0.5f;    // Затухание (0.8 - вязко, 0.3 - прыгуче)

    private readonly List<IAnimatable> _targets = [];
    private readonly Timer _timer;
    private const float LerpSpeed = 0.15f;

    public GlobalAnimator()
    {
        _timer = new Timer { Interval = 16 };
        _timer.Tick += ( s, e ) => Tick();
        _timer.Start();
    }

    public void Register( IAnimatable target ) => _targets.Add( target );
    public void Unregister( IAnimatable target ) => _targets.Remove( target );

    private void Tick()
    {
        if ( _targets.Count == 0 ) return;

        foreach ( IAnimatable target in _targets.ToList() )
        {
            AnimateContainer( target );
        }
    }

    private static void AnimateContainer( IAnimatable container )
    {
        IReadOnlyList<LaunchButtonView> buttons = container.Buttons;
        bool moved = false;

        for ( int i = 0; i < buttons.Count; i++ )
        {
            LaunchButtonView btn = buttons[ i ];

            if ( btn.Capture || btn.IsDragging )
                continue;

            (Point Location, int Width) layout = container.LayoutEngine.GetLayout( i, container.ScrollValue, container.Width, buttons, container.Theme );

            btn.Left = ( int )Lerp( btn.Left, layout.Location.X, out bool m1 );
            btn.Top = ( int )Lerp( btn.Top, layout.Location.Y, out bool m2 );
            btn.Width = ( int )Lerp( btn.Width, layout.Width, out bool m3 );

            if ( m1 || m2 || m3 ) moved = true;
        }

        if ( moved )
            container.UpdateVisual();
    }

    private static float Lerp( float current, float target, out bool moved )
    {
        float diff = target - current;
        if ( Math.Abs( diff ) > 0.5f )
        {
            moved = true;
            return current + diff * LerpSpeed;
        }
        moved = false;
        return target;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        _targets.Clear();
    }

    public static float CalculateSpring( float current, float target )
    {
        // Рассчитываем силу притяжения к цели
        float force = ( target - current ) * SpringStiffness;

        // Применяем силу к скорости и добавляем затухание
        _velocity = ( _velocity + force ) * SpringDamping;

        // Возвращаем новое значение
        return current + _velocity;
    }
}
