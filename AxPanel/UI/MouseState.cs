namespace AxPanel.UI;

public struct MouseState
{
    public MouseState() { }

    public bool MouseInControl { get; set; } = false;
    public bool MouseInDeleteButton { get; set; } = false;
    public bool ButtonMoved { get; set; } = false;

    public bool MouseInGroupPlay { get; set; } = false;
}