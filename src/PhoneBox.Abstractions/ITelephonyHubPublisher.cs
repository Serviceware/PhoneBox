using System.Threading.Tasks;

namespace PhoneBox.Abstractions
{
    public interface ITelephonyHubPublisher
    {
        ITelephonySubscriptionHubPublisher RetrieveSubscriptionHubPublisher(CallSubscriber subscriber);
    }
    public interface ITelephonySubscriptionHubPublisher
    {
        Task OnCallNotification(CallNotificationEvent call);
        Task OnCallState(CallStateEvent call);

    }
}