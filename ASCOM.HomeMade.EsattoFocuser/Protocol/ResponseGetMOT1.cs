using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASCOM.HomeMade.Protocol
{
    public class ResponseGetMOT1
    {
        public int ABS_POS { get; set; }
        public int EL_POS { get; set; }
        public int MARK { get; set; }
        public int SPEED { get; set; }
        public int ACC { get; set; }
        public int DEC { get; set; }
        public int MAX_SPEED { get; set; }
        public int MIN_SPEED { get; set; }
        public int TVAL_HOLD { get; set; }
        public int TVAL_RUN { get; set; }
        public int TVAL_ACC { get; set; }
        public int TVAL_DEC { get; set; }
        public int T_FAST { get; set; }
        public int TON_MIN { get; set; }
        public int TOFF_MIN { get; set; }
        public int FS_SPD { get; set; }
        public int OCD_TH { get; set; }
        public int STEP_MODE { get; set; }
        public int CONFIG { get; set; }
        public int ALARM { get; set; }
        public ResponseStatus STATUS { get; set; }
        public string NTC_T { get; set; }
        public int CAL_MAXPOS { get; set; }
        public int CAL_BKLASH { get; set; }
        public int CAL_MIN_LO { get; set; }
        public int CAL_MIN_HI { get; set; }
        public int CAL_MAX_LO { get; set; }
        public int CAL_MAX_HI { get; set; }
        public int CAL_OFFMIN { get; set; }
        public string CAL_STATUS { get; set; }
        public int HSENDET { get; set; }
        public int CAL_MINPOS { get; set; }
        public int CAL_HOMEP { get; set; }
        public int CAL_NUMOF { get; set; }
        public string CAL_DIR { get; set; }
        public int LASTDIR { get; set; }
        public int LOCKMOV { get; set; }
        public int HOLDCURR_STATUS { get; set; }
    }
}
