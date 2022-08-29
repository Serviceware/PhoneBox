using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace PhoneBox.Client
{
    internal sealed class AccessTokenProvider : IAccessTokenProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<AuthorizationOptions> _options;

        public AccessTokenProvider(IHttpClientFactory httpClientFactory, IOptions<AuthorizationOptions> options)
        {
            this._httpClientFactory = httpClientFactory;
            this._options = options;
        }

        public async Task<string?> GetAccessToken()
        {
            using (HttpClient client = this._httpClientFactory.CreateClient())
            {
                if (this._options.Value.Authority == null)
                    throw new InvalidOperationException("Authorization.Authority not configured");

                OpenIdConnectDiscoveryDocument? discoveryDocument = await CollectDiscoveryDocument(client, this._options.Value.Authority).ConfigureAwait(false);
                if (String.IsNullOrEmpty(discoveryDocument.TokenEndpoint))
                    throw new InvalidOperationException("Could not find 'token_endpoint' in OpenIdConnect discovery document");

                FormUrlEncodedContent content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string?>("grant_type", "password")
                  , new KeyValuePair<string, string?>("client_id", this._options.Value.ClientId)
                  , new KeyValuePair<string, string?>("username", this._options.Value.UserName)
                  , new KeyValuePair<string, string?>("password", this._options.Value.Password)
                });

                HttpResponseMessage response = await client.PostAsync(discoveryDocument.TokenEndpoint, content).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                TokenResponse? result = await response.Content.ReadFromJsonAsync<TokenResponse>().ConfigureAwait(false);
                if (String.IsNullOrEmpty(result?.AccessToken))
                    throw new InvalidOperationException("Did not receive a valid credential from token endpoint");

                return result.AccessToken;
            }
        }

        private static async Task<OpenIdConnectDiscoveryDocument> CollectDiscoveryDocument(HttpClient client, string authority)
        {
            HttpResponseMessage response = await client.GetAsync($"{authority.TrimEnd('/')}/.well-known/openid-configuration").ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            OpenIdConnectDiscoveryDocument? document = await response.Content.ReadFromJsonAsync<OpenIdConnectDiscoveryDocument>().ConfigureAwait(false);

            if (document == null)
                throw new InvalidOperationException($"Did not get discovery document from endpoint: {response.RequestMessage!.RequestUri}");

            return document;
        }

        private sealed class OpenIdConnectDiscoveryDocument
        {
            [JsonPropertyName("token_endpoint")]
            public string? TokenEndpoint { get; set; }
        }

        private sealed class TokenResponse
        {
            [JsonPropertyName("access_token")]
            public string? AccessToken { get; set; }
        }
    }
}