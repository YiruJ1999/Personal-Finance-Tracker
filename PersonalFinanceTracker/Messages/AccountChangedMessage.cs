// Personal Finance Tracker
// File: PersonalFinanceTracker/Messages/AccountChangedMessage.cs
// Purpose: Defines a lightweight message payload used for view model communication.

// Messages/AccountChangedMessage.cs
using CommunityToolkit.Mvvm.Messaging.Messages;

public sealed class AccountChangedMessage : ValueChangedMessage<int>
{
    public AccountChangedMessage(int accountId) : base(accountId) { }
}

