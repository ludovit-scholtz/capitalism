namespace MasterApi.Configuration;

public sealed class GameAdministrationOptions
{
    public const string SectionName = "GameAdministration";

    public List<string> RootAdministratorEmails { get; set; } = [];
}