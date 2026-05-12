namespace Api.Types;

public enum SalaryLevel
{
    Minimum,
    Standard,
    Premium,
    Executive,
}

public enum DividendVoteChoiceInput
{
    Approve,
    Reject,
}

public sealed class SetCompanySalaryLevelInput
{
    public Guid CompanyId { get; set; }
    public Guid CityId { get; set; }
    public SalaryLevel SalaryLevel { get; set; } = SalaryLevel.Standard;
}

public sealed class VoteDividendInput
{
    public Guid CompanyId { get; set; }
    public DividendVoteChoiceInput Vote { get; set; }
}
