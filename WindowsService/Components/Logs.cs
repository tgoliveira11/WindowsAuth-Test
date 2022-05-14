using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsService.Components
{
    public class Logs
    {
        public static void WriteLog(string Action, string Message, Exception ex)
        {
            try
            {
                
            }
            catch 
            {
            }
        }

        public static void WriteLog(string Action, string Message, EventLogEntryType Type)
        {
            switch (Type)
            {
                case EventLogEntryType.Error:
                    {
                        //Raise(Monitoring.EventTypes.CriticalError, Action, Message, Monitoring.EventCodes.Workflow);
                        break;
                    }

                case EventLogEntryType.FailureAudit:
                case EventLogEntryType.SuccessAudit:
                    {
                        //Raise(Monitoring.EventTypes.Audit, Action, Message, Monitoring.EventCodes.Workflow);
                        break;
                    }

                case EventLogEntryType.Warning:
                    {
                        //Raise(Monitoring.EventTypes.Warning, Action, Message, Monitoring.EventCodes.Workflow);
                        break;
                    }

                case EventLogEntryType.Information:
                    {
                        //Raise(Monitoring.EventTypes.Information, Action, Message, Monitoring.EventCodes.Workflow);
                        break;
                    }

                default:
                    {
                        //Raise(Monitoring.EventTypes.NonCriticalError, Action, Message, Monitoring.EventCodes.Workflow);
                        break;
                    }
            }
        }
    }
}
