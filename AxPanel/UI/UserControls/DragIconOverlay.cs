using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AxPanel.UI.UserControls;
public class DragIconOverlay : Form
{
    private Bitmap _iconBitmap;

    public DragIconOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        AllowTransparency = true;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta; // Делаем фон прозрачным
        Size = new Size( 32, 32 );
        StartPosition = FormStartPosition.Manual;
    }

    public void SetIcon( string filePath )
    {
        // Извлекаем системную иконку файла
        using var icon = Icon.ExtractAssociatedIcon( filePath );
        if ( icon != null )
        {
            _iconBitmap?.Dispose();
            _iconBitmap = icon.ToBitmap();
            Invalidate();
        }
    }

    protected override void OnPaint( PaintEventArgs e )
    {
        if ( _iconBitmap != null )
            e.Graphics.DrawImage( _iconBitmap, 0, 0, 32, 32 );
    }

    // Чтобы окно не "воровало" фокус у основной программы
    protected override bool ShowWithoutActivation => true;
}
