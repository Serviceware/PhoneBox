using System.Threading.Tasks;

namespace PhoneBox.Abstractions
{
    public interface ITelephonyHub
    {
        Task SendMessage(string message);
        Task ReceiveCallNotification(CallNotificationEvent call);
        Task ReceiveCallState(CallStateEvent call);
    }
}