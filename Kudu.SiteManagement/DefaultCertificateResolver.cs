using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.SiteManagement
{
    public class DefaultCertificateResolver : ICertificateResolver
    {
        public X509Certificate2 LookupX509Certificate2(string certificateName, string storeName = "My")
        {
            using (var searcher = new X509StoreSearcher(storeName, StoreLocation.LocalMachine))
            {
                return searcher.Find(certificateName);
            }
        }
    }
}
