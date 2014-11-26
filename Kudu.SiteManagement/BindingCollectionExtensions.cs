using Microsoft.Web.Administration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IIS = Microsoft.Web.Administration;

namespace Kudu.SiteManagement
{
    public static class BindingCollectionExtensions
    {

        public static IIS.Site Add(this IIS.SiteCollection siteCollection, string name, string bindingInformation, string physicalPath, byte[] certificateHash, SslFlags sslFlags)
        {
            var site = siteCollection.Add(name, bindingInformation, physicalPath, certificateHash);

            SetSslFlags(site.Bindings.First(), sslFlags);

            return site;
        }

        public static IIS.Binding Add(this IIS.BindingCollection bindingCollection, string bindingInformation, byte[] certificateHash, string certificateStoreName, SslFlags sslFlags)
        {
            var binding = bindingCollection.Add(bindingInformation, certificateHash, certificateStoreName);

            SetSslFlags(binding, sslFlags);

            return binding;
        }

        private static void SetSslFlags(Binding binding, SslFlags sslFlags)
        {
            binding.SetAttributeValue("sslFlags", sslFlags);
        }

    }
}
