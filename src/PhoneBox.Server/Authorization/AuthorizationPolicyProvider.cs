using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Options;

namespace PhoneBox.Server.Authorization
{
    /// <remarks>
    /// Unfortunately, the Authorization implementation doesn't honor bindable configuration sources like <see cref="IOptionsMonitor{AuthorizationOptions}"/>.
    /// Therefore we have to apply the changed configuration to the policy, everytime it's resolved.
    /// </remarks>
    internal sealed class AuthorizationPolicyProvider : DefaultAuthorizationPolicyProvider
    {
        private readonly IOptionsMonitor<AuthorizationOptions> _authorizationOptions;

        public AuthorizationPolicyProvider(IOptions<Microsoft.AspNetCore.Authorization.AuthorizationOptions> options, IOptionsMonitor<AuthorizationOptions> authorizationOptions) : base(options)
        {
            this._authorizationOptions = authorizationOptions;
        }

        public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            AuthorizationPolicy? policy = await base.GetPolicyAsync(policyName).ConfigureAwait(false);

            if (policy != null && policyName == "HubConsumer")
            {
                IAuthorizationRequirement subscriberIdClaimRequirement = new ClaimsAuthorizationRequirement(this._authorizationOptions.CurrentValue.SubscriberIdClaimType, allowedValues: null);
                policy = new AuthorizationPolicy(policy.Requirements.Append(subscriberIdClaimRequirement), policy.AuthenticationSchemes);
            }

            return policy;
        }
    }
}