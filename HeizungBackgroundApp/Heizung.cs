using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;

namespace HeizungBackgroundApp
{
    internal class Heizung
    {
        ILog _Logger = Logging.GetLogger(typeof(Heizung));

        public async Task DoWorkAsync(CancellationToken cancel)
        {
            try
            {
                // Display all serial devices
                string aqs = SerialDevice.GetDeviceSelector();

                var dis = await DeviceInformation.FindAllAsync(aqs);
                for (int i = 0; i < dis.Count; i++)
                {
                    System.Diagnostics.Debug.WriteLine("{0} - {1} - {2}", dis[i].Id, dis[i].Name, dis[i].Kind);
                }

                if (dis.Count > 0)
                {
                    // Normally the first one is the USB device:
                    var ol = new Viessmann.OptoLink(dis[0].Id);
                    await ol.DoWorkAsync(cancel);
                }
            }
            catch(Exception exp)
            {
                _Logger.Error(exp);
                throw;
            }

        }
    }
}
