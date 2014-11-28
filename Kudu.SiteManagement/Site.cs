using System;
using System.Collections.Generic;
using System.Linq;

namespace Kudu.SiteManagement
{
    public class Site
    {
        public Site()
        {
            SiteUrls = new List<Uri>();
        }
        public Uri ServiceUrl
        {
            get
            {
                return ServiceUrls.FirstOrDefault();
            }
        }
        public Uri SiteUrl
        {
            get
            {
                return SiteUrls.FirstOrDefault();
            }
        }
        public IList<Uri> SiteUrls { get; set; }
        public IList<Uri> ServiceUrls { get; set; }
    }
}