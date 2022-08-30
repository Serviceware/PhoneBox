using PhoneBox.Abstractions;

namespace PhoneBox.Server.WebHook
{
    internal sealed class WebHookConnector : ITelephonyConnector
    {
        void ITelephonyConnector.Subscribe(CallSubscriber subscriber) { }
    }
}