using Service_Test;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using WindowsService.Components;

namespace WindowsService
{
    public partial class Service1 : ServiceBase
    {
        public Service1()
        {
            InitializeComponent();
        }

        public int GetTimerInterval()
        {
            // Primeiro tenta pegar da tag nova
            TimeSpan? timerTimespan = null;
            
            // Pega o valor default de 2 minutos
            if (timerTimespan == null || timerTimespan.Value.TotalSeconds < 1)
                timerTimespan = new TimeSpan(0, 2, 0);
            
            return (int)timerTimespan.Value.TotalMilliseconds;
        }


        protected override void OnStart(string[] args)
        {
            System.Threading.Thread.Sleep(10000);
            timer1.Interval = GetTimerInterval();
            timer1.Enabled = true;
            PeriodicTaskBase.StartTasks(Log, true);
        }

        protected override void OnStop()
        {
            try
            {
                Logs.WriteLog("Service Stoping", $"Service WindowsTestService stop requested at {DateTimeOffset.Now}", EventLogEntryType.Warning);
                PeriodicTaskBase.StopTasks(Log);
                Logs.WriteLog("Service Stopped", $"Service WindowsTestService stopped at {DateTimeOffset.Now}", EventLogEntryType.Warning);
            }
            catch (Exception ex)
            {
                Log("OnStop", null, ex);
            }
        }

        private void Log(string category, string message, Exception ex)
        {
            var action = category ?? string.Empty;
            if (ex == null)
                Logs.WriteLog(action, message ?? string.Empty, EventLogEntryType.Information);
            else
                Logs.WriteLog(action, message ?? string.Empty, ex);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                PeriodicTaskBase.StartTasks(Log, false);
            }
            catch (Exception ex)
            {
                Log("timerElapsed", null/* TODO Change to default(_) if this is not a reference type */, ex);
            }
        }
    }
}
