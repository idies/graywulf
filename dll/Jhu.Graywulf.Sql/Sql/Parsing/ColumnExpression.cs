﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jhu.Graywulf.Parsing;
using Jhu.Graywulf.Sql.NameResolution;

namespace Jhu.Graywulf.Sql.Parsing
{
    public partial class ColumnExpression : ITableReference, IColumnReference
    {
        private ColumnReference columnReference;

        public ColumnReference ColumnReference
        {
            get { return columnReference; }
            set { columnReference = value; }
        }

        public DatabaseObjectReference DatabaseObjectReference
        {
            get { return columnReference.ParentTableReference; }
            set { throw new InvalidOperationException(); }
        }

        /// <summary>
        /// Gets or sets the table reference associated with this column expression
        /// </summary>
        /// <remarks></remarks>
        public TableReference TableReference
        {
            get { return columnReference.ParentTableReference; }
            set { columnReference.ParentTableReference = value; }
        }

        public UserVariable AssignedVariable
        {
            get { return FindDescendant<UserVariable>(); }
        }

        public ColumnAlias ColumnAlias
        {
            get { return FindDescendant<ColumnAlias>(); }
        }

        public Expression Expression
        {
            get { return FindDescendant<Expression>(); }
        }

        protected override void OnInitializeMembers()
        {
            base.OnInitializeMembers();

            this.columnReference = null;
        }

        protected override void OnCopyMembers(object other)
        {
            base.OnCopyMembers(other);

            var old = (ColumnExpression)other;

            this.columnReference = old.columnReference;
        }

        public static ColumnExpression CreateStar()
        {
            var ci = ColumnIdentifier.CreateStar();
            var exp = Expression.Create(ci);
            var ce = Create(exp, null);

            ce.columnReference = ci.ColumnReference;

            return ce;
        }

        public static ColumnExpression CreateStar(TableReference tableReference)
        {
            var ci = ColumnIdentifier.CreateStar(tableReference);
            var exp = Expression.Create(ci);
            var ce = Create(exp, null);

            ce.columnReference = ci.ColumnReference;

            return ce;
        }

        public static ColumnExpression Create(Expression exp, string alias)
        {
            var ce = new ColumnExpression();
            ce.Stack.AddLast(exp);

            if (!String.IsNullOrWhiteSpace(alias))
            {
                ce.Stack.AddLast(Whitespace.Create());
                ce.Stack.AddLast(Keyword.Create("AS"));
                ce.Stack.AddLast(Whitespace.Create());
                ce.Stack.AddLast(ColumnAlias.Create(alias));
            }

            return ce;
        }

        public override void Interpret()
        {
            base.Interpret();

            this.columnReference = ColumnReference.Interpret(this);
        }
    }
}
