﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASCOM.HomeMade.Protocol
{
    public class ResponseRes
    {
        public ResponseGet get { get; set; }
        public ResponseCmd cmd { get; set; }
    }
}
