﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Jhu.Graywulf.Jobs {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class JobNames {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal JobNames() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Jhu.Graywulf.Jobs.JobNames", typeof(JobNames).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Copy.
        /// </summary>
        public static string CopyTablesJob {
            get {
                return ResourceManager.GetString("CopyTablesJob", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Maintenance.
        /// </summary>
        public static string ExportMaintenanceJob {
            get {
                return ResourceManager.GetString("ExportMaintenanceJob", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Export.
        /// </summary>
        public static string ExportTablesJob {
            get {
                return ResourceManager.GetString("ExportTablesJob", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Import.
        /// </summary>
        public static string ImportTablesJob {
            get {
                return ResourceManager.GetString("ImportTablesJob", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Mirror.
        /// </summary>
        public static string MirrorDatabaseJob {
            get {
                return ResourceManager.GetString("MirrorDatabaseJob", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Query.
        /// </summary>
        public static string SqlQueryJob {
            get {
                return ResourceManager.GetString("SqlQueryJob", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Script.
        /// </summary>
        public static string SqlScriptJob {
            get {
                return ResourceManager.GetString("SqlScriptJob", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test.
        /// </summary>
        public static string TestJob {
            get {
                return ResourceManager.GetString("TestJob", resourceCulture);
            }
        }
    }
}
