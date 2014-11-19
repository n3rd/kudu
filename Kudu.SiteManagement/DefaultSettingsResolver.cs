using System;
using System.Configuration;
using System.IO;

namespace Kudu.SiteManagement
{
    public class DefaultSettingsResolver : ISettingsResolver
    {
        private readonly string _sitesBaseUrl;
        private readonly string _serviceSitesBaseUrl;
        private readonly bool _customHostNames;
        private readonly bool _serviceSiteBasicAuth;

        public DefaultSettingsResolver()
            : this(sitesBaseUrl: null, serviceSitesBaseUrl: null, enableCustomHostNames: null, enableServiceSiteBasicAuth: null)
        {
        }

        public DefaultSettingsResolver(string sitesBaseUrl, string serviceSitesBaseUrl, string enableCustomHostNames, string enableServiceSiteBasicAuth)
        {
            // Ensure the base url is normalised to not have a leading dot,
            // we will add this on later when joining the application name up
            if (sitesBaseUrl != null)
            {
                _sitesBaseUrl = sitesBaseUrl.TrimStart('.');
            }
            if (serviceSitesBaseUrl != null)
            {
                _serviceSitesBaseUrl = serviceSitesBaseUrl.TrimStart('.');
            }

            if (!String.IsNullOrEmpty(_serviceSitesBaseUrl) && !String.IsNullOrEmpty(_sitesBaseUrl))
            {
                if (_serviceSitesBaseUrl.Equals(_sitesBaseUrl, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("serviceSitesBaseUrl cannot be the same as sitesBaseUrl.");
                }
            }

            if (enableCustomHostNames == null || !Boolean.TryParse(enableCustomHostNames, out _customHostNames))
            {
                _customHostNames = false;
            }

            if (enableServiceSiteBasicAuth == null || !Boolean.TryParse(enableServiceSiteBasicAuth, out _serviceSiteBasicAuth))
            {
                _serviceSiteBasicAuth = false;
            }
        }

        public string SitesBaseUrl
        {
            get
            {
                return _sitesBaseUrl;
            }
        }

        public string ServiceSitesBaseUrl
        {
            get
            {
                return _serviceSitesBaseUrl;
            }            
        }

        public bool CustomHostNames
        {
            get
            {
                return _customHostNames;
            }
        }

        public bool ServiceSiteBasicAuth
        {
            get
            {
                return _serviceSiteBasicAuth;
            }
        }
    }
}
