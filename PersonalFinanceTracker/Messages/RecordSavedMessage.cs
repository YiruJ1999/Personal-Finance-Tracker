// Personal Finance Tracker
// File: PersonalFinanceTracker/Messages/RecordSavedMessage.cs
// Purpose: Defines a lightweight message payload used for view model communication.

using CommunityToolkit.Mvvm.Messaging.Messages;

namespace PersonalFinanceTracker.Messages;

public class RecordSavedMessage : ValueChangedMessage<bool>
{
    public RecordSavedMessage() : base(true) { }
}

