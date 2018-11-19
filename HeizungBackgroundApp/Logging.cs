using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeizungBackgroundApp
{
    /// <summary>
    /// TODO: Logging with ETW... 
    /// http://gunnarpeipman.com/2016/11/iot-etw-trace/
    /// </summary>
    internal class Logging : ILog
    {
        public static ILog GetLogger(Type type)
        {
            return new Logging(type.Name);
        }
        public Logging(string name)
        {
            _EvtSource = new EventSource(name, EventSourceSettings.Default);
        }

        EventSource _EvtSource;

        public void Debug(string text)
        {
            System.Diagnostics.Debug.WriteLine(text);
            _EvtSource.Write(text, new EventSourceOptions() { Level = EventLevel.Warning, Opcode = EventOpcode.Info });
        }

        public void Error(Exception exp)
        {
            _EvtSource.Write(exp.ToString(), new EventSourceOptions() { Level = EventLevel.Error, Opcode = EventOpcode.Info });
            System.Diagnostics.Debug.WriteLine(exp);
        }

    }
}
