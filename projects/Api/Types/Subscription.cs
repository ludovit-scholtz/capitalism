using HotChocolate.Authorization;
namespace Api.Types;

public sealed class Subscription
{
    [Authorize]
    [Subscribe]
    [Topic]
    public InGameChatMessage? ChatMessageSent(Guid? cityId, [EventMessage] InGameChatMessage message)
        => message.CityId == cityId ? message : null;
}
