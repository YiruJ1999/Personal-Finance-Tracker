// Messages/AccountChangedMessage.cs
using CommunityToolkit.Mvvm.Messaging.Messages;

public sealed class AccountChangedMessage : ValueChangedMessage<int>
{
    public AccountChangedMessage(int accountId) : base(accountId) { }
}
