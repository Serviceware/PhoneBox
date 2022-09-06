using System.Threading.Tasks;

namespace PhoneBox.Abstractions
{
    public interface ITelephonyHub
    {
        Task ReceiveCallNotification(CallNotificationEvent content);
        Task ReceiveCallState(CallStateEvent content);
    }
}