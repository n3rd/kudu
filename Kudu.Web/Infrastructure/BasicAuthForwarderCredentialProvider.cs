using Kudu.Client.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;

namespace Kudu.Web.Infrastructure
{
    public class BasicAuthForwarderCredentialProvider : ICredentialProvider
    {

        private readonly ICredentials _credentials;

        public BasicAuthForwarderCredentialProvider(HttpContextBase context)
        {
            _credentials = ParseAuthHeader(context.Request.Headers["Authorization"]);
        }

        private static ICredentials ParseAuthHeader(string authHeader)
        {
            // Check this is a Basic Auth header
            if (authHeader == null || authHeader.Length == 0 || !authHeader.StartsWith("Basic", StringComparison.OrdinalIgnoreCase)) return null;

            // Pull out the Credentials with are seperated by ':' and Base64 encoded
            string base64Credentials = authHeader.Substring(6);
            string[] credentials = Encoding.ASCII.GetString(Convert.FromBase64String(base64Credentials)).Split(new char[] { ':' });

            if (credentials.Length != 2 || string.IsNullOrEmpty(credentials[0]) || string.IsNullOrEmpty(credentials[0])) return null;

            // Okay this is the credentials
            return new NetworkCredential(credentials[0], credentials[1]);
        }

        public System.Net.ICredentials GetCredentials()
        {
            return _credentials;
        }
    }
}