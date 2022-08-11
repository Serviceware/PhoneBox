using System.Threading.Tasks;

namespace PhoneBox
{
    public interface ITelephonyHub
    {
        Task SendMessage(string message);
    }
}