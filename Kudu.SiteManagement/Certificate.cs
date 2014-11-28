using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.SiteManagement
{
    public class Certificate
    {

        public string Name { get; set; }

        public string Thumbprint { get; set; }

        public byte[] Hash { get; set; }

    }
}
