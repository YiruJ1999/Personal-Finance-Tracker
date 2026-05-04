// Personal Finance Tracker
// File: PersonalFinanceTracker/Messages/RecordDeletedMessage.cs
// Purpose: Defines a lightweight message payload used for view model communication.

using CommunityToolkit.Mvvm.Messaging.Messages;

namespace PersonalFinanceTracker.Messages;

public class RecordDeletedMessage : ValueChangedMessage<bool>
{
    public RecordDeletedMessage() : base(true) { }
}

