using System.Threading.Tasks;

namespace PhoneBox.Abstractions
{
    public interface ITelephonyEventDispatcher
    {
        Task OnCallNotification(CallNotificationEvent call);
        Task OnCallState(CallStateEvent call);

    }
}