namespace AxPanel.Model;

public struct ProcessStats
{
    public bool IsRunning;
    public float CpuUsage;
    public float RamMb;
    public int WindowCount; // Новое поле для счетчика окон
    public DateTime? StartTime;
}