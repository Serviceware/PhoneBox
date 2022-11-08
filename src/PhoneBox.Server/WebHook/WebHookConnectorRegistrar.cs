using System;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using PhoneBox.Abstractions;

namespace PhoneBox.Server.WebHook
{
    internal sealed class WebHookConnectorRegistrar : TelephonyConnectorRegistrar<WebHookConnector>
    {
        private const string AuthorizationPolicyName = "WebHookConsumer";

        public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddGeneratedEndpoints();
            services.Configure<WebHookOptions>(configuration.GetSection(WebHookOptions.ConfigurationSectionName));
            services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, SecretKeyAuthenticationHandler>("WebHookConsumer", configureOptions: null);
            services.AddAuthorization(x =>
            {
                x.AddPolicy(AuthorizationPolicyName, y => y.AddAuthenticationSchemes("WebHookConsumer")
                                                           .RequireAuthenticatedUser()
                                                           .Build());
            });
        }

        public override void ConfigureApplication(WebApplication application)
        {
            application.AddGeneratedEndpoints(AuthorizationPolicyName);
        }

        private sealed class SecretKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
        {
            private readonly IOptionsMonitor<WebHookOptions> _webHookOptions;

            public SecretKeyAuthenticationHandler(IOptionsMonitor<WebHookOptions> webHookOptions, IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock) : base(options, logger, encoder, clock)
            {
                this._webHookOptions = webHookOptions;
            }

            protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(this.HandleAuthenticate());

            private AuthenticateResult HandleAuthenticate()
            {
                string token = base.Request.Headers[HeaderNames.Authorization];
                if (!String.IsNullOrEmpty(token) && token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    token = token.Substring(7).Trim();
                }

                if (String.IsNullOrEmpty(token))
                    return AuthenticateResult.NoResult();

                if (token != this._webHookOptions.CurrentValue.SharedSecret)
                    return AuthenticateResult.Fail("Invalid shared secret");

                Claim nameIdentifierClaim = new Claim(ClaimTypes.NameIdentifier, token);
                ClaimsPrincipal principal = new ClaimsPrincipal(new ClaimsIdentity(EnumerableExtensions.Create(nameIdentifierClaim), base.Scheme.Name));

                return AuthenticateResult.Success(new AuthenticationTicket(principal, base.Scheme.Name));
            }
        }
    }
}