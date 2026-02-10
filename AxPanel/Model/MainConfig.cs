using System.Text.Json.Serialization;

namespace AxPanel.Model;

public class MainConfig
{
    public int Top { get; set; }

    public int Left { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public int ScroolValueIncrement { get; set; }

    public int HeaderHeight { get; set; }

    public int ContainerHeaderHeight { get; set; }

    public int BorderWidth { get; set; }

    public string ThemeFileName { get; set; }

    public string ItemsConfig { get; set; }

    [JsonConverter( typeof( JsonStringEnumConverter ) )]
    public LayoutMode LayoutMode { get; set; } = LayoutMode.List;
}