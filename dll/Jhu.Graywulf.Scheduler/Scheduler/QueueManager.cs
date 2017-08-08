﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Jhu.Graywulf.Components;
using Jhu.Graywulf.Activities;
using Jhu.Graywulf.Registry;
using Jhu.Graywulf.Logging;

namespace Jhu.Graywulf.Scheduler
{
    /// <summary>
    /// Implements the main functions of the job queue and polling.
    /// </summary>
    /// <remarks>
    /// This class is instantiated as a singleton and is accessed via the
    /// QueueManager.Instance member
    /// </remarks>
    public class QueueManager
    {
        #region Static declarations

        /// <summary>
        /// QueueManager singleton
        /// </summary>
        public static QueueManager Instance = new QueueManager();

        #endregion
        #region Private member varibles

        private object syncRoot = new object();

        /// <summary>
        /// If true, process is running in interactive mode, otherwise
        /// it's a Windows service
        /// </summary>
        private bool interactive;

        /// <summary>
        /// All jobs listed by their workflow instance IDs
        /// </summary>
        /// <remarks>
        /// WorkflowInstanceId, Job. Needs synchronization
        /// </remarks>
        private Dictionary<Guid, Job> runningJobsByWorkflowInstanceId;

        /// <summary>
        /// All jobs listed by job guid
        /// </summary>
        /// <remarks>
        /// Needs synchronization
        /// </remarks>
        private Dictionary<Guid, Job> runningJobsByGuid;

        /// <summary>
        /// Jobs marked as finished and waiting for final processing
        /// on the pooler thread
        /// </summary>
        /// <remarks>
        /// Needs synchronization
        /// </remarks>
        private Queue<JobWorkflowEvent> jobWorkflowEvents;

        /// <summary>
        /// AppDomains stored by their IDs.
        /// </summary>
        /// <remarks>
        /// AppDomain.Id, AppDomainHost
        /// </remarks>
        private Dictionary<int, AppDomainHost> appDomains;

        /// <summary>
        /// Thread to run the poller on
        /// </summary>
        private Thread pollerThread;

        /// <summary>
        /// If true, requests the poller to stop
        /// </summary>
        private bool pollerStopRequested;

        /// <summary>
        /// Used by the poller thread to signal to end of polling
        /// </summary>
        private AutoResetEvent pollerStopEvent;

        /// <summary>
        /// Debug option to prevent loading the entire cluster config
        /// Used for faster test execution
        /// </summary>
        private bool isLayouRequired;

        private bool isControlServiceEnabled;

        /// <summary>
        /// Cached cluster information
        /// </summary>
        private Cluster cluster;

        /// <summary>
        /// Task scheduler
        /// </summary>
        private Scheduler scheduler;

        private System.ServiceModel.ServiceHost controlServiceHost;

        #endregion
        #region Properties

        /// <summary>
        /// Gets the value determining if the scheduler is running in interactive (command-line) mode.
        /// </summary>
        public bool Interactive
        {
            get { return interactive; }
        }

        internal Dictionary<Guid, Job> RunningJobs
        {
            get { return runningJobsByGuid; }
        }

        /// <summary>
        /// Gets a cached version of the cluster configuration
        /// </summary>
        internal Cluster Cluster
        {
            get { return cluster; }
        }

        /// <summary>
        /// Gets a reference to the task scheduler.
        /// </summary>
        public Scheduler Scheduler
        {
            get { return scheduler; }
        }

        public bool IsLayoutRequired
        {
            get { return isLayouRequired; }
            set { isLayouRequired = value; }
        }

        public bool IsControlServiceEnabled
        {
            get { return isControlServiceEnabled; }
            set { isControlServiceEnabled = value; }
        }

        #endregion
        #region Constructors and initializers

        public QueueManager()
        {
            InitializeMembers();
        }

        private void InitializeMembers()
        {
            this.interactive = true;

            this.appDomains = null;
            this.runningJobsByWorkflowInstanceId = null;
            this.runningJobsByGuid = null;
            this.jobWorkflowEvents = null;

            this.pollerStopRequested = false;
            this.pollerThread = null;

            this.isLayouRequired = true;
            this.isControlServiceEnabled = true;
            this.cluster = null;
            this.scheduler = null;
        }

        private void InitializeScheduler()
        {
            this.scheduler = new Scheduler(this);
        }

        #endregion
        #region Cluster configuration caching functions

        /// <summary>
        /// Initializes cluster settings
        /// </summary>
        /// <param name="clusterName"></param>
        private void InitializeCluster(string clusterName)
        {
            // TODO: wrap this into retry logic once recurring cluster loading is implemented
            this.cluster = LoadCluster(clusterName);
        }

        /// <summary>
        /// Loads the configuration of the entire cluster from the database.
        /// </summary>
        /// <returns></returns>
        internal Cluster LoadCluster(string clusterName)
        {
            LogDebug(String.Format("Loading cluster config. Layout is {0}required.", isLayouRequired ? "" : "not "));

            using (RegistryContext context = ContextManager.Instance.CreateContext(ConnectionMode.AutoOpen, TransactionMode.AutoCommit))
            {
                var ef = new EntityFactory(context);
                var c = ef.LoadEntity<Jhu.Graywulf.Registry.Cluster>(clusterName);

                var cluster = new Cluster(c);

                c.LoadMachineRoles(true);

                // *** TODO: handle machines that are down
                // *** TODO: define root object which limits queues handled by scheduler instance
                foreach (var mr in c.MachineRoles.Values)
                {
                    var mri = new MachineRole(mr);
                    cluster.MachineRoles.Add(mr.Guid, mri);

                    mr.LoadQueueInstances(true);

                    foreach (var qi in mr.QueueInstances.Values)
                    {
                        var q = new Queue(qi);
                        cluster.Queues.Add(qi.Guid, q);
                    }

                    mr.LoadMachines(true);

                    foreach (var mm in mr.Machines.Values)
                    {
                        var mmi = new Machine(mm);
                        cluster.Machines.Add(mm.Guid, mmi);

                        mm.LoadServerInstances(true);

                        foreach (var si in mm.ServerInstances.Values)
                        {
                            var ssi = new ServerInstance(si);
                            ssi.Machine = mmi;

                            cluster.ServerInstances.Add(si.Guid, ssi);
                        }

                        mm.LoadQueueInstances(true);

                        foreach (var qi in mm.QueueInstances.Values)
                        {
                            var q = new Queue(qi);
                            cluster.Queues.Add(qi.Guid, q);
                        }
                    }
                }

                if (isLayouRequired)
                {
                    c.LoadDomains(true);
                    foreach (var dom in c.Domains.Values)
                    {
                        dom.LoadFederations(true);
                        foreach (var ff in dom.Federations.Values)
                        {
                            ff.LoadDatabaseDefinitions(true);
                            foreach (var dd in ff.DatabaseDefinitions.Values)
                            {
                                cluster.DatabaseDefinitions.Add(dd.Guid, new DatabaseDefinition(dd));

                                dd.LoadDatabaseInstances(true);
                                foreach (var di in dd.DatabaseInstances.Values)
                                {
                                    var ddi = new DatabaseInstance(di);

                                    // add to global list
                                    cluster.DatabaseInstances.Add(di.Guid, ddi);

                                    // add to database definition lists
                                    Dictionary<Guid, DatabaseInstance> databaseinstances;
                                    if (cluster.DatabaseDefinitions[dd.Guid].DatabaseInstances.ContainsKey((di.DatabaseVersion.Name)))
                                    {
                                        databaseinstances = cluster.DatabaseDefinitions[dd.Guid].DatabaseInstances[di.DatabaseVersion.Name];
                                    }
                                    else
                                    {
                                        databaseinstances = new Dictionary<Guid, DatabaseInstance>();
                                        cluster.DatabaseDefinitions[dd.Guid].DatabaseInstances.Add(di.DatabaseVersion.Name, databaseinstances);
                                    }

                                    databaseinstances.Add(di.Guid, ddi);

                                    ddi.ServerInstance = cluster.ServerInstances[di.ServerInstanceReference.Guid];
                                    ddi.DatabaseDefinition = cluster.DatabaseDefinitions[dd.Guid];
                                }
                            }
                        }
                    }
                }

                LogDebug("Cluster config loaded.");

                return cluster;
            }
        }

        #endregion
        #region QueueManager control functions

        /// <summary>
        /// Starts the scheduler
        /// </summary>
        /// <param name="interactive"></param>
        public void Start(string clusterName, bool interactive)
        {
            // Initialize Queue Manager
            this.interactive = interactive;

            appDomains = new Dictionary<int, AppDomainHost>();
            runningJobsByWorkflowInstanceId = new Dictionary<Guid, Job>();
            runningJobsByGuid = new Dictionary<Guid, Job>();
            jobWorkflowEvents = new Queue<JobWorkflowEvent>();

            // Run sanity check
            Scheduler.Configuration.RunSanityCheck();

            // Log starting event
            Logging.LoggingContext.Current.LogOperation(
                Logging.EventSource.Scheduler,
                "Graywulf Scheduler Service has started.",
                null,
                new Dictionary<string, object>() { { "UserAccount", String.Format("{0}\\{1}", Environment.UserDomainName, Environment.UserName) } });

            // *** TODO: error handling, and repeat a couple of times, then shut down with exception
            // If anything below breaks, we wait for a while and restart the whole scheduler

            InitializeCluster(clusterName);
            InitializeScheduler();
            StartControlService();
            ProcessInterruptedJobs();
            StartPoller();
        }

        /// <summary>
        /// Persists all jobs and stops the scheduler.
        /// </summary>
        /// <remarks>
        /// Waits until running workflow are idle and persists them.
        /// </remarks>
        public void Stop(TimeSpan timeout)
        {
            Stop(timeout, true);
        }

        /// <summary>
        /// Waits for all jobs to complete and stops the scheduler.
        /// </summary>
        /// <param name="timeout"></param>
        public void DrainStop(TimeSpan timeout)
        {
            Stop(timeout, false);
        }

        private void Stop(TimeSpan timeout, bool requestPersist)
        {
            StopControlService();
            StopPoller();

            if (requestPersist)
            {
                foreach (var job in runningJobsByWorkflowInstanceId.Values)
                {
                    PersistJob(job);
                }
            }

            StopAppDomains(timeout);
            ProcessFinishedJobs();

            // Log stop event
            Logging.LoggingContext.Current.LogOperation(
                Logging.EventSource.Scheduler,
                "Graywulf Scheduler Service has stopped.",
                null,
                new Dictionary<string, object>() { { "UserAccount", String.Format("{0}\\{1}", Environment.UserDomainName, Environment.UserName) } });
        }

        /// <summary>
        /// Cancels all running workflows and shuts down the scheduler.
        /// </summary>
        public void Kill(TimeSpan timeout)
        {
            StopControlService();
            StopPoller();

            foreach (var job in runningJobsByWorkflowInstanceId.Values)
            {
                CancelOrTimeOutJob(job, false);
            }

            StopAppDomains(timeout);
            ProcessFinishedJobs();

            // Log stop event
            Logging.LoggingContext.Current.LogOperation(
                Logging.EventSource.Scheduler,
                "Graywulf Remote Service has been killed.",
                null,
                new Dictionary<string, object>() { { "UserAccount", String.Format("{0}\\{1}", Environment.UserDomainName, Environment.UserName) } });
        }

        private void StartControlService()
        {
            if (isControlServiceEnabled)
            {
                // In case the server has been just rebooted
                // wait for the Windows Process Activation Service (WAS)
                if (Util.ServiceControl.IsServiceInstalled("WAS"))
                {
                    Util.ServiceControl.WaitForService("WAS", 1000, 500);
                }

                // Initialize WCF service host to run the control service
                var helper = new ServiceModel.ServiceHelper()
                {
                    ContractType = typeof(ISchedulerControl),
                    ServiceType = typeof(SchedulerControl),
                    ServiceName = "Control",
                    Configuration = Scheduler.Configuration.Endpoint,
                };

                helper.CreateService();

                controlServiceHost = helper.Host;
            }
        }

        private void StopControlService()
        {
            if (isControlServiceEnabled)
            {
                controlServiceHost.Close();
                controlServiceHost = null;
            }
        }

        #endregion
        #region Poller functions

        /// <summary>
        /// Starts the poller thread asynchronously
        /// </summary>
        public void StartPoller()
        {
            if (pollerThread == null)
            {
                pollerStopEvent = new AutoResetEvent(false);
                pollerStopRequested = false;

                pollerThread = new Thread(new ThreadStart(Poller));
                pollerThread.Name = "Graywulf Scheduler Poller";
                pollerThread.Start();
            }
            else
            {
                throw new InvalidOperationException(ExceptionMessages.PollerHasAlreadyStarted);
            }

            LogOperation("The job poller thread has been started.");
        }

        /// <summary>
        /// Stops the poller thread synchronously
        /// </summary>
        /// <remarks>
        /// Waits until the last polling completes
        /// </remarks>
        public void StopPoller()
        {
            if (pollerThread == null)
            {
                throw new InvalidOperationException(ExceptionMessages.PollerHasNotStarted);
            }

            if (pollerThread != null)
            {
                pollerStopRequested = true;
                pollerStopEvent.WaitOne();
                pollerThread = null;
            }

            LogOperation("The job poller thread has been stopped.");
        }

        /// <summary>
        /// Poller loop
        /// </summary>
        private void Poller()
        {
            while (!pollerStopRequested)
            {
                /*
                // Polling might fail, but wrap it into retry logic so it keeps trying
                // on errors. This is to prevent the scheduler from stopping when the
                // connection to the database is lost intermittently.
                var pollRetry = new DelayedRetryLoop()
                {
                    InitialDelay = 1000,
                    MaxDelay = 30000,
                    DelayMultiplier = 1.5,
                    MaxRetries = -1
                };

                pollRetry.Execute(Poll);*/

                ProcessTimedOutJobs();
                PollAndStartJobs();
                PollAndCancelJobs();
                ProcessFinishedJobs();
                UnloadOldAppDomains(Constants.UnloadAppDomainTimeout);

                Thread.Sleep(Scheduler.Configuration.PollingInterval);
            }

            // Signal the completion event
            pollerStopEvent.Set();
        }

        private void BookkeepAddJob(Job job)
        {
            lock (syncRoot)
            {
                runningJobsByWorkflowInstanceId.Add(job.WorkflowInstanceId, job);
                runningJobsByGuid.Add(job.Guid, job);
                appDomains[job.AppDomainID].RunningJobs.Add(job.Guid, job);

                // Update job queue last user, so round-robin scheduling will work
                cluster.Queues[job.QueueGuid].Jobs.Add(job.Guid, job);
                cluster.Queues[job.QueueGuid].LastUserGuid = job.UserGuid;
            }
        }

        private void BookkeepRemoveJob(Job job)
        {
            // TODO: can this happen multiple times?

            lock (syncRoot)
            {
                runningJobsByGuid.Remove(job.Guid);
                runningJobsByWorkflowInstanceId.Remove(job.WorkflowInstanceId);
                cluster.Queues[job.QueueGuid].Jobs.Remove(job.Guid);
                appDomains[job.AppDomainID].RunningJobs.Remove(job.Guid);
            }
        }

        /// <summary>
        /// Process jobs that are found in an intermediate stage, possibly
        /// due to an unexpected shutdown of the scheduler.
        /// </summary>
        private void ProcessInterruptedJobs()
        {
            // TODO: where to handle errors?

            using (RegistryContext context = ContextManager.Instance.CreateContext(ConnectionMode.AutoOpen, TransactionMode.AutoCommit))
            {
                int failed = 0;
                int started = 0;
                var jf = new JobInstanceFactory(context);

                jf.UserGuid = Guid.Empty;
                jf.QueueInstanceGuids.UnionWith(Cluster.Queues.Keys);

                jf.JobExecutionStatus =
                    JobExecutionState.Executing |
                    JobExecutionState.Persisting |
                    JobExecutionState.Cancelling |
                    JobExecutionState.Starting;

                foreach (var j in jf.FindJobInstances())
                {
                    j.ReleaseLock(true);

                    if ((jf.JobExecutionStatus & JobExecutionState.Starting) != 0)
                    {
                        // Jobs marked as waiting probably can be restarted without a side effect
                        j.JobExecutionStatus = JobExecutionState.Scheduled;

                        started++;
                    }
                    else
                    {
                        // Process previously interrupted jobs (that are marked as running)
                        // Process jobs that are marked as executing - these remained in this state
                        // because of a failure in the scheduler
                        j.JobExecutionStatus = JobExecutionState.Failed;
                        j.ExceptionMessage = Jhu.Graywulf.Registry.ExceptionMessages.SchedulerUnexpectedShutdown;

                        failed++;
                    }

                    j.Save();

                    if (j.JobExecutionStatus == JobExecutionState.Failed)
                    {
                        j.RescheduleIfRecurring();
                    }
                }

                LogOperation(String.Format(
                    "Processed interrupted jobs. Marked {0} jobs as failed and {1} jobs as scheduler.",
                    failed, started));
            }
        }

        /// <summary>
        /// Checks each job if it timed out then cancels them.
        /// </summary>
        private void ProcessTimedOutJobs()
        {
            Job[] jobs;

            lock (syncRoot)
            {
                var q = from j in runningJobsByWorkflowInstanceId.Values
                        where j.IsTimedOut(Cluster.Queues[j.QueueGuid].Timeout)
                        select j;
                jobs = q.ToArray();
            }

            foreach (Job job in jobs)
            {
                CancelOrTimeOutJob(job, true);
            }
        }

        /// <summary>
        /// Queries the registry for new jobs to schedule
        /// </summary>
        private void PollAndStartJobs()
        {
            foreach (var queue in Cluster.Queues.Values)
            {
                var jobs = PollNewJobs(queue);

                if (jobs != null)
                {
                    foreach (var job in jobs)
                    {
                        StartOrResumeJob(job);
                    }
                }
            }
        }

        private List<Job> PollNewJobs(Queue queue)
        {
            try
            {
                List<Job> res = new List<Job>();

                using (RegistryContext context = ContextManager.Instance.CreateContext(ConnectionMode.AutoOpen, TransactionMode.AutoCommit))
                {
                    // The number of jobs to be requested from the queue is the
                    // number of maximum outstanding jobs minus the number of
                    // already running jobs

                    var maxjobs = queue.MaxOutstandingJobs - queue.Jobs.Count;

                    var jf = new JobInstanceFactory(context);
                    var jis = jf.FindNextJobInstances(
                        queue.Guid,
                        queue.LastUserGuid,
                        maxjobs);

                    foreach (var ji in jis)
                    {
                        var ef = new EntityFactory(context);
                        var user = ef.LoadEntity<User>(ji.UserGuidOwner);

                        var job = new Job()
                        {
                            Guid = ji.Guid,
                            QueueGuid = ji.ParentReference.Guid,
                            WorkflowTypeName = ji.WorkflowTypeName,
                            Timeout = ji.JobTimeout,

                            ClusterGuid = cluster.Guid,
                            DomainGuid = ji.JobDefinition.Federation.Domain.Guid,
                            FederationGuid = ji.JobDefinition.Federation.Guid,
                            JobGuid = ji.Guid,
                            JobID = ji.JobID,
                            JobName = ji.Name,
                            UserGuid = user.Guid,
                            UserName = user.Name,
                        };

                        if ((ji.JobExecutionStatus & JobExecutionState.Scheduled) != 0)
                        {
                            job.Status = JobStatus.Starting;
                            ji.JobExecutionStatus = JobExecutionState.Starting;
                        }
                        else if ((ji.JobExecutionStatus & JobExecutionState.Persisted) != 0)
                        {
                            // Save cancel requested flag here
                            ji.JobExecutionStatus ^= JobExecutionState.Persisted;
                            ji.JobExecutionStatus |= JobExecutionState.Starting;

                            job.Status = JobStatus.Resuming;
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }

                        ji.Save();
                        res.Add(job);
                    }
                }

                return res;
            }
            catch (Exception ex)
            {
                LogError(ex);
                return null;
            }
        }

        private void PollAndCancelJobs()
        {
            foreach (var queue in Cluster.Queues.Values)
            {
                var jobs = PollCancellingJobs(queue);

                if (jobs != null)
                {
                    foreach (var job in jobs)
                    {
                        CancelOrTimeOutJob(job, false);
                    }
                }
            }
        }

        private List<Job> PollCancellingJobs(Queue queue)
        {
            try
            {
                List<Job> res = new List<Job>();

                using (var context = ContextManager.Instance.CreateContext(ConnectionMode.AutoOpen, TransactionMode.AutoCommit))
                {
                    var jf = new JobInstanceFactory(context);


                    jf.UserGuid = Guid.Empty;
                    jf.QueueInstanceGuids.Clear();
                    jf.QueueInstanceGuids.Add(queue.Guid);
                    jf.JobExecutionStatus = JobExecutionState.CancelRequested;

                    foreach (var ji in jf.FindJobInstances())
                    {
                        Job job;

                        lock (syncRoot)
                        {
                            if (runningJobsByGuid.TryGetValue(ji.Guid, out job))
                            {
                                res.Add(job);
                            }
                            else
                            {
                                // Cancelling a job that's not running, or
                                // running in a different scheduler instance
                            }
                        }
                    }
                }

                return res;
            }
            catch (Exception ex)
            {
                LogError(ex);
                return null;
            }
        }

        private void ProcessFinishedJobs()
        {
            lock (syncRoot)
            {
                while (jobWorkflowEvents.Count > 0)
                {
                    var e = jobWorkflowEvents.Dequeue();
                    ProcessFinishedJob(e);
                    BookkeepRemoveJob(e.Job);
                }
            }
        }

        private void ProcessFinishedJob(JobWorkflowEvent e)
        {
            try
            {
                using (RegistryContext context = ContextManager.Instance.CreateContext(ConnectionMode.AutoOpen, TransactionMode.AutoCommit))
                {
                    var job = e.Job;

                    // *** TODO why do we need to set the job guid here explicitly?
                    context.JobReference.Guid = job.Guid;

                    var ef = new EntityFactory(context);
                    var ji = ef.LoadEntity<JobInstance>(job.Guid);

                    // Update execution status, error message and finish time
                    switch (e.WorkflowEvent)
                    {
                        case WorkflowEventType.Completed:
                            LogJobOperation("Job {0} has completed.", job);
                            ji.JobExecutionStatus = JobExecutionState.Completed;
                            break;
                        case WorkflowEventType.Cancelled:
                            LogJobOperation("Job {0} has been cancelled.", job);
                            ji.JobExecutionStatus = JobExecutionState.Cancelled;
                            break;
                        case WorkflowEventType.TimedOut:
                            LogJobOperation("Job {0} has timed out.", job);
                            ji.JobExecutionStatus = JobExecutionState.TimedOut;
                            break;
                        case WorkflowEventType.Persisted:
                            LogJobOperation("Job {0} has been persisted.", job);
                            ji.JobExecutionStatus = JobExecutionState.Persisted;
                            break;
                        case WorkflowEventType.Failed:
                            LogJobOperation("Job {0} has failed.", job);
                            ji.JobExecutionStatus = JobExecutionState.Failed;
                            ji.ExceptionMessage = GetExceptionMessage(e.WorkflowException);
                            break;
                    }

                    // Update registry
                    ji.DateFinished = DateTime.Now;
                    ji.Save();

                    ji.ReleaseLock(false);
                    ji.RescheduleIfRecurring();
                }
            }
            catch (Exception ex)
            {
                // TODO: remove failed job
                LogError(ex);
            }
        }

        protected string GetExceptionMessage(Exception exception)
        {
            if (exception is AggregateException)
            {
                return exception.InnerException.Message;
            }
            else
            {
                return exception.Message;
            }
        }

        #endregion
        #region Direct job control functions

        /// <summary>
        /// Starts a single job
        /// </summary>
        /// <param name="job"></param>
        private void StartOrResumeJob(Job job)
        {
            try
            {
                using (RegistryContext context = ContextManager.Instance.CreateContext(ConnectionMode.AutoOpen, TransactionMode.AutoCommit))
                {
                    // TODO: why need to set job guid here manually?
                    context.JobReference.Guid = job.Guid;

                    var ef = new EntityFactory(context);
                    var ji = ef.LoadEntity<JobInstance>(job.Guid);

                    // Lock the job, so noone else can pick it up
                    ji.DateStarted = DateTime.Now;
                    ji.ObtainLock();

                    ji.Save();
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                return;
                // TODO: remove failing job
            }

            // Schedule job in the appropriate app domain
            var adh = GetOrCreateAppDomainHost(job);

            // Check if job is a new instance or previously persisted and
            // has to be resumed
            switch (job.Status)
            {
                case JobStatus.Starting:
                    LogJobOperation("Starting job {0}.", job);
                    job.WorkflowInstanceId = adh.PrepareStartJob(job);
                    break;
                case JobStatus.Resuming:
                    LogJobOperation("Resuming job {0}.", job);
                    job.WorkflowInstanceId = adh.PrepareResumeJob(job);
                    break;
                default:
                    throw new NotImplementedException();
            }

            // Update job status
            job.TimeStarted = DateTime.Now;
            job.Status = JobStatus.Executing;
            job.AppDomainID = adh.ID;

            // Send job for execution in the designated AppDomain
            adh.RunJob(job);

            BookkeepAddJob(job);
        }

        private void CancelOrTimeOutJob(Job job, bool timeout)
        {
            try
            {
                using (RegistryContext context = ContextManager.Instance.CreateContext(ConnectionMode.AutoOpen, TransactionMode.AutoCommit))
                {
                    // *** TODO: why set guid here manually?
                    context.JobReference.Guid = job.Guid;

                    var ef = new EntityFactory(context);
                    var ji = ef.LoadEntity<JobInstance>(job.Guid);

                    ji.JobExecutionStatus = JobExecutionState.Cancelling;

                    ji.Save();
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                return;
                // TODO: remove failing job
            }

            if (timeout)
            {
                LogJobStatus(String.Format("Job {0} is timing out.", job.JobID), job);
                job.Status = JobStatus.TimedOut;
                appDomains[job.AppDomainID].TimeOutJob(job);
            }
            else
            {
                LogJobStatus(String.Format("Job {0} is cancelling.", job.JobID), job);
                job.Status = JobStatus.Cancelled;
                appDomains[job.AppDomainID].CancelJob(job);
            }
        }

        private void PersistJob(Job job)
        {
            try
            {
                using (RegistryContext context = ContextManager.Instance.CreateContext(ConnectionMode.AutoOpen, TransactionMode.AutoCommit))
                {
                    // *** TODO: why do we have to set jobguid here explicitly?
                    context.JobReference.Guid = job.Guid;

                    var ef = new EntityFactory(context);
                    var ji = ef.LoadEntity<JobInstance>(job.Guid);

                    ji.JobExecutionStatus = JobExecutionState.Persisting;

                    ji.Save();
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                return;
                // TODO: remove failed job
            }

            LogJobStatus("Persisting job {0}.", job);
            appDomains[job.AppDomainID].PersistJob(job);
            job.Status = JobStatus.Persisted;
        }

        /// <summary>
        /// Finished the execution of a job and records the results in the registry.
        /// </summary>
        /// <param name="workflowInstanceId"></param>
        /// <param name="eventType"></param>
        private void FinishJob(Guid instanceID, WorkflowApplicationHostEventArgs e)
        {
            // TODO: certain workflows (probably failing ones) raise more than
            // one event that mark the job as finished. For now, we do the bookeeping
            // only once and remove the job from running jobs at the very first event

            lock (syncRoot)
            {
                var evn = new JobWorkflowEvent()
                {
                    Job = runningJobsByWorkflowInstanceId[instanceID],
                    WorkflowEvent = e.EventType,
                    WorkflowException = e.Exception,
                };

                jobWorkflowEvents.Enqueue(evn);
            }
        }

        /// <summary>
        /// Mark job as failed and make sure it's removed from the queue
        /// </summary>
        /// <param name="job"></param>
        private void FailJob(Job job, Exception ex)
        {
            lock (syncRoot)
            {
                var evn = new JobWorkflowEvent()
                {
                    Job = job,
                    WorkflowEvent = WorkflowEventType.Failed,
                    WorkflowException = ex,
                };

                jobWorkflowEvents.Enqueue(evn);
            }
        }

        /// <summary>
        /// Looks up information about a job necessary to configure a registry context
        /// </summary>
        /// <remarks>
        /// This function is called by the workflow framework via IScheduler when creating
        /// a new context to access the registry.
        /// </remarks>
        /// <param name="workflowInstanceId"></param>
        internal JobInfo GetJobInfo(Guid workflowInstanceId)
        {
            var job = runningJobsByWorkflowInstanceId[workflowInstanceId];
            return new JobInfo(job);
        }

        #endregion
        #region AppDomainHost control functions

        /// <summary>
        /// Returns an AppDomainHost to run the job.
        /// </summary>
        /// <remarks>
        /// Either an existing AppDomainHost is returned that has the
        /// required assembly, or a new AppDomainHost is created.
        /// </remarks>
        /// <param name="job"></param>
        /// <returns></returns>
        private AppDomainHost GetOrCreateAppDomainHost(Job job)
        {
            System.AppDomain ad;
            var isnew = Components.AppDomainManager.Instance.GetAppDomainForType(job.WorkflowTypeName, true, out ad);
            int id = ad.Id;

            if (isnew || !appDomains.ContainsKey(id))
            {
                // New app domain, wire up event handlers and create host
                var adh = new AppDomainHost(ad);
                appDomains.Add(ad.Id, adh);

                adh.WorkflowEvent += AppDomainHost_WorkflowEvent;
                adh.UnhandledException += AppDomainHost_UnhandledException;
                adh.Start(Scheduler, interactive);

                return adh;
            }
            else
            {
                // Old app domain, return existing host
                return appDomains[id];
            }
        }

        private void StopAppDomains(TimeSpan timeout)
        {
            // No synchronization here, because it is called from stop or kill
            // at which point the poller thread has been stopped already.

            // We can also be sure that all jobs have stopped at this point

            foreach (var id in appDomains.Keys)
            {
                var adh = appDomains[id];
                adh.Stop(timeout, interactive);
                adh.Unload();
            }
        }

        /// <summary>
        /// Checks AppDomain usage and unload those that have been idle
        /// for a given period of time.
        /// </summary>
        private void UnloadOldAppDomains(TimeSpan timeout)
        {
            var q = from adh in appDomains.Values
                    where adh.RunningJobs.Count == 0 &&
                          (DateTime.Now - adh.LastTimeActive) > Scheduler.Configuration.AppDomainIdle
                    select adh;
            var adhs = q.ToArray();

            foreach (var adh in adhs)
            {
                adh.Stop(timeout, interactive);
                adh.Unload();
                appDomains.Remove(adh.ID);
            }
        }

        /// <summary>
        /// Handles events raised by any AppDomainHost
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void AppDomainHost_WorkflowEvent(object sender, WorkflowApplicationHostEventArgs e)
        {
            FinishJob(e.InstanceId, e);
        }

        void AppDomainHost_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var adh = (AppDomainHost)sender;
            var ex = e.ExceptionObject as Exception;
            LogError(ex);

            // Finish all jobs that have been running inside the app domain
            foreach (var job in adh.RunningJobs.Values)
            {
                FailJob(job, ex);
            }

            // TODO: figure out how to unit-test this
        }

        #endregion
        #region Logging functions

        internal void LogDebug(string message, params object[] args)
        {
#if DEBUG
            var method = Logging.LoggingContext.Current.UnwindStack(2);
            Logging.LoggingContext.Current.LogDebug(
                Logging.EventSource.Scheduler,
                String.Format(message, args),
                method.DeclaringType.FullName + "." + method.Name,
                null);
#endif
        }

        private void LogJobStatus(string message, Job job)
        {
#if DEBUG
            var method = Logging.LoggingContext.Current.UnwindStack(2);
            var e = Logging.LoggingContext.Current.CreateEvent(
                EventSeverity.Status,
                EventSource.Scheduler,
                String.Format(message, job.JobID),
                method.DeclaringType.FullName + "." + method.Name,
                null,
                null);
            job.UpdateLoggingEvent(e);
            Logging.LoggingContext.Current.WriteEvent(e);
#endif
        }

        private void LogOperation(string message)
        {
            var method = Logging.LoggingContext.Current.UnwindStack(2);
            Logging.LoggingContext.Current.LogOperation(
                Logging.EventSource.Scheduler,
                message,
                method.DeclaringType.FullName + "." + method.Name,
                null);
        }

        private void LogJobOperation(string message, Job job)
        {
            var method = Logging.LoggingContext.Current.UnwindStack(2);
            var e = Logging.LoggingContext.Current.CreateEvent(
                EventSeverity.Operation,
                EventSource.Scheduler,
                String.Format(message, job.JobID),
                method.DeclaringType.FullName + "." + method.Name,
                null,
                null);
            job.UpdateLoggingEvent(e);
            Logging.LoggingContext.Current.WriteEvent(e);
        }

        private void LogError(Exception ex)
        {
            var method = Logging.LoggingContext.Current.UnwindStack(2);
            Logging.LoggingContext.Current.LogError(Logging.EventSource.Scheduler, ex);
        }

        #endregion
    }
}
