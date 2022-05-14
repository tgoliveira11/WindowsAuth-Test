using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Service_Test
{
    public abstract class PeriodicTaskBase
    {
        #region Controller

        private static object controllerLocker = new object();
        private static Action<string, string, Exception> _internalLogger;

        private class TasksService
        {
            public readonly IEnumerable<PeriodicTaskBase> PeriodicTasks;

            public TasksService()
            {
                try
                {
                    PeriodicTasks = PeriodicTasks ?? Library.MEFHelper.List<PeriodicTaskBase>()?.OrderBy(f => f.TaskOrder);
                }
                catch (Exception ex)
                {
                    _internalLogger?.Invoke("Service Tasks Load", $"Error when trying to load {typeof(TasksService).Name}", ex);
                }
            }
        }

        private static TasksService Controller;
        public static async void StartTasks(Action<string, string, Exception> logger, bool isStartup)
        {
            try
            {
                lock (controllerLocker)
                {
                    _internalLogger = _internalLogger ?? logger;
                    if (Controller?.PeriodicTasks == null)
                        Controller = new TasksService();
                }

                var tasks = Controller?.PeriodicTasks?.Where(t => t.IsEnabled).ToArray();
                // No startup da aplicação só roda tasks com o TaskOrder < 0
                if (isStartup)
                    tasks = (from t in tasks
                             where t.TaskOrder < 0
                             select t).ToArray();
                if (tasks?.Any() == true)
                {
                    if (logger != null)
                        logger("Tasks start", $"Starting {nameof(TasksService)} tasks.", null/* TODO Change to default(_) if this is not a reference type */);
                    foreach (var t in tasks)
                        t.StartProcess(logger);
                    logger?.Invoke("Tasks start", $"{tasks.Length} tasks started on {nameof(TasksService)}", null/* TODO Change to default(_) if this is not a reference type */);
                }

                

            }
            catch (Exception ex)
            {
                logger?.Invoke("Tasks start", $"Error when starting {nameof(TasksService)} tasks:", ex);
            }
        }

        public static void StopTasks(Action<string, string, Exception> logger)
        {
            try
            {
                if (Controller != null && Controller.PeriodicTasks != null)
                {
                    foreach (var t in Controller.PeriodicTasks)
                        t.StopProcess();
                }
            }
            catch (Exception ex)
            {
                logger?.Invoke("Tasks start", $"Error when stopping {nameof(TasksService)} tasks:", ex);
            }
        }

        private static readonly object _processingLock;
        static PeriodicTaskBase()
        {
            _processingLock = typeof(TasksService);
        }

        private bool _isProcessing;
        /// <summary>
        ///     ''' Indica se a task está em execução no momento.
        ///     ''' </summary>
        public bool IsProcessing
        {
            get
            {
                lock (_processingLock)
                    return _isProcessing;
            }
            protected set
            {
                lock (_processingLock)
                    _isProcessing = value;
            }
        }

        private bool _isStopping;
        /// <summary>
        ///     ''' Indica se foi solicitada a parada do processamento da task. 
        ///     ''' </summary>
        protected bool IsStopping
        {
            get
            {
                lock (_processingLock)
                    return _isStopping;
            }
            set
            {
                lock (_processingLock)
                    _isStopping = value;
            }
        }

        /// <summary>
        ///     ''' Evento de controle de thread para controle de parada.
        ///     ''' </summary>
        private ManualResetEvent FinishedProcessing;
        protected internal virtual bool NeedsExecution()
        {
            if (IsProcessing || IsStopping)
                return false;
            if (lastExec.HasValue && ExecDelay.HasValue)
                return DateTime.Now >= lastExec.Value.AddSeconds(ExecDelay.Value.TotalSeconds);
            return true;
        }

        #endregion

        #region Properties

        private string _myName;

        private string myName
        {
            get
            {
                if (_myName == null)
                {
                    if (string.IsNullOrWhiteSpace(this.TaskName))
                        _myName = this.GetType().FullName;
                    else
                        _myName = this.TaskName;
                }
                return _myName;
            }
        }
        protected DateTime? lastExec { get; set; }
        protected virtual bool IsDisabled { get; set; }

        protected virtual bool IsEnabled
        {
            get
            {
                if (IsDisabled)
                    return false;
                if (StartTime != null && StartTime.HasValue && DateTime.Now <= new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, StartTime.Value.Hours, StartTime.Value.Minutes, StartTime.Value.Seconds))
                    return false;
                if (EndTime != null && EndTime.HasValue && DateTime.Now > new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, EndTime.Value.Hours, EndTime.Value.Minutes, EndTime.Value.Seconds))
                    return false;
                return true;
            }
        }

        /// <summary>
        ///     ''' Defines the order used by task execution
        ///     ''' </summary>
        protected virtual int TaskOrder
        {
            get
            {
                return 100;
            }
        }

        /// <summary>
        ///     ''' Task name
        ///     ''' </summary>
        ///     ''' <returns></returns>
        protected virtual string TaskName { get; }
        /// <summary>
        ///     ''' The task is enabled only after this time (if has value)
        ///     ''' </summary>
        protected virtual TimeSpan? StartTime { get; set; }
        /// <summary>
        ///     ''' The task is enabled only before this time (if has value)
        ///     ''' </summary>
        protected virtual TimeSpan? EndTime { get; set; }
        /// <summary>
        ///     ''' Defines the minimal timespan between two executions
        ///     ''' </summary>
        protected virtual TimeSpan? ExecDelay { get; set; }

        #endregion

        #region Execution
        /// <summary>
        ///     ''' Inicia o processamento da task em thread paralela.
        ///     ''' </summary>
        public void StartProcess(Action<string, string, Exception> logger)
        {
            if (NeedsExecution())
            {
                _internalLogger = logger;
                IsProcessing = true;
                IsStopping = false;
                lastExec = DateTime.Now;
                FinishedProcessing = new ManualResetEvent(false);
                Task.Factory.StartNew(ProcessAll, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }

        /// <summary>
        ///     ''' Solicita a parada do processamento. Este método é utilizado quando o serviço é pausado ou parado, para garantir
        ///     ''' a integridade dos dados.
        ///     ''' </summary>
        ///     ''' <remarks></remarks>
        public virtual void StopProcess()
        {
            var f = FinishedProcessing;
            IsStopping = true;
            if (IsProcessing && f != null)
            {
                Log($"Stopping task {myName} processing.");
                FinishedProcessing.WaitOne();
                Log($"{myName} stopped.");
            }
        }

        private static System.Globalization.CultureInfo _CurrentCulture;
        public static System.Globalization.CultureInfo CurrentCulture()
        {
            if (_CurrentCulture == null)
            {
                _CurrentCulture = System.Globalization.CultureInfo.DefaultThreadCurrentCulture;
            }
            return _CurrentCulture;
        }

        /// <summary>
        ///     ''' Inicia o processamento de todos os ítens necessários. Continua o processamento
        ///     ''' enquanto o método ProcessItem retornar true ou até ser solicitada uma parada pelo método StopProcess.
        ///     ''' Este método é normalmente executado em sua própria thread.
        ///     ''' </summary>
        protected virtual void ProcessAll()
        {
            try
            {
                Thread.CurrentThread.CurrentUICulture = CurrentCulture();
                Thread.CurrentThread.CurrentCulture = CurrentCulture();
                var continueProcess = true;
                Log(myName, "Execution started @ " + System.Convert.ToString(DateTime.Now.TimeOfDay));
                while (continueProcess && !IsStopping)
                {
                    Log(myName, "Execute iteration started @ " + System.Convert.ToString(DateTime.Now.TimeOfDay));
                    continueProcess = ProcessItem();
                }
            }
            catch (Exception ex)
            {
                Log(myName, "Execute error @ " + System.Convert.ToString(DateTime.Now.TimeOfDay), ex);
            }
            finally
            {
                IsProcessing = false;
                Log(myName, "Execution ended @ " + System.Convert.ToString(DateTime.Now.TimeOfDay));
                if (FinishedProcessing != null)
                    FinishedProcessing.Set();
            }
        }

        /// <summary>
        ///     ''' Método responsável por processar um item. Este método deve retornar true enquanto houver ítens para ser executados.
        ///     ''' </summary>
        protected abstract bool ProcessItem();
        #endregion

        #region Log

        /// <summary>
        ///     ''' Método de notificação de log. O executavel do serviço captura todas as chamadas a este método e as registra em seus devidos lugares (EventLog, Log do Portal, Arquivo texto...).
        ///     ''' </summary>
        ///     ''' <remarks></remarks>
        ///     '''
        private readonly Action<string, string, Exception> _logger;
        /// <summary>
        ///     ''' Método para notificação de log. O executável do serviço captura todas as chamadas a este método e as registra em seus devidos lugares dependendo do tipo.
        ///     ''' Se for passada uma Exception, o log é gravado no log do Portal como Erro e também no EventViewer.
        ///     ''' Se não for passada uma Exception, a mensagem é gravada como Warning no log do portal e no EventViewer.
        ///     ''' </summary>
        ///     ''' <param name="category">Categoria do Log</param>
        ///     ''' <param name="message">Mensagem do Log. Só é usada se não for passada uma Exception</param>
        ///     ''' <param name="ex">Exception para efetuar log de Erros.</param>
        protected void Log(string category, string message = null, Exception ex = null)
        {
            try
            {
                var action = category ?? "Info";
                message = message ?? string.Empty;
                if ((!action.Contains(myName)) && (!message.Contains(myName)))
                    action = $"Task [{myName}] - {action}";
                _internalLogger?.Invoke(action, message, ex);
            }
            catch
            {
            }// tratamento vazio pois a função de log não deveria causar erros na app.
        }

        #endregion
    }
}
