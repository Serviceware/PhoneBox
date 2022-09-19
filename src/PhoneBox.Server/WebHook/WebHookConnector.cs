using PhoneBox.Abstractions;

namespace PhoneBox.Server.WebHook
{
    internal sealed class WebHookConnector : ITelephonyConnector
    {
        void ITelephonyConnector.Subscribe(CallSubscriberConnection connection) { }
        void ITelephonyConnector.Unsubscribe(CallSubscriberConnection connection) { }
    }
}