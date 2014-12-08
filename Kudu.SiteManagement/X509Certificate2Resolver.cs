using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.SiteManagement
{
    public class X509Certificate2Resolver : ICertificateResolver
    {
        public Certificate FindByFriendlyName(string friendlyName, string storeName = "My")
        {
            using (var searcher = new X509StoreSearcher(storeName, StoreLocation.LocalMachine))
            {
                return ToCertificate(searcher.FindByFriendlyName(friendlyName));
            }
        }

        public Certificate FindByTumbprint(string thumbprint, string storeName = "My")
        {
            using (var searcher = new X509StoreSearcher(storeName, StoreLocation.LocalMachine))
            {
                return ToCertificate(searcher.FindByThumbprint(thumbprint));
            }
        }

        public IEnumerable<Certificate> FindAll(string storeName = "My")
        {
            using (var searcher = new X509StoreSearcher(storeName, StoreLocation.LocalMachine))
            {
                return searcher.FindAll()
                               .Select(ToCertificate);
            }
        }

        private static Certificate ToCertificate(X509Certificate2 certificate)
        {
            return certificate == null ? null : new Certificate
            {
                Name = string.IsNullOrEmpty(certificate.FriendlyName) ? certificate.GetNameInfo(X509NameType.SimpleName, false) : certificate.FriendlyName,
                Thumbprint = certificate.Thumbprint,
                Hash = certificate.GetCertHash(),
            };
        }
    }
}
