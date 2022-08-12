using System.Threading.Tasks;

namespace PhoneBox.Abstractions
{
    public interface ITelephonyHubPublisher
    {
        Task OnCall(CallSubscriber subscriber, CallInfo call);
    }
}