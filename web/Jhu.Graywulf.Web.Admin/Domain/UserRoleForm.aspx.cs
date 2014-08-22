﻿using System;
using System.Collections;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Xml.Linq;
using Jhu.Graywulf.Registry;

namespace Jhu.Graywulf.Web.Admin.Domain
{
    public partial class UserRoleForm : EntityFormPageBase<UserRole>
    {
        protected override void OnUpdateForm()
        {
            base.OnUpdateForm();

            Default.Checked = Item.Default;
        }

        protected override void OnSaveForm()
        {
            base.OnSaveForm();

            Item.Default = Default.Checked;
        }
    }
}