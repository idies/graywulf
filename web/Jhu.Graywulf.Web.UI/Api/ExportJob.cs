﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Jhu.Graywulf.Registry;
using Jhu.Graywulf.Schema;
using Jhu.Graywulf.Format;
using Jhu.Graywulf.Jobs.ExportTables;
using Jhu.Graywulf.SqlParser;

namespace Jhu.Graywulf.Web.UI.Api
{
    public class ExportJob : Job
    {
        private string[] tables;
        private string format;
        private Uri uri;

        public override JobType Type
        {
            get { return JobType.Export; }
            set {  }
        }

        public string[] Tables
        {
            get { return tables; }
            set { tables = value; }
        }

        public string Format
        {
            get { return format; }
            set { format = value; }
        }

        public Uri Uri
        {
            get { return uri; }
            set { uri = value; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// Used to display list of tables on the web UI.
        /// </remarks>
        public string TableList
        {
            get
            {
                if (tables != null)
                {
                    string res = "";
                    for (int i = 0; i < tables.Length; i++)
                    {
                        if (i > 0)
                        {
                            res += ", ";
                        }

                        res += tables[i];
                    }

                    return res;
                }
                else
                {
                    return String.Empty;
                }
            }
        }

        public ExportJob()
        {
            InitializeMembers();
        }

        public ExportJob(JobInstance jobInstance)
            :base(jobInstance)
        {
            InitializeMembers();

            CopyFromJobInstance(jobInstance);
        }

        private void InitializeMembers()
        {
            this.tables = null;
            this.format = null;
            this.uri = null;
        }

        private void CopyFromJobInstance(JobInstance jobInstance)
        {
            // Because job parameter type might come from an unknown 
            // assembly, instead of deserializing, read xml directly here

            if (jobInstance.Parameters.ContainsKey(Jhu.Graywulf.Jobs.Constants.JobParameterExport))
            {
                var xml = new XmlDocument();
                xml.LoadXml(jobInstance.Parameters[Jhu.Graywulf.Jobs.Constants.JobParameterExport].XmlValue);

                this.tables = new string[] { "xxx" };
                this.format = GetAttribute(xml, "/ExportTablesParameters/Destinations/DataFileBase", "z:Type");
                this.uri = new Uri(GetXmlInnerText(xml, "ExportTablesParameters/Uri"));

                // TODO:
                // jobDescription.SchemaName = GetXmlInnerText(xml, "ExportTables/Sources/TableOrView/SchemaName");
                // jobDescription.ObjectName = GetXmlInnerText(xml, "ExportTables/Sources/TableOrView/ObjectName");
                // jobDescription.Path = GetXmlInnerText(xml, "ExportTables/Destinations/DataFileBase/Uri");
            }
        }

        public ExportTablesParameters CreateParameters(FederationContext context)
        {
            var ef = ExportTablesFactory.Create(context.Federation);

            // Add tables and destination files
            var ts = new TableOrView[tables.Length];

            // Table names are specified as string, so we need to parse them
            var parser = new SqlParser.SqlParser();
            var nr = new SqlNameResolver()
            {
                SchemaManager = context.SchemaManager
            };

            for (int i = 0; i < tables.Length; i++)
            {
                var tn = (SqlParser.TableOrViewName)parser.Execute(new SqlParser.TableOrViewName(), tables[i]);
                var tr = tn.TableReference;
                tr.SubstituteDefaults(context.SchemaManager, context.MyDBDataset.Name);
                ts[i] = context.MyDBDataset.Tables[tr.DatabaseName, tr.SchemaName, tr.DatabaseObjectName];
            }

            return ef.CreateParameters(context.Federation, ts, uri, format, GetQueueName(context), Comments);
        }

        private string GetTableName(TableOrView table)
        {
            return table.ObjectName;
        }

        public override JobInstance Schedule(FederationContext context)
        {
            var p = CreateParameters(context);

            var ef = ExportTablesFactory.Create(context.Federation);
            var job = ef.ScheduleAsJob(p, GetQueueName(context), Comments);

            job.Save();

            return job;
        }
    }
}
