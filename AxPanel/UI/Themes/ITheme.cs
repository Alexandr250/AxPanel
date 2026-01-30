using AxPanel.UI.ElementStyles;

namespace AxPanel.UI.Themes;

#pragma warning disable CS8618
public interface ITheme
{
    WindowStyle WindowStyle { get; }
    ContainerStyle ContainerStyle { get; }
    ButtonStyle ButtonStyle { get; }
}