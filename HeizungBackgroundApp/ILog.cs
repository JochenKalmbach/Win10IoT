using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeizungBackgroundApp
{
    internal interface ILog
    {
        void Debug(string text);

        void Error(Exception exp);
    }
}
