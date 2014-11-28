﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Client.Deployment;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Core.Infrastructure;
using Microsoft.Web.Administration;
using IIS = Microsoft.Web.Administration;
using System.Text;

namespace Kudu.SiteManagement
{
    public class SiteManager : ISiteManager
    {
        private const string HostingStartHtml = "hostingstart.html";

        private readonly static Random portNumberGenRnd = new Random((int)DateTime.UtcNow.Ticks);

        private readonly IPathResolver _pathResolver;
        private readonly bool _traceFailedRequests;
        private readonly string _logPath;
        private readonly ISettingsResolver _settingsResolver;
        private readonly ICertificateResolver _certificateResolver;

        public SiteManager(IPathResolver pathResolver, ISettingsResolver settingsResolver, ICertificateResolver certificateResolver)
            : this(pathResolver, traceFailedRequests: false, logPath: null, settingsResolver: settingsResolver, certificateResolver: certificateResolver)
        {
        }

        public SiteManager(IPathResolver pathResolver, bool traceFailedRequests, string logPath, ISettingsResolver settingsResolver, ICertificateResolver certificateResolver)
        {
            _logPath = logPath;
            _pathResolver = pathResolver;
            _traceFailedRequests = traceFailedRequests;
            _settingsResolver = settingsResolver;
            _certificateResolver = certificateResolver;
        }

        public IEnumerable<string> GetSites()
        {
            using (var iis = GetServerManager())
            {
                // The app pool is the app name
                return iis.Sites.Where(x => x.Name.StartsWith("kudu_", StringComparison.OrdinalIgnoreCase))
                                .Select(x => x.Applications[0].ApplicationPoolName)
                                .Distinct()
                                .ToList();
            }
        }

        public Site GetSite(string applicationName)
        {
            using (var iis = GetServerManager())
            {
                var mainSiteName = GetLiveSite(applicationName);
                var serviceSiteName = GetServiceSite(applicationName);

                IIS.Site mainSite = iis.Sites[mainSiteName];

                if (mainSite == null)
                {
                    return null;
                }

                IIS.Site serviceSite = iis.Sites[serviceSiteName];
                // IIS.Site devSite = iis.Sites[devSiteName];

                var site = new Site();
                site.ServiceUrls = GetSiteUrls(serviceSite);
                site.SiteUrls = GetSiteUrls(mainSite);
                return site;
            }
        }

        private static List<Uri> GetSiteUrls(IIS.Site site)
        {
            var urls = new List<Uri>();

            if (site == null)
            {
                return null;
            }

            foreach (IIS.Binding binding in site.Bindings)
            {
                var builder = new UriBuilder
                {
                    Host = String.IsNullOrEmpty(binding.Host) ? "localhost" : binding.Host,
                    Scheme = binding.Protocol,
                    Port = binding.EndPoint.Port == 80 ? -1 : binding.EndPoint.Port
                };

                urls.Add(builder.Uri);
            }

            return urls;
        }

        public async Task<Site> CreateSiteAsync(string applicationName, ICredentials credentials = null)
        {
            using (var iis = GetServerManager())
            {
                try
                {
                    // Determine the host header values
                    List<string> siteBindings = GetDefaultBindings(applicationName, _settingsResolver.SitesBaseUrl);
                    List<string> serviceSiteBindings = GetDefaultBindings(applicationName, _settingsResolver.ServiceSitesBaseUrl);

                    // Create the service site for this site
                    string serviceSiteName = GetServiceSite(applicationName);
                    var serviceSite = CreateSiteAsync(iis, applicationName, serviceSiteName, _pathResolver.ServiceSitePath, serviceSiteBindings, _settingsResolver.ServiceSiteBasicAuth, "kudu_service");

                    // Create the main site
                    string siteName = GetLiveSite(applicationName);
                    string root = _pathResolver.GetApplicationPath(applicationName);
                    string siteRoot = _pathResolver.GetLiveSitePath(applicationName);
                    string webRoot = Path.Combine(siteRoot, Constants.WebRoot);

                    FileSystemHelpers.EnsureDirectory(webRoot);
                    File.WriteAllText(Path.Combine(webRoot, HostingStartHtml), @"<html> 
<head>
<title>This web site has been successfully created</title>
<style type=""text/css"">
 BODY { color: #444444; background-color: #E5F2FF; font-family: verdana; margin: 0px; text-align: center; margin-top: 100px; }
 H1 { font-size: 16pt; margin-bottom: 4px; }
</style>
</head>
<body>
<h1>This web site has been successfully created</h1><br/>
</body> 
</html>");

                    var site = CreateSiteAsync(iis, applicationName, siteName, webRoot, siteBindings);

                    // Map a path called _app to the site root under the service site
                    MapServiceSitePath(iis, applicationName, Constants.MappedSite, root);

                    // Commit the changes to iis
                    iis.CommitChanges();

                    var serviceUrls = new List<Uri>();
                    foreach (var url in serviceSite.Bindings)
                    {
                        var builder = new UriBuilder
                        {
                            Host = String.IsNullOrEmpty(url.Host) ? "localhost" : url.Host,
                            Scheme = url.Protocol,
                            Port = url.EndPoint.Port == 80 ? -1 : url.EndPoint.Port
                        };

                        serviceUrls.Add(builder.Uri);
                    }

                    // Wait for the site to start
                    await OperationManager.AttemptAsync(() => WaitForSiteAsync(serviceUrls[0].ToString(), credentials));

                    // Set initial ScmType state to LocalGit
                    var settings = new RemoteDeploymentSettingsManager(serviceUrls.First() + "api/settings", credentials: credentials);
                    await settings.SetValue(SettingsKeys.ScmType, ScmType.LocalGit);

                    var siteUrls = new List<Uri>();
                    foreach (var url in site.Bindings)
                    {
                        var builder = new UriBuilder
                        {
                            Host = String.IsNullOrEmpty(url.Host) ? "localhost" : url.Host,
                            Scheme = url.Protocol,
                            Port = url.EndPoint.Port == 80 ? -1 : url.EndPoint.Port
                        };

                        siteUrls.Add(builder.Uri);
                    }

                    return new Site
                    {
                        ServiceUrls = serviceUrls,
                        SiteUrls = siteUrls
                    };
                }
                catch
                {
                    try
                    {
                        DeleteSiteAsync(applicationName).Wait();
                    }
                    catch
                    {
                        // Don't let it throw if we're unable to delete a failed creation.
                    }
                    throw;
                }
            }
        }

        public async Task DeleteSiteAsync(string applicationName)
        {
            using (var iis = GetServerManager())
            {
                // Get the app pool for this application
                string appPoolName = GetAppPool(applicationName);
                IIS.ApplicationPool kuduPool = iis.ApplicationPools[appPoolName];

                if (kuduPool == null)
                {
                    // If there's no app pool then do nothing
                    return;
                }

                await Task.WhenAll(
                    DeleteSiteAsync(iis, GetLiveSite(applicationName)),
                    // Don't delete the physical files for the service site
                    DeleteSiteAsync(iis, GetServiceSite(applicationName), deletePhysicalFiles: false)
                );

                iis.CommitChanges();

                string appPath = _pathResolver.GetApplicationPath(applicationName);
                var sitePath = _pathResolver.GetLiveSitePath(applicationName);

                try
                {
                    DeleteSafe(sitePath);
                    DeleteSafe(appPath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }
                finally
                {
                    // Remove the app pool and commit changes
                    iis.ApplicationPools.Remove(iis.ApplicationPools[appPoolName]);
                    iis.CommitChanges();

                    // Clear out the app pool user profile directory if it exists
                    string userDir = Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd(Path.DirectorySeparatorChar));
                    string appPoolDirectory = Path.Combine(userDir, appPoolName);
                    DeleteSafe(appPoolDirectory);
                }
            }
        }

        private static string NormalizeBinding(string binding)
        {
            string normalizedBinding;

            //Note: Seems like http and https is the two IIS allows when adding bindings to a site, nothing else.
            if (binding.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || binding.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                normalizedBinding = binding;
            }
            else
            {
                normalizedBinding = "http://" + binding;
            }

            return normalizedBinding;
        }

        public bool AddSiteBinding(string applicationName, string siteBinding, string siteCertificate, SiteType siteType)
        {
            IIS.Site site;

            var uri = new Uri(NormalizeBinding(siteBinding));

            try
            {
                using (var iis = GetServerManager())
                {
                    if (!IsAvailable(uri.Host, uri.Port, iis))
                    {
                        return false;
                    }

                    if (siteType == SiteType.Live)
                    {
                        site = iis.Sites[GetLiveSite(applicationName)];
                    }
                    else
                    {
                        site = iis.Sites[GetServiceSite(applicationName)];
                    }

                    if (site != null)
                    {
                        Certificate certificate = null;

                        if (!string.IsNullOrEmpty(siteCertificate))
                        {
                            certificate = _certificateResolver.FindByTumbprint(siteCertificate);
                        }

                        if ("http".Equals(uri.Scheme, StringComparison.OrdinalIgnoreCase)
                            || certificate == null)
                        {
                            site.Bindings.Add("*:" + uri.Port + ":" + uri.Host, "http");
                        }
                        else
                        {
                            site.Bindings.Add("*:" + uri.Port + ":" + uri.Host, certificate.Hash, "My", SslFlags.Sni); 
                        }

                        iis.CommitChanges();

                        Thread.Sleep(1000);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool RemoveSiteBinding(string applicationName, string siteBinding, SiteType siteType)
        {
            IIS.Site site;

            try
            {
                using (var iis = GetServerManager())
                {
                    if (siteType == SiteType.Live)
                    {
                        site = iis.Sites[GetLiveSite(applicationName)];
                    }
                    else
                    {
                        site = iis.Sites[GetServiceSite(applicationName)];
                    }

                    if (site != null)
                    {
                        var uri = new Uri(siteBinding);
                        var binding = site.Bindings.FirstOrDefault(x => x.Host.Equals(uri.Host)
                                && x.EndPoint.Port.Equals(uri.Port)
                                && x.Protocol.Equals(uri.Scheme));

                        if (binding != null)
                        {
                            site.Bindings.Remove(binding);
                            iis.CommitChanges();

                            Thread.Sleep(1000);
                        }
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void MapServiceSitePath(IIS.ServerManager iis, string applicationName, string path, string siteRoot)
        {
            string serviceSiteName = GetServiceSite(applicationName);

            // Get the service site
            IIS.Site site = iis.Sites[serviceSiteName];
            if (site == null)
            {
                throw new InvalidOperationException("Could not retrieve service site");
            }

            // Map the path to the live site in the service site
            site.Applications.Add(path, siteRoot);
        }

        private static IIS.ApplicationPool EnsureAppPool(IIS.ServerManager iis, string appName)
        {
            string appPoolName = GetAppPool(appName);
            var kuduAppPool = iis.ApplicationPools[appPoolName];
            if (kuduAppPool == null)
            {
                iis.ApplicationPools.Add(appPoolName);
                iis.CommitChanges();
                kuduAppPool = iis.ApplicationPools[appPoolName];
                kuduAppPool.ManagedPipelineMode = IIS.ManagedPipelineMode.Integrated;
                kuduAppPool.ManagedRuntimeVersion = "v4.0";
                kuduAppPool.AutoStart = true;
                kuduAppPool.ProcessModel.LoadUserProfile = true;
            }

            EnsureDefaultDocument(iis);

            return kuduAppPool;
        }

        private static List<String> GetDefaultBindings(string applicationName, string baseUrl)
        {
            var siteBindings = new List<string>();
            if (!String.IsNullOrWhiteSpace(baseUrl))
            {
                string binding = CreateBindingInformation(applicationName, baseUrl);
                siteBindings.Add(binding);
            }
            return siteBindings;
        }

        private static int GetRandomPort(IIS.ServerManager iis)
        {
            int randomPort = portNumberGenRnd.Next(1025, 65535);
            while (!IsAvailable(randomPort, iis))
            {
                randomPort = portNumberGenRnd.Next(1025, 65535);
            }

            return randomPort;
        }

        private static bool IsAvailable(int port, IIS.ServerManager iis)
        {
            var tcpConnections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
            foreach (var connectionInfo in tcpConnections)
            {
                if (connectionInfo.LocalEndPoint.Port == port)
                {
                    return false;
                }
            }

            foreach (var iisSite in iis.Sites)
            {
                foreach (var binding in iisSite.Bindings)
                {
                    if (binding.EndPoint != null && binding.EndPoint.Port == port)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsAvailable(string host, int port, IIS.ServerManager iis)
        {
            foreach (var iisSite in iis.Sites)
            {
                foreach (var binding in iisSite.Bindings)
                {
                    if (binding.EndPoint != null && binding.EndPoint.Port == port && binding.Host == host)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private IIS.Site CreateSiteAsync(IIS.ServerManager iis, string applicationName, string siteName, string siteRoot, List<string> siteBindings, bool protectSite = false, string certificateName = null)
        {
            var pool = EnsureAppPool(iis, applicationName);

            IIS.Site site;

            if (siteBindings != null && siteBindings.Count > 0)
            {
                Certificate certificate = null;

                if (!string.IsNullOrEmpty(certificateName))
                {
                    certificate = _certificateResolver.FindByFriendlyName(certificateName);
                }

                if (certificate == null)
                {
                    site = iis.Sites.Add(siteName, "http", siteBindings.First(), siteRoot);
                }
                else
                {
                    site = iis.Sites.Add(siteName, siteBindings.First(), siteRoot, certificate.Hash, SslFlags.Sni);
                }
            }
            else
            {
                int sitePort = GetRandomPort(iis);
                site = iis.Sites.Add(siteName, siteRoot, sitePort);
            }

            site.ApplicationDefaults.ApplicationPoolName = pool.Name;

            if (protectSite)
            {
                ProtectSite(iis, siteName);
            }

            if (_traceFailedRequests)
            {
                site.TraceFailedRequestsLogging.Enabled = true;
                string path = Path.Combine(_logPath, applicationName, "Logs");
                Directory.CreateDirectory(path);
                site.TraceFailedRequestsLogging.Directory = path;
            }

            return site;
        }

        private void ProtectSite(IIS.ServerManager iis, string siteName)
        {
            var config = iis.GetApplicationHostConfiguration();

            var anonymousAuthSection = config.GetSection("system.webServer/security/authentication/anonymousAuthentication", siteName);
            anonymousAuthSection.SetAttributeValue("enabled", false);

            var basicAuthSection = config.GetSection("system.webServer/security/authentication/basicAuthentication", siteName);
            basicAuthSection.SetAttributeValue("enabled", true);
        }

        private static void EnsureDefaultDocument(IIS.ServerManager iis)
        {
            Configuration applicationHostConfiguration = iis.GetApplicationHostConfiguration();
            ConfigurationSection defaultDocumentSection = applicationHostConfiguration.GetSection("system.webServer/defaultDocument");

            ConfigurationElementCollection filesCollection = defaultDocumentSection.GetCollection("files");

            if (!filesCollection.Any(ConfigurationElementContainsHostingStart))
            {
                ConfigurationElement addElement = filesCollection.CreateElement("add");

                addElement["value"] = HostingStartHtml;
                filesCollection.Add(addElement);

                iis.CommitChanges();
            }
        }

        private static bool ConfigurationElementContainsHostingStart(ConfigurationElement configurationElement)
        {
            object valueAttribute = configurationElement["value"];

            return valueAttribute != null && String.Equals(HostingStartHtml, valueAttribute.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private static string CreateBindingInformation(string applicationName, string baseUrl, string defaultIp = "*", string defaultPort = "80")
        {
            // Creates the 'bindingInformation' parameter for IIS.ServerManager.Sites.Add()
            // Accepts baseUrl in 3 formats: hostname, hostname:port and ip:port:hostname

            // Based on the default parameters, applicationName + baseUrl it creates
            // a string in the format ip:port:hostname

            string[] parts = baseUrl.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            string ip = defaultIp;
            string host = string.Empty;
            string port = defaultPort;

            switch (parts.Length)
            {
                case 1: // kudu.mydomain
                    host = parts[0];
                    break;
                case 2: // kudu.mydomain:8080
                    host = parts[0];
                    port = parts[1];
                    break;
                case 3: // 192.168.100.3:80:kudu.mydomain
                    ip = parts[0];
                    port = parts[1];
                    host = parts[2];
                    break;
            }

            return String.Format("{0}:{1}:{2}", ip, port, applicationName + "." + host);
        }

        private static Task DeleteSiteAsync(IIS.ServerManager iis, string siteName, bool deletePhysicalFiles = true)
        {
            var site = iis.Sites[siteName];
            if (site != null)
            {
                return OperationManager.AttemptAsync(async () =>
                {
                    await Task.Run(() =>
                    {
                        if (deletePhysicalFiles)
                        {
                            string physicalPath = site.Applications[0].VirtualDirectories[0].PhysicalPath;
                            DeleteSafe(physicalPath);
                        }
                        iis.Sites.Remove(site);
                    });
                });
            }

            return Task.FromResult(0);
        }

        private static string GetLiveSite(string applicationName)
        {
            return "kudu_" + applicationName;
        }

        private static string GetServiceSite(string applicationName)
        {
            return "kudu_service_" + applicationName;
        }

        private static string GetAppPool(string applicationName)
        {
            return applicationName;
        }

        private static void DeleteSafe(string physicalPath)
        {
            if (!Directory.Exists(physicalPath))
            {
                return;
            }

            FileSystemHelpers.DeleteDirectorySafe(physicalPath);
        }

        private static ServerManager GetServerManager()
        {
            return new IIS.ServerManager(Environment.ExpandEnvironmentVariables("%windir%\\system32\\inetsrv\\config\\applicationHost.config"));
        }

        private static async Task WaitForSiteAsync(string serviceUrl, ICredentials credentials)
        {
            using (var client = HttpClientHelper.CreateClient(serviceUrl, credentials))
            {
                using (var response = await client.GetAsync(""))
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }
    }
}