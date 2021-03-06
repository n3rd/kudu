﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Kudu.SiteManagement;
using System;

namespace Kudu.Web.Models
{
    public class ApplicationViewModel
    {
        public ApplicationViewModel()
        {
        }

        public ApplicationViewModel(IApplication application, ISettingsResolver settingsResolver)
        {
            Name = application.Name;
            SiteUrl = application.SiteUrl.ToString();
            SiteUrls = application.SiteUrls;
            ServiceUrl = application.ServiceUrl.ToString();
            ServiceUrls = application.ServiceUrls;
            CustomHostNames = settingsResolver.CustomHostNames;
        }

        [Required]
        public string Name { get; set; }
        public string SiteUrl { get; set; }
        public IEnumerable<Uri> SiteUrls { get; set; }
        public string ServiceUrl { get; set; }
        public IEnumerable<Uri> ServiceUrls { get; set; }
        public bool CustomHostNames { get; private set; }
        public RepositoryInfo RepositoryInfo { get; set; }
        
        public string GitUrl
        {
            get
            {
                if (RepositoryInfo == null)
                {
                    return null;
                }
                return RepositoryInfo.GitUrl.ToString();
            }
        }

        public IList<DeployResult> Deployments { get; set; }

        public string CloneUrl
        {
            get
            {
                if (RepositoryInfo == null)
                {
                    return null;
                }

                switch (RepositoryInfo.Type)
                {
                    case RepositoryType.Git:
                        return GitUrl;
                }
                return null;
            }
        }
    }
}