﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jhu.Graywulf.Sql.Parsing
{
    public partial class WhileStatement
    {
        public BooleanExpression Condition
        {
            get
            {
                return FindDescendant<BooleanExpression>();
            }
        }

        public Statement Statement
        {
            get
            {
                return FindDescendant<AnyStatement>().SpecificStatement;
            }
        }

        public override IEnumerable<Statement> EnumerateSubStatements()
        {
            yield return Statement;
        }
    }
}
