using System.Threading.Tasks;

namespace PhoneBox.Abstractions
{
    public interface ITelephonyHubPublisher
    {
        Task OnCall(CallSubscriber subscriber, string phoneNumber);
    }

    public readonly struct CallSubscriber
    {
        public string PhoneNumber { get; }

        public CallSubscriber(string phoneNumber)
        {
            this.PhoneNumber = phoneNumber;
        }
    }
}