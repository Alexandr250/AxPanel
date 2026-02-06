using AxPanel.UI.ElementStyles;

namespace AxPanel.UI.Themes;

public class Theme : ITheme
{
    public WindowStyle WindowStyle { get; set; }
    public ContainerStyle ContainerStyle { get; set; }
    public ButtonStyle ButtonStyle { get; set; }
}
