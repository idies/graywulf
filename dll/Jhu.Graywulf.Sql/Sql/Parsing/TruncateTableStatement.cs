﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jhu.Graywulf.Sql.Parsing
{
    public partial class TruncateTableStatement : IStatement
    {
        public bool IsResolvable
        {
            get { return true; }
        }

        public StatementType StatementType
        {
            get { return StatementType.Modify; }
        }

        public IEnumerable<Statement> EnumerateSubStatements()
        {
            yield break;
        }
    }
}
