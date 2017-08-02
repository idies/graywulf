﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Configuration;

namespace Jhu.Graywulf.Scheduler
{
    public class SchedulerService : ServiceBase
    {
        public SchedulerService()
        {
            this.ServiceName = "SchedulerService";
        }

        protected override void OnStart(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += Util.ServiceControl.WriteErrorDump;

            // Initialize WCF service host to run the control service
            // It will start the logger
            QueueManager.Instance.Start(Registry.ContextManager.Configuration.ClusterName, false);
        }

        protected override void OnStop()
        {
            // The queue manager will also stop the logger
            QueueManager.Instance.Stop(TimeSpan.FromHours(1.5));
        }

        protected override void OnPause()
        {
            QueueManager.Instance.StopPoller();
        }

        protected override void OnContinue()
        {
            QueueManager.Instance.StartPoller();
        }
    }
}
