using Microsoft.AspNetCore.Builder;

namespace PhoneBox.Abstractions
{
    public interface ITelephonyConnectorSetup
    {
        void Setup(WebApplication application);
    }
}