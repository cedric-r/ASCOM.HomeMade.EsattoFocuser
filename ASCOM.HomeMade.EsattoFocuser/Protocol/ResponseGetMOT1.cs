using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASCOM.HomeMade.Protocol
{
    public class ResponseGetMOT1
    {
        public int ABS_POS;
        public int EL_POS;
        public int MARK;
        public int SPEED;
        public int ACC;
        public int DEC;
        public int MAX_SPEED;
        public int MIN_SPEED;
        public int TVAL_HOLD;
        public int TVAL_RUN;
        public int TVAL_ACC;
        public int TVAL_DEC;
        public int T_FAST;
        public int TON_MIN;
        public int TOFF_MIN;
        public int FS_SPD;
        public int OCD_TH;
        public int STEP_MODE;
        public int CONFIG;
        public int ALARM;
        public ResponseStatus STATUS;
        public double NTC_T;
        public int CAL_MAXPOS;
        public int CAL_BKLASH;
        public int CAL_MIN_LO;
        public int CAL_MIN_HI;
        public int CAL_MAX_LO;
        public int CAL_MAX_HI;
        public int CAL_OFFMIN;
        public string CAL_STATUS;
        public int HSENDET;
        public int CAL_MINPOS;
        public int CAL_HOMEP;
        public int CAL_NUMOF;
        public string CAL_DIR;
        public int LASTDIR;
        public int LOCKMOV;
        public int HOLDCURR_STATUS;
    }
}
