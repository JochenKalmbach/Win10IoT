using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Windows.ApplicationModel.Background;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using System.Threading.Tasks;
using System.Threading;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace HeizungBackgroundApp
{
    public sealed class StartupTask : IBackgroundTask
    {
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            // 
            // TODO: Insert code to perform background work
            //
            // If you start any asynchronous methods here, prevent the task
            // from closing prematurely by using BackgroundTaskDeferral as
            // described in http://aka.ms/backgroundtaskdeferral
            //
            _ServiceDeferral = taskInstance.GetDeferral();

            DoWorkAsync();
        }

        BackgroundTaskDeferral _ServiceDeferral;

        void OnQuit()
        {
            _ServiceDeferral.Complete();
        }


        public async void DoWorkAsync()
        {
            using (var cancel = new CancellationTokenSource())
            {
                var heizung = new Heizung();
                Task t1 = heizung.DoWorkAsync(cancel.Token);
                var samsungTv = new OnlineCheck();
                Task t2 = samsungTv.DoWorkAsync(cancel.Token);

                await Task.WhenAll(t1, t2);
            }
        }

    }
}
