using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASCOM.HomeMade.Protocol
{
    public class ResponseStatus
    {
        public int HIZ { get; set; }
        public int UVLO { get; set; }
        public int TH_SD { get; set; }
        public int TH_WRN { get; set; }
        public int OCD { get; set; }
        public int WCMD { get; set; }
        public int NOPCMD { get; set; }
        public int BUSY { get; set; }
        public string DIR { get; set; }
        public string MST { get; set; }
    }
}
