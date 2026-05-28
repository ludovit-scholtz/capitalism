namespace MasterApi.Configuration;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; set; }

    public string AzureCommunicationServicesConnectionString { get; set; } = string.Empty;

    public string SenderAddress { get; set; } = "no-reply@capitalism.local";

    public string SenderName { get; set; } = "Capitalism";

    public bool WeeklyReportsEnabled { get; set; }

    public int WeeklyReportUtcHour { get; set; } = 12;

    public DayOfWeek WeeklyReportDayOfWeek { get; set; } = DayOfWeek.Friday;

    public int SchedulerIntervalMinutes { get; set; } = 30;
}
