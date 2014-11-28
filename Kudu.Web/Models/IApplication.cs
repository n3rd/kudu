using System;
using System.Collections.Generic;

namespace Kudu.Web.Models
{
    public interface IApplication
    {
        string Name { get; set; }
        Uri ServiceUrl { get; }
        Uri SiteUrl { get; }
        IList<Uri> SiteUrls { get; set; }
        IList<Uri> ServiceUrls { get; set; }
    }
}
