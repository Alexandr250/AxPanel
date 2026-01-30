namespace AxPanel.Model;

public enum ContainerType
{
    Normal,
    System
}

public class ContainerItem
{
    public List<LaunchItem> Items { get; set; } = new List<LaunchItem>();

    public string Name { get; set; }

    public ContainerType Type { get; set; } = ContainerType.Normal;
}