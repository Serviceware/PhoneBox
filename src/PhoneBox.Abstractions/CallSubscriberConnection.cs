namespace PhoneBox.Abstractions
{
    public readonly struct CallSubscriberConnection
    {
        public string ConnectionId { get; }
        public CallSubscriber Subscriber { get; }

        public CallSubscriberConnection(string connectionId, CallSubscriber subscriber)
        {
            this.ConnectionId = connectionId;
            this.Subscriber = subscriber;
        }
    }
}