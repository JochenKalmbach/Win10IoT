using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;

namespace HeizungBackgroundApp.Web
{
    class WebServer
    {
        public WebServer(AppServiceConnection serviceCon)
        {
            _Server  = new HttpServer(8000, serviceCon);
        }

        HttpServer _Server;
    }
}
