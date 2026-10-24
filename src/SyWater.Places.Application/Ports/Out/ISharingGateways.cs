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

/// <summary>
/// HU-042 / HU-043 · ms-notification: the invited person finds the invitation in the notification center (and gets it by
/// WhatsApp / SMS when their phone is verified), and the owner learns the answer. Best effort: the invitation is already
/// saved, so a notification service that is down never makes it fail.
/// </summary>
public interface IInvitationAlerts
{
    /// <summary>True if ms-notification took it.</summary>
    Task<bool> InvitationSentAsync(InvitationAlert alert, CancellationToken ct);

    /// <summary>True if ms-notification took it.</summary>
    Task<bool> InvitationAnsweredAsync(InvitationAnswerAlert alert, CancellationToken ct);
}
