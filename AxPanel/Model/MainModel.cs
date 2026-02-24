using System.Text.Json.Serialization;

namespace AxPanel.Model;

public class MainModel
{
    [JsonPropertyOrder( -100 )] // Гарантирует, что поле будет в самом верху JSON
    public string ConfigSignature { get; set; } = "AxPanel items config file";

    public List<ContainerItem> Containers { get; set; } = [];
}