using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.SiteManagement
{
    [Flags]
    public enum SslFlags
    {
        None,
        Sni,
        CentralCertStore
    }
}
