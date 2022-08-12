using System.Threading.Tasks;

namespace PhoneBox.Abstractions
{
    public interface ITelephonyHub
    {
        Task SendMessage(string message);
        Task Call(CallInfo call);
    }
}