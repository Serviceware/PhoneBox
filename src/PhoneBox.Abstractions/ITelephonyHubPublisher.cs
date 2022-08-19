namespace PhoneBox.Abstractions
{
    public interface ITelephonyHubPublisher
    {
        ITelephonySubscriptionHubPublisher RetrieveSubscriptionHubPublisher(CallSubscriber subscriber);
    }
}