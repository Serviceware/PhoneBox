﻿namespace PhoneBox.Server
{
    internal sealed class AuthorizationOptions
    {
        public const string ConfigurationSectionName = "Authorization";

        public string? Authority { get; set; }
        public string Audience { get; set; } = "phone-box";
        public string SubscriberIdClaimType { get; set; } = "phone_number";
    }
}