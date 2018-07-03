﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jhu.Graywulf.Parsing;
using Jhu.Graywulf.Sql.Schema;
using Jhu.Graywulf.Sql.Parsing;

namespace Jhu.Graywulf.Sql.NameResolution
{
    public class TableReference : DatabaseObjectReference
    {
        #region Property storage variables

        private string alias;
        private string variableName;
        private TableContext tableContext;
        private bool isComputed;

        private VariableReference variableReference;
        private List<ColumnReference> columnReferences;

        #endregion
        #region Properties

        /// <summary>
        /// Gets or sets the resolved alias
        /// </summary>
        public string Alias
        {
            get { return alias; }
            set { alias = value; }
        }

        public string TableName
        {
            get { return DatabaseObjectName; }
            set { DatabaseObjectName = value; }
        }

        public string VariableName
        {
            get { return variableName; }
            set { variableName = value; }
        }

        public TableContext TableContext
        {
            get { return tableContext; }
            set { tableContext = value; }
        }

        public TableOrView TableOrView
        {
            get { return (TableOrView)DatabaseObject; }
        }

        /// <summary>
        /// Gets a value indicating whether the table source is computed by custom code.
        /// </summary>
        /// <remarks>
        /// This is an extension to traditional SQL queries to support tables that are
        /// calculated during multi-step execution, for instance the xmatch results table
        /// in sky-query.
        /// </remarks>
        public bool IsComputed
        {
            get { return isComputed; }
            set { isComputed = value; }
        }

        public bool IsCachable
        {
            get
            {
                return
                  !tableContext.HasFlag(TableContext.Subquery) &&
                  !tableContext.HasFlag(TableContext.CommonTable) &&
                  !tableContext.HasFlag(TableContext.UserDefinedFunction) &&
                  !tableContext.HasFlag(TableContext.Variable) &&
                  !tableContext.HasFlag(TableContext.CreateTable) &&
                  !tableContext.HasFlag(TableContext.Target) &&             // TODO: review this
                  !IsComputed;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// A table reference might be an alias if only the table name part is specified.
        /// </remarks>
        public bool IsPossiblyAlias
        {
            get
            {
                return (alias != null || DatabaseObjectName != null) && DatasetName == null && DatabaseName == null && VariableName == null;
            }
        }

        public override bool IsUndefined
        {
            get { return base.IsUndefined && alias == null && variableName == null; }
        }

        /// <summary>
        /// Gets the unique name of the table (alias, if available)
        /// </summary>
        /// <remarks>
        /// Never use this in query generation!
        /// </remarks>
        public override string UniqueName
        {
            get
            {
                // TODO: review this and make sure kez is unique even if table
                // is referenced deep down in CTEs

                if (!String.IsNullOrWhiteSpace(variableName))
                {
                    return String.Format("{0}", variableName);
                }
                else if (!String.IsNullOrWhiteSpace(alias))
                {
                    return String.Format("[{0}]", alias);
                }
                else
                {
                    return base.UniqueName;
                }
            }
        }

        /// <summary>
        /// Returns the exported name of a subquery or a table
        /// </summary>
        public string ExportedName
        {
            get
            {
                if (tableContext.HasFlag(TableContext.Subquery) ||
                    tableContext.HasFlag(TableContext.CommonTable) ||
                    tableContext.HasFlag(TableContext.UserDefinedFunction) ||
                    isComputed ||
                    alias != null)
                {
                    return alias;
                }
                else if (tableContext.HasFlag(TableContext.Variable))
                {
                    return variableName;
                }
                else
                {
                    // If no alias is used then use table name
                    // SQL Server doesn't allow two tables with the same name without alias
                    // so this behavior is fine
                    return DatabaseObjectName;
                }
            }
        }

        public VariableReference VariableReference
        {
            get { return variableReference; }
            set { variableReference = value; }
        }

        public List<ColumnReference> ColumnReferences
        {
            get { return columnReferences; }
        }

        #endregion
        #region Constructors and initializer

        public TableReference()
        {
            InitializeMembers();
        }

        public TableReference(Node node)
            : base(node)
        {
            InitializeMembers();
        }

        public TableReference(TableReference old)
            : base(old)
        {
            CopyMembers(old);
        }

        public TableReference(string alias)
        {
            throw new NotImplementedException();

            // TODO: review

            this.alias = alias;
            this.tableContext = TableContext.None;
            this.isComputed = false;
        }

        public TableReference(TableOrView table, string alias, bool copyColumns)
            : base(table)
        {
            this.alias = alias;
            this.tableContext = TableContext.None;
            this.isComputed = false;

            this.columnReferences = new List<ColumnReference>();

            if (copyColumns)
            {
                foreach (var c in table.Columns.Values)
                {
                    columnReferences.Add(new ColumnReference(c, this, new DataTypeReference(c.DataType)));
                }
            }
        }

        private void InitializeMembers()
        {
            this.alias = null;
            this.variableName = null;
            this.tableContext = TableContext.None;
            this.isComputed = false;

            this.variableReference = null;
            this.columnReferences = new List<ColumnReference>();
        }

        private void CopyMembers(TableReference old)
        {
            this.alias = old.alias;
            this.variableName = old.variableName;
            this.tableContext = old.tableContext;
            this.isComputed = old.isComputed;

            this.variableReference = old.variableReference;
            // Deep copy of column references
            this.columnReferences = new List<ColumnReference>();

            foreach (var cr in old.columnReferences)
            {
                var ncr = new ColumnReference(this, cr);
                this.columnReferences.Add(ncr);
            }
        }

        public override object Clone()
        {
            return new TableReference(this);
        }

        #endregion

        public static TableReference Interpret(FunctionTableSource ts)
        {
            var alias = ts.Alias;
            var fr = ts.FunctionReference;

            var tr = new TableReference(ts)
            {
                alias = Util.RemoveIdentifierQuotes(alias?.Value),
                DatasetName = fr.DatasetName,
                DatabaseName = fr.DatabaseName,
                SchemaName = fr.SchemaName,
                DatabaseObjectName = fr.DatabaseObjectName,
                tableContext = TableContext.UserDefinedFunction
            };

            // TODO: tvf calls can have and alias list

            return tr;
        }

        public static TableReference Interpret(SimpleTableSource ts)
        {
            var tr = ts.TableReference;
            var alias = ts.Alias;

            tr.alias = Util.RemoveIdentifierQuotes(alias?.Value);

            return tr;
        }

        public static TableReference Interpret(VariableTableSource ts)
        {
            var alias = ts.Alias;
            var variable = ts.Variable;

            var tr = new TableReference(ts)
            {
                alias = Util.RemoveIdentifierQuotes(alias?.Value),
                variableName = variable.VariableName,
                variableReference = variable.VariableReference,
                tableContext = TableContext.Variable
            };

            return tr;
        }

        public static TableReference Interpret(SubqueryTableSource ts)
        {
            var alias = ts.Alias;

            var tr = new TableReference(ts)
            {
                alias = Util.RemoveIdentifierQuotes(alias.Value),
                tableContext = TableContext.Subquery,
            };

            // TODO: is subquery parsed at this point? Copy columns now?

            return tr;
        }

        public static TableReference Interpret(CommonTableSpecification cts)
        {
            var alias = cts.Alias;
            var subquery = cts.Subquery;

            var tr = new TableReference(cts)
            {
                alias = Util.RemoveIdentifierQuotes(alias.Value),
                tableContext = TableContext.Subquery | TableContext.CommonTable,
            };

            // TODO: is subquery parsed at this point? Copy columns now?
            // What about column name aliases?

            return tr;
        }

        public static TableReference Interpret(TableOrViewIdentifier ti)
        {
            var ds = ti.FindDescendant<DatasetPrefix>();
            var fpi = ti.FindDescendant<FourPartIdentifier>();

            if (fpi.NamePart4 != null)
            {
                throw NameResolutionError.TableNameTooManyParts(ti);
            }

            var tr = new TableReference(ti)
            {
                DatasetName = Util.RemoveIdentifierQuotes(ds?.DatasetName),
                DatabaseName = Util.RemoveIdentifierQuotes(fpi.NamePart3),
                SchemaName = Util.RemoveIdentifierQuotes(fpi.NamePart2),
                DatabaseObjectName = Util.RemoveIdentifierQuotes(fpi.NamePart1),
                IsUserDefined = true,
                tableContext = TableContext.TableOrView
            };

            return tr;
        }

        public static TableReference Interpret(ColumnIdentifier ci, bool columnNameLast)
        {
            // At this point we have to make the assumption that the very last token
            // in the four part identifier is the column name. If it is a property
            // accessor of a CLR UDT, it will be handled by the name resolver.

            TableReference tr;
            var ds = ci.FindDescendant<DatasetPrefix>();
            var fpi = ci.FindDescendant<FourPartIdentifier>();

            if (columnNameLast)
            {
                tr = new TableReference(ci)
                {
                    DatasetName = Util.RemoveIdentifierQuotes(ds?.DatasetName),
                    DatabaseName = Util.RemoveIdentifierQuotes(fpi.NamePart4),
                    SchemaName = Util.RemoveIdentifierQuotes(fpi.NamePart3),
                    DatabaseObjectName = Util.RemoveIdentifierQuotes(fpi.NamePart2),
                };
            }
            else
            {
                tr = new TableReference(ci)
                {
                    DatasetName = Util.RemoveIdentifierQuotes(ds?.DatasetName),
                    DatabaseName = Util.RemoveIdentifierQuotes(fpi.NamePart3),
                    SchemaName = Util.RemoveIdentifierQuotes(fpi.NamePart2),
                    DatabaseObjectName = Util.RemoveIdentifierQuotes(fpi.NamePart1),
                };
            }

            tr.IsUserDefined = true;
            tr.tableContext |= TableContext.TableOrView;

            return tr;
        }
        
        public void LoadColumnReferences(SchemaManager schemaManager)
        {
            this.columnReferences.Clear();

            if (tableContext.HasFlag(TableContext.CommonTable) ||
                tableContext.HasFlag(TableContext.Subquery))
            {
                throw new InvalidOperationException();
            }
            else if (tableContext.HasFlag(TableContext.UserDefinedFunction))
            {
                LoadUdfColumnReferences(schemaManager);
            }
            else if (tableContext.HasFlag(TableContext.TableOrView))
            {
                LoadTableOrViewColumnReferences(schemaManager);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private void LoadUdfColumnReferences(SchemaManager schemaManager)
        {
            // TVF calls can have a column alias list
            List<ColumnAlias> calist = null;
            var cal = this.Node.FindDescendant<ColumnAliasList>();
            if (cal != null)
            {
                calist = new List<ColumnAlias>(cal.EnumerateDescendants<ColumnAlias>());
            }

            // Get dataset description
            DatasetBase ds;
            try
            {
                ds = schemaManager.Datasets[DatasetName];
            }
            catch (SchemaException ex)
            {
                throw new NameResolverException(String.Format(ExceptionMessages.UnresolvableDatasetReference, DatasetName, Node.Line, Node.Col), ex);
            }

            int q = 0;
            TableValuedFunction tvf;
            if (ds.TableValuedFunctions.ContainsKey(DatabaseName, SchemaName, DatabaseObjectName))
            {
                tvf = ds.TableValuedFunctions[DatabaseName, SchemaName, DatabaseObjectName];
            }
            else
            {
                // TODO: move this to name resolver instead
                throw new NameResolverException(String.Format(ExceptionMessages.UnresolvableUdfReference, DatabaseObjectName, Node.Line, Node.Col));
            }

            foreach (var cd in tvf.Columns.Values)
            {
                var cr = new ColumnReference(cd, this, new DataTypeReference(cd.DataType));

                // if column alias list is present, use the alias instead of the original name
                if (calist != null)
                {
                    cr.ColumnName = Util.RemoveIdentifierQuotes(calist[q].Value);
                }

                this.columnReferences.Add(cr);
                q++;
            }
        }

        private void LoadTableOrViewColumnReferences(SchemaManager schemaManager)
        {
            // Get dataset description
            DatasetBase ds;
            try
            {
                ds = schemaManager.Datasets[DatasetName];
            }
            catch (SchemaException ex)
            {
                throw new NameResolverException(String.Format(ExceptionMessages.UnresolvableDatasetReference, DatasetName, Node.Line, Node.Col), ex);
            }

            // Get table description
            TableOrView td;
            if (ds.Tables.ContainsKey(DatabaseName, SchemaName, DatabaseObjectName))
            {
                td = ds.Tables[DatabaseName, SchemaName, DatabaseObjectName];
            }
            else if (ds.Views.ContainsKey(DatabaseName, SchemaName, DatabaseObjectName))
            {
                td = ds.Views[DatabaseName, SchemaName, DatabaseObjectName];
            }
            else
            {
                throw new NameResolverException(String.Format(ExceptionMessages.UnresolvableTableReference, DatabaseObjectName, Node.Line, Node.Col));
            }

            // Copy columns to the table reference in appropriate order
            this.columnReferences.AddRange(td.Columns.Values.OrderBy(c => c.ID).Select(c => new ColumnReference(c, this, new DataTypeReference(c.DataType))));
        }

        /// <summary>
        /// Compares two table references for name resolution.
        /// </summary>
        /// <remarks>
        /// It is a logical comparison that follows the rules of name resolution logic
        /// in queries.
        /// </remarks>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Compare(TableReference other)
        {
            // If object are the same
            if (this == other)
            {
                return true;
            }

            // Otherwise compare strings
            bool res = true;

            res = res && (this.DatasetName == null || other.DatasetName == null ||
                    SchemaManager.Comparer.Compare(this.DatasetName, other.DatasetName) == 0);

            res = res && (this.DatabaseName == null || other.DatabaseName == null ||
                    SchemaManager.Comparer.Compare(this.DatabaseName, other.DatabaseName) == 0);

            res = res && (this.SchemaName == null || other.SchemaName == null ||
                    SchemaManager.Comparer.Compare(this.SchemaName, other.SchemaName) == 0);

            res = res && (this.DatabaseObjectName == null || other.DatabaseObjectName == null ||
                    SchemaManager.Comparer.Compare(this.DatabaseObjectName, other.DatabaseObjectName) == 0);

            // When resolving columns, a table reference of a column may match any table or alias
            // if no alias, nor table name is specified but
            // the two aliases, if specified, must always match

            res = res &&
                (this.DatasetName == null && this.DatabaseName == null && this.SchemaName == null && this.DatabaseObjectName == null && this.alias == null ||
                 other.DatasetName == null && other.DatabaseName == null && other.SchemaName == null && other.DatabaseObjectName == null && other.alias == null ||
                 this.alias == null && other.alias == null ||
                 this.alias != null && other.alias != null && SchemaManager.Comparer.Compare(this.alias, other.alias) == 0);

            return res;
        }

        public List<ColumnReference> FilterColumnReferences(ColumnContext columnContext)
        {
            var res = new Dictionary<string, ColumnReference>();
            var t = (TableOrView)DatabaseObject;            // TODO: what if function?

            // Primary key columns
            if ((columnContext & ColumnContext.PrimaryKey) != 0 && t.PrimaryKey != null)
            {
                foreach (var cd in t.PrimaryKey.Columns.Values)
                {
                    if (!res.ContainsKey(cd.ColumnName))
                    {
                        res.Add(cd.ColumnName, new ColumnReference(cd, this, new DataTypeReference(cd.DataType)));
                    }
                }
            }

            // Columns marked as key
            if ((columnContext & ColumnContext.Key) != 0)
            {
                foreach (var cd in t.Columns.Values)
                {
                    if (cd.IsKey && !res.ContainsKey(cd.ColumnName))
                    {
                        res.Add(cd.ColumnName, new ColumnReference(cd, this, new DataTypeReference(cd.DataType)));
                    }
                }
            }

            // Other columns
            foreach (var cr in ColumnReferences)
            {
                // Avoid hint and special contexts
                if (((columnContext & cr.ColumnContext) != 0 || (columnContext & ColumnContext.NonReferenced) != 0)
                    && !res.ContainsKey(cr.ColumnName))
                {
                    res.Add(cr.ColumnName, cr);
                }
            }

            return new List<ColumnReference>(res.Values.OrderBy(c => t.Columns[c.ColumnName].ID));
        }
    }
}
