namespace PhoneBox.Abstractions
{
    public interface ITelephonyConnector
    {
        void Subscribe(CallSubscriberConnection connection);
        void Unsubscribe(CallSubscriberConnection connection);
    }
}