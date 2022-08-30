namespace PhoneBox.Abstractions
{
    public interface ITelephonyEventDispatcherFactory
    {
        ITelephonyEventDispatcher Create(CallSubscriber subscriber);
    }
}