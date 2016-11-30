﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Jhu.Graywulf.Web.Api.V1;

namespace Jhu.Graywulf.Web.UI.Apps.Jobs
{
    public partial class ImportForm : FederationUserControlBase
    {
        private ImportJob job;

        public ImportJob Job
        {
            get { return job; }
            set
            {
                job = value;
                UpdateForm();
            }
        }

        public void UpdateForm()
        {
            dataset.Text = job.Destination.Dataset;
            table.Text = job.Destination.Table;

            if (job.Uri != null)
            {
                uri.Text = job.Uri.ToString();
            }

            if (job.FileFormat != null && job.FileFormat.MimeType != null)
            {
                fileFormat.Text = job.FileFormat.MimeType;
            }
        }
    }
}