﻿// <copyright file="TokenAcquisitionHelper.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Microsoft.Teams.Apps.EmployeeTraining.Helpers
{
#pragma warning disable IDE0005 // Using directive is unnecessary.
    using global::Azure.Identity;
#pragma warning restore IDE0005 // Using directive is unnecessary.
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Options;
    using Microsoft.Identity.Client;
    using Microsoft.Teams.Apps.EmployeeTraining.Models.Configuration;

    /// <summary>
    /// Provides methods to fetch user and application access token for Graph scopes.
    /// </summary>
    public class TokenAcquisitionHelper : ITokenAcquisitionHelper
    {
        /// <summary>
        /// Represents scopes required by MsalNet for accessing token.
        /// </summary>
        private readonly List<string> messages = new List<string>();

        /// <summary>
        /// Represents scopes required by MsalNet for accessing token.
        /// </summary>
        private readonly string[] scopesRequestedByMsalNet = new string[]
        {
            "openid",
            "profile",
            "offline_access",
        };

        /// <summary>
        /// Represents application access scopes.
        /// </summary>
        private readonly string[] applicationScopesList = new string[]
        {
            "https://graph.microsoft.com/.default",
        };

        /// <summary>
        /// Instance of IOptions to read data from application configuration.
        /// </summary>
        private readonly IOptions<AzureSettings> azureSettings;

        /// <summary>
        /// Instance of confidential client app to access web API.
        /// </summary>
        private IConfidentialClientApplication confidentialClientApp;

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenAcquisitionHelper"/> class.
        /// </summary>
        /// <param name="confidentialClientApp">Instance of ConfidentialClientApplication class.</param>
        /// <param name="botOptions">Instance of IOptions for BotSettings to read data from application configuration.</param>
        public TokenAcquisitionHelper(
            IConfidentialClientApplication confidentialClientApp,
            IOptions<AzureSettings> botOptions)
        {
            this.confidentialClientApp = confidentialClientApp;
            this.azureSettings = botOptions;
        }

        /// <summary>
        /// Get token on behalf of user and add it to cache.
        /// </summary>
        /// <param name="userAadId">Azure AD object identifier for logged in user.</param>
        /// <param name="jwtToken">Id token of user.</param>
        /// <returns>Token with graph scopes.</returns>
        public async Task<string> GetUserAccessTokenAsync(string userAadId, string jwtToken)
        {
            try
            {
                List<string> scopeList = this.azureSettings.Value.GraphScope.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                this.messages.Add(this.azureSettings.Value.GraphScope);

                // Gets user account from the accounts available in token cache.
                // https://docs.microsoft.com/en-us/dotnet/api/microsoft.identity.client.clientapplicationbase.getaccountasync?view=azure-dotnet
                // Concatenation of UserObjectId and TenantId separated by a dot is used as unique identifier for getting user account.
                // https://docs.microsoft.com/en-us/dotnet/api/microsoft.identity.client.accountid.identifier?view=azure-dotnet#Microsoft_Identity_Client_AccountId_Identifier
                var account = await this.confidentialClientApp.GetAccountAsync($"{userAadId}.{this.azureSettings.Value.TenantId}");

                // Attempts to acquire an access token for the account from the user token cache.
                // https://docs.microsoft.com/en-us/dotnet/api/microsoft.identity.client.clientapplicationbase.acquiretokensilent?view=azure-dotnet
                this.messages.Add("Acquire token silent");
                AuthenticationResult result = await this.confidentialClientApp
                    .AcquireTokenSilent(scopeList, account)
                    .ExecuteAsync();
                this.messages.Add("Acquired token silent");
                return result.AccessToken;
            }
            catch (MsalUiRequiredException ex)
            {
                this.messages.Add("exception token silent");
                this.messages.Add(ex.Message);

                // If token does no exist in cache then get token on behalf of user.
                return await this.AquireTokenOnBehalfOfUserAsync(this.azureSettings.Value.GraphScope, jwtToken);
                this.messages.Add("Acquire token on behalf done");
            }
        }

        /// <summary>
        /// Get user Azure AD access token.
        /// </summary>
        /// <returns>Access token with Graph scopes.</returns>
        public async Task<string> GetApplicationAccessTokenAsync()
        {
            this.messages.Add("Access token client app");
            AuthenticationResult result = await this.confidentialClientApp
                .AcquireTokenForClient(this.applicationScopesList)
                .WithAuthority($"https://login.microsoftonline.com/{this.azureSettings.Value.TenantId}")
                .ExecuteAsync();
            this.messages.Add("Got the access token");
            return result.AccessToken;
        }

        /// <summary>
        /// Returns the logs
        /// </summary>
        /// <returns>return the logs</returns>
        public string GetLogs()
        {
            return string.Join("\n", this.messages);
        }

        /// <summary>
        /// Get token on behalf of user.
        /// </summary>
        /// <param name="graphScopes">Graph scopes to be added to token.</param>
        /// <param name="jwtToken">JWT bearer token.</param>
        /// <returns>Token with graph scopes.</returns>
        private async Task<string> AquireTokenOnBehalfOfUserAsync(string graphScopes, string jwtToken)
        {
            this.messages.Add("Acquire token on behalf of user async");

            graphScopes = graphScopes ?? throw new ArgumentNullException(nameof(graphScopes));
            jwtToken = jwtToken ?? throw new ArgumentNullException(nameof(jwtToken));
            UserAssertion userAssertion = new UserAssertion(jwtToken, "urn:ietf:params:oauth:grant-type:jwt-bearer");
            IEnumerable<string> requestedScopes = graphScopes.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            this.messages.Add("in acquire token ");
            this.messages.Add("graph scopes");
            this.messages.Add(graphScopes);

            this.messages.Add("requested scopes");
            this.messages.Add(string.Join(" ", requestedScopes));
            this.messages.Add("scopes requested by msal net");
            this.messages.Add(string.Join(" ", this.scopesRequestedByMsalNet));

            // Result to make sure that the cache is filled-in before the controller tries to get access tokens
            var result = await this.confidentialClientApp.AcquireTokenOnBehalfOf(
                requestedScopes.Except(this.scopesRequestedByMsalNet),
                userAssertion)
                .ExecuteAsync();

            this.messages.Add("access token");
            this.messages.Add(result.AccessToken);

            return result.AccessToken;
        }
    }
}
