using System.Threading.Tasks;

namespace PhoneBox.Generators.Tests
{
    public interface ITelephonyHub
    {
        Task SendMessage(string message);
        Task ReceiveCallNotification(CallNotificationEvent content);
        Task ReceiveCallState(CallStateEvent content);
    }
}