using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Kudu.Web.Models
{
    public class Application : IApplication
    {
        public Application()
        {
            SiteUrls = new List<Uri>();
        }

        public string Name { get; set; }
        public Uri ServiceUrl
        {
            get
            {
                return ServiceUrls[0];
            }
        }
        public Uri SiteUrl
        {
            get
            {
                return SiteUrls[0];
            }
        }
        public IList<Uri> SiteUrls { get; set; }
        public IList<Uri> ServiceUrls { get; set; }
    }
}