using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASCOM.HomeMade.Protocol
{
    public class ResponseStatus
    {
        public int HIZ;
        public int UVLO;
        public int TH_SD;
        public int TH_WRN;
        public int OCD;
        public int WCMD;
        public int NOPCMD;
        public int BUSY;
        public string DIR;
        public string MST;
    }
}
