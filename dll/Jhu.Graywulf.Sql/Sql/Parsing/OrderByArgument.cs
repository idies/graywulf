﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jhu.Graywulf.Sql.Parsing
{
    public partial class OrderByArgument
    {
        public Expression Expression
        {
            get { return FindDescendant<Expression>(); }
        }
    }
}
