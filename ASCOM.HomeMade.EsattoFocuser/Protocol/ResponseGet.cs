﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASCOM.HomeMade.Protocol
{
    public class ResponseGet
    {
        public ResponseGetMOT1 MOT1 { get; set; }
        public double EXT_T { get; set; }
    }
}
