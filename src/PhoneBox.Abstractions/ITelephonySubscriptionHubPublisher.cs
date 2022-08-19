using System.Threading.Tasks;

namespace PhoneBox.Abstractions
{
    public interface ITelephonySubscriptionHubPublisher
    {
        Task OnCallNotification(CallNotificationEvent call);
        Task OnCallState(CallStateEvent call);

    }
}