﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jhu.Graywulf.Sql.Parsing
{
    public partial class Statement
    {
        public IStatement SpecificStatement
        {
            get { return (IStatement)Stack.First.Value; }
        }
    }
}