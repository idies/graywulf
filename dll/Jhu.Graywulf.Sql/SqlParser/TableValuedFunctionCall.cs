﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jhu.Graywulf.ParserLib;

namespace Jhu.Graywulf.SqlParser
{
    public partial class TableValuedFunctionCall : ITableReference
    {
        private TableReference tableReference;

        public TableReference TableReference
        {
            get { return tableReference; }
            set { tableReference = value; }
        }

        public UdfIdentifier UdfIdentifier
        {
            get
            {
                var fi = FindDescendant<FunctionIdentifier>();
                var udfi = fi.FindDescendant<UdfIdentifier>();

                return udfi;
            }
        }

        public bool IsUdf
        {
            get { return UdfIdentifier != null; }
        }

        protected override void OnInitializeMembers()
        {
            base.OnInitializeMembers();

            this.tableReference = null;
        }

        protected override void OnCopyMembers(object other)
        {
            base.OnCopyMembers(other);

            var old = (TableValuedFunctionCall)other;

            this.tableReference = old.tableReference;
        }

        public override void Interpret()
        {
            base.Interpret();

            if (IsUdf)
            {
                this.tableReference = new TableReference(this);
            }
        }
    }
}
