﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Jhu.Graywulf.Sql.Parsing
{
    public partial class TableHint
    {
        public Identifier Identifier
        {
            get { return FindDescendant<Identifier>(); }
        }
    }
}