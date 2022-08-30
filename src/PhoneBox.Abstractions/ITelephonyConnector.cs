namespace PhoneBox.Abstractions
{
    public interface ITelephonyConnector
    {
        void Subscribe(CallSubscriber subscriber);
    }
}