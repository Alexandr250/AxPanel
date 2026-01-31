namespace AxPanel.Model;

public record ProcessStats(
    bool IsRunning,
    float CpuUsage,
    float RamMb,
    DateTime? StartTime
);