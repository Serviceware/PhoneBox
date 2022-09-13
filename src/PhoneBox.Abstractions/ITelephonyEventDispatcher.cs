using System.Threading.Tasks;

namespace PhoneBox.Abstractions
{
    public interface ITelephonyEventDispatcher
    {
        Task OnCallConnected(CallConnectedEvent call);
        Task OnCallDisconnected(CallDisconnectedEvent call);
    }
}