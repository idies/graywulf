﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jhu.Graywulf.Sql.Parsing
{
    public partial class StatementBlock : IStatement
    {
        public bool IsResolvable
        {
            get { return false; }
        }
        
        public IEnumerable<Statement> EnumerateSubStatements()
        {
            return EnumerateDescendants<Statement>(true);
        }
    }
}
