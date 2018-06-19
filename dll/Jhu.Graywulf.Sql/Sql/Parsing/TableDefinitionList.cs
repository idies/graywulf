﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jhu.Graywulf.Sql.Parsing
{
    public partial class TableDefinitionList
    {
        public IEnumerable<TableDefinitionItem> EnumerateTableDefinitionItems()
        {
            return EnumerateDescendants<TableDefinitionItem>();
        }
    }
}