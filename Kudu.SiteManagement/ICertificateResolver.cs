using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.SiteManagement
{
    public interface ICertificateResolver
    {
        Certificate FindByFriendlyName(string friendlyName, string storeName = "My");

        Certificate FindByTumbprint(string thumbprint, string storeName = "My");

        IEnumerable<Certificate> FindAll(string storeName = "My");
    }
}
