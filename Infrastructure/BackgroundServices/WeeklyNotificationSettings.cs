namespace tmr_backend.Infrastructure.BackgroundServices;

public sealed class WeeklyNotificationSettings
{
    public bool Enabled { get; set; } = false;
    public string DayOfWeek { get; set; } = "Friday";
    public int Hour { get; set; } = 17;
    public int Minute { get; set; } = 0;
    public int CheckIntervalMinutes { get; set; } = 5;
}
