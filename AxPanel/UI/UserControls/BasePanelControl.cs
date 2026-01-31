namespace AxPanel.UI.UserControls;

public abstract class BasePanelControl : BaseControl
{
    public BasePanelControl()
    {
        DoubleBuffered = true;

        this.SetStyle( ControlStyles.AllPaintingInWmPaint |  // Игнорировать WM_ERASEBKGND для уменьшения мерцания
                       ControlStyles.UserPaint |             // Контрол рисует себя сам
                       ControlStyles.OptimizedDoubleBuffer | // Использовать буфер в памяти
                       ControlStyles.ResizeRedraw, true );   // Перерисовывать при изменении размера
    }
}