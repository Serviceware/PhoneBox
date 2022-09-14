namespace PhoneBox.Abstractions
{
    public enum CallHangUpReason
    {
        None = 0,
        CallerClosed = 1,
        RecipientClosed = 2,
        BothClosed = 4
    }
}