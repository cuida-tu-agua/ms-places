using SyWater.Places.Application.Sharing;

namespace SyWater.Places.Application.Ports.Out;

/// <summary>The accounts live in ms-iam: this is how places finds the people behind an id or an e-mail.</summary>
public interface IUserDirectory
{
    /// <exception cref="ExternalServiceUnavailableException">ms-iam did not answer.</exception>
    Task<IReadOnlyDictionary<Guid, UserContact>> GetContactsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);

    /// <summary>The registered user with that e-mail, or null if nobody has an account with it.</summary>
    /// <exception cref="ExternalServiceUnavailableException">ms-iam did not answer.</exception>
    Task<UserContact?> FindByEmailAsync(string email, CancellationToken ct);
}

/// <summary>Sends the invitation e-mail (through ms-iam, which owns the mail server).</summary>
public interface IInvitationNotifier
{
    /// <summary>True if the mail was handed to the mail service; false if it could not be sent right now.</summary>
    Task<bool> SendInvitationAsync(InvitationMail mail, CancellationToken ct);
}

/// <summary>The secret part of a guest link: long, random, impossible to guess.</summary>
public interface IGuestTokenGenerator
{
    string NewToken();
}
