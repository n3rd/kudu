using Kudu.Client.Infrastructure;
using System;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Web;

namespace Kudu.Web.Infrastructure
{
    public class BasicAuthProxyCredentialProvider : ICredentialProvider
    {
        private readonly ICredentials _credentials;

        public BasicAuthProxyCredentialProvider(HttpContextBase context)
        {
            var networkCredentials = new NetworkCredential();

            if (TryParseAuthHeader(context.Request.Headers, networkCredentials))
            {
                _credentials = networkCredentials;
            }
            else
            {
                _credentials = new NetworkCredential("admin", "kudu");
            }
        }

        private static bool TryParseAuthHeader(NameValueCollection headers, NetworkCredential _credentials)
        {
            bool result = false;

            string authHeader = headers["Authorization"];

            if(!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Basic", StringComparison.OrdinalIgnoreCase))
            {
                string base64Credentials = authHeader.Substring(6);
                string[] credentials = Encoding.ASCII.GetString(Convert.FromBase64String(base64Credentials)).Split(new char[] { ':' });
                
                if(credentials.Length == 2) 
                {
                    _credentials.UserName = credentials[0];
                    _credentials.Password = credentials[1];

                    result = true;
                } 
            }

            return result;
        }

        public ICredentials GetCredentials()
        {
            return _credentials;
        }
    }
}