using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASCOM.HomeMade
{
    public class DeviceStatus
    {
        public bool OK = false;
        public int speed = 0;
        public bool connected = false;
        public int maxStep = 0;
        public int position = 0;
        public int maxPosition = 0;
        public bool busy = false;
        public double internalTemperature = 0;
        public double externalTemperature = 0;
    }
}
