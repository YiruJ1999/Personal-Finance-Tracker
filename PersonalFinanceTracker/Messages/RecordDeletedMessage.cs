using CommunityToolkit.Mvvm.Messaging.Messages;

namespace PersonalFinanceTracker.Messages;

public class RecordDeletedMessage : ValueChangedMessage<bool>
{
    public RecordDeletedMessage() : base(true) { }
}
