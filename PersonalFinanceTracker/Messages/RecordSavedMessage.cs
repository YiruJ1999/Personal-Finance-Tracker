using CommunityToolkit.Mvvm.Messaging.Messages;

namespace PersonalFinanceTracker.Messages;

public class RecordSavedMessage : ValueChangedMessage<bool>
{
    public RecordSavedMessage() : base(true) { }
}
