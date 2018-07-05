﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jhu.Graywulf.Parsing;
using Jhu.Graywulf.Sql.Schema;
using Jhu.Graywulf.Sql.Parsing;

namespace Jhu.Graywulf.Sql.NameResolution
{
    public class ColumnReference : ReferenceBase
    {
        #region Private member variables

        private TableReference parentTableReference;
        private DataTypeReference parentDataTypeReference;

        private string columnName;
        private string columnAlias;
        private DataTypeReference dataTypeReference;

        private bool isStar;
        private bool isMultiPartIdentifier;
        private bool isComplexExpression;
        private int selectListIndex;

        private ColumnContext columnContext;

        #endregion
        #region Properties

        /// <summary>
        /// Gets or sets the reference to the table defining the column
        /// </summary>
        public TableReference ParentTableReference
        {
            get { return parentTableReference; }
            set { parentTableReference = value; }
        }

        /// <summary>
        /// Gets or sets the reference to the data type defining the column
        /// </summary>
        public DataTypeReference ParentDataTypeReference
        {
            get { return parentDataTypeReference; }
            set { parentDataTypeReference = value; }
        }

        public string ColumnName
        {
            get { return columnName; }
            set { columnName = value; }
        }

        public string ColumnAlias
        {
            get { return columnAlias; }
            set { columnAlias = value; }
        }

        public DataTypeReference DataTypeReference
        {
            get { return dataTypeReference; }
            set { dataTypeReference = value; }
        }

        public bool IsStar
        {
            get { return isStar; }
            set { isStar = value; }
        }

        public bool IsMultiPartIdentifier
        {
            get { return isMultiPartIdentifier; }
            set { isMultiPartIdentifier = value; }
        }

        public bool IsComplexExpression
        {
            get { return isComplexExpression; }
        }

        public int SelectListIndex
        {
            get { return selectListIndex; }
            set { selectListIndex = value; }
        }
        
        public ColumnContext ColumnContext
        {
            get { return columnContext; }
            set { columnContext = value; }
        }

        public bool IsReferenced
        {
            get
            {
                return columnContext != 0;
            }
        }

        public override string UniqueName
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        #endregion
        #region Constructors and initializers

        public ColumnReference()
        {
            InitializeMembers();
        }

        public ColumnReference(Node node)
            : base(node)
        {
        }

        public ColumnReference(ColumnReference old)
        {
            CopyMembers(old);
        }

        public ColumnReference(TableReference parentTableReference, ColumnReference old)
        {
            CopyMembers(old);

            this.parentTableReference = parentTableReference;
        }

        public ColumnReference(TableReference parentTableReference, DataTypeReference parentDataTypeReference, ColumnReference old, DataTypeReference dataTypeReference)
        {
            CopyMembers(old);

            this.parentTableReference = parentTableReference;
            this.parentDataTypeReference = parentDataTypeReference;
            this.dataTypeReference = dataTypeReference;
        }

        public ColumnReference(DataTypeReference parentDataTypeReference, ColumnReference old)
        {
            CopyMembers(old);

            this.parentDataTypeReference = parentDataTypeReference;
        }

        public ColumnReference(Node node, TableReference tableReference, string columnName, DataTypeReference dataTypeReference)
            :base(node)
        {
            InitializeMembers();

            this.parentTableReference = tableReference;
            this.dataTypeReference = dataTypeReference;
            this.columnName = columnName;
        }

        public ColumnReference(Column column, TableReference tableReference, DataTypeReference dataTypeReference)
        {
            InitializeMembers();

            this.parentTableReference = tableReference;
            this.dataTypeReference = dataTypeReference;

            this.columnName = column.Name;

            if (column.IsKey)
            {
                this.columnContext |= ColumnContext.PrimaryKey;
            }

            if (column.IsIdentity)
            {
                this.columnContext |= ColumnContext.Identity;
            }

            // TODO: copy metadata here
        }

        private void InitializeMembers()
        {
            this.parentTableReference = null;
            this.parentDataTypeReference = null;

            this.columnName = null;
            this.columnAlias = null;
            this.dataTypeReference = null;

            this.isStar = false;
            this.isMultiPartIdentifier = false;
            this.isComplexExpression = false;
            this.selectListIndex = -1;

            this.columnContext = ColumnContext.None;
        }

        private void CopyMembers(ColumnReference old)
        {
            this.parentTableReference = old.parentTableReference;
            this.parentDataTypeReference = old.parentDataTypeReference;

            this.columnName = old.columnName;
            this.columnAlias = old.columnAlias;
            this.dataTypeReference = old.dataTypeReference;

            this.isStar = old.isStar;
            this.isMultiPartIdentifier = old.isMultiPartIdentifier;
            this.isComplexExpression = old.isComplexExpression;
            this.selectListIndex = old.selectListIndex;

            this.columnContext = old.columnContext;
        }

        public override object Clone()
        {
            return new ColumnReference(this);
        }

        #endregion

        public static ColumnReference CreateStar()
        {
            var cr = new ColumnReference()
            {
                IsStar = true,
                ColumnName = "*",
                parentTableReference = new TableReference()
            };

            return cr;
        }

        public static ColumnReference CreateStar(TableReference tableReference)
        {
            var cr = CreateStar();
            cr.parentTableReference = tableReference;

            return cr;
        }

        public static ColumnReference Interpret(ColumnIdentifier ci)
        {
            // Depending on the number of parts, the column identifier can be
            // first, second or third; all the rest is property access of UDT columns
            var mpi = ci.FindDescendant<MultiPartIdentifier>();
            var cr = new ColumnReference(ci)
            {
                parentTableReference = new TableReference(),
                isMultiPartIdentifier = true
            };

            return cr;
        }

        public static ColumnReference Interpret(StarColumnIdentifier ci)
        {
            var ti = ci.TableOrViewIdentifier;
            var cr = new ColumnReference(ci)
            {
                ParentTableReference = ti?.TableReference ?? new TableReference(),
                isStar = true,
                columnName = "*"
            };

            return cr;
        }

        public static ColumnReference Interpret(ColumnDefinition cd)
        {
            var cr = new ColumnReference(
                cd,
                null,
                Util.RemoveIdentifierQuotes(cd.ColumnName.Value),
                cd.DataTypeIdentifier.DataTypeReference);

            return cr;
        }

        public static ColumnReference Interpret(IndexColumnDefinition ic)
        {
            var cr = new ColumnReference(ic)
            {
                columnName = Util.RemoveIdentifierQuotes(ic.ColumnName.Value)
            };

            return cr;
        }

        public static ColumnReference Interpret(IncludedColumnDefinition ic)
        {
            var cr = new ColumnReference(ic)
            {
                columnName = Util.RemoveIdentifierQuotes(ic.ColumnName.Value)
            };

            return cr;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ce"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static ColumnReference Interpret(ColumnExpression ce)
        {
            ColumnReference cr;
            var exp = ce.Expression;
            var star = ce.StarColumnIdentifier;
            var alias = ce.ColumnAlias;

            if (exp != null)
            {
                if (exp.IsSingleColumn)
                {
                    var ci = exp.FindDescendantRecursive<ColumnIdentifier>();
                    cr = new ColumnReference(ci.ColumnReference);
                }
                else
                {
                    cr = new ColumnReference(ce)
                    {
                        isComplexExpression = true
                    };
                }
            }
            else
            {
                cr = new ColumnReference(star.ColumnReference);
            }

            if (alias != null)
            {
                cr.columnAlias = Util.RemoveIdentifierQuotes(alias.Value);
            }

            return cr;
        }

        public static ColumnReference Interpret(UpdateSetMutator usm)
        {
            var cr = new ColumnReference()
            {
                columnName = usm.ColumnName.Value,
                parentTableReference = new TableReference(),
            };

            return cr;
        }

        public bool Compare(ColumnReference other)
        {
            // other must be a direct column reference, ie having a TableReference set
            // or must be a complex expression with an alias set
            if (other.parentTableReference == null && !other.isComplexExpression)
            {
                throw new InvalidOperationException();
            }

            bool res = true;

            if ((this.parentTableReference == null || this.parentTableReference.IsUndefined) && !this.isComplexExpression)
            {
                // No table is specified, only compare by column name
                res &= this.CompareByName(other);
            }
            else if (other.parentTableReference == null || other.parentTableReference.IsUndefined)
            {
                // TODO: verify if this can happen
                // if this is an alias
                res &= this.parentTableReference == null && SchemaManager.Comparer.Compare(this.columnName, other.columnAlias) == 0;
            }
            else
            {
                // Now both have the table reference set, make sure they are equal

                // compare the two table references
                res &= (this.parentTableReference.Compare(other.parentTableReference));

                // compare the two names
                res &= this.CompareByName(other);
            }

            return res;
        }

        private bool CompareByName(ColumnReference other)
        {
            // If the other column is aliased, always compare by alias, otherwise fall back to compare by name
            if (other.columnAlias != null)
            {
                return SchemaManager.Comparer.Compare(this.columnName, other.columnAlias) == 0;
            }
            else
            {
                return SchemaManager.Comparer.Compare(this.columnName, other.columnName) == 0;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Never use this in query generation!
        /// </remarks>
        public override string ToString()
        {
            var res = String.Empty;

            if (parentTableReference != null && !ParentTableReference.IsUndefined)
            {
                res += parentTableReference.ToString();
                res += ".";
            }

            if (isStar)
            {
                res += "*";
            }
            else
            {
                res += String.Format("[{0}]", columnName);
            }

            if (columnAlias != null)
            {
                res += String.Format(" AS [{0}]", columnAlias);
            }

            return res;
        }
    }
}
