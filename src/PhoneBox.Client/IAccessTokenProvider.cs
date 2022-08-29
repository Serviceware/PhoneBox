using System.Threading.Tasks;

namespace PhoneBox.Client
{
    internal interface IAccessTokenProvider
    {
        Task<string?> GetAccessToken();
    }
}