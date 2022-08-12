namespace PhoneBox.Abstractions
{
    public interface ITelephonyConnector
    {
        void Register(CallSubscriber subscriber);
    }
}