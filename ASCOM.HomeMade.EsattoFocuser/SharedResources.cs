/**
 * ASCOM.HomeMade.EsattoFocuser - Esatto controller
 * Copyright (C) 2022 Cedric Raguenaud [cedric@raguenaud.earth]
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 *
 */
using ASCOM.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ASCOM.HomeMade
{
    public static class SharedResources
    {
        // object used for locking to prevent multiple drivers accessing common code at the same time
        private static readonly object lockObject = new object();

        // Shared serial port. This will allow multiple drivers to use one single serial port.
        private static readonly ASCOM.Utilities.Serial ASCOMSerial = new ASCOM.Utilities.Serial();

        // Counter for the number of connections to the serial port
        private static int ConnectedClients = 0;

        // Donc bother to implement all the structures for simple calls
        static string GET_STATUS = "{\"req\":{\"get\":{\"MOT1\":\"\"}}}";
        static string GET_EXT_T = "{\"req\":{\"get\":{\"EXT_T\":\"\"}}}";
        static string EXT_T_RESPONSE = "{\"res\":{\"get\":{\"EXT_T\":\"#TEMP#\"}}}";
        static string GOTO = "{\"req\":{\"cmd\":{\"MOT1\":{\"GOTO\":#POS#}}}}";
        static string GOTO_RESPONSE = "{\"res\":{\"cmd\":{\"MOT1\":{\"GOTO\":\"done\"}}}}";
        static string GET_SN = "{\"req\":{\"get\":{\"SN\":\"\"}}}";
        static string GET_FW = "{\"req\":{\"get\":{\"SWVERS\":\"\"}}}";
        static string ABORT = "{\"req\":{\"cmd\":{\"MOT1\" :{\"MOT_ABORT\":\"\"}}}}";
        static string STOP = "{\"req\":{\"cmd\":{\"MOT1\" :{\"MOT_STOP\":\"\"}}}}";
        static string GO_HOME = "{\"req\":{\"cmd\":{\"MOT1\" :{\"GOHOME\":\"\"}}}}";

        private static int CMD_LENGTH = 1024;
        // 
        private static string CMD_START { get { return ""; } }
        private static string CMD_END { get { return ""; } }

        public static bool TraceEnabled = false;

        //
        // Public access to shared resources
        //

        /// <summary>
        /// Shared serial port
        /// </summary>
        private static ASCOM.Utilities.Serial SharedSerial
        {
            get
            {
                return ASCOMSerial;
            }
        }

        /// <summary>
        /// number of connections to the shared serial port
        /// </summary>
        public static string COMPortName
        {
            get
            {
                return SharedSerial.PortName;
            }
            set
            {
                if (SharedSerial.Connected && SharedSerial.PortName != value)
                {
                    LogMessage("SharedResources::COMPortName", "NotSupportedException: Serial port already connected");
                    throw new NotSupportedException("Serial port already connected");
                }

                SharedSerial.PortName = value;
                LogMessage("SharedResources::COMPortName", "New serial port name: "+ value);
            }
        }

        /// <summary>
        /// number of connections to the shared serial port
        /// </summary>
        public static int Connections
        {
            get
            {
                //LogMessage("Connections", "ConnectedClients: {0}", ConnectedClients);
                return ConnectedClients;
            }
            set
            {
                ConnectedClients = value;
                //LogMessage("Connections", "ConnectedClients new value: {0}", ConnectedClients);
            }
        }

        public static bool Connected
        {
            get
            {
                LogMessage("SharedResources::Connected", "SharedSerial.Connected: "+ SharedSerial.Connected.ToString());
                return SharedSerial.Connected;
            }
            set
            {
                if (SharedSerial.Connected == value) { return; }

                // Check if we are the first client using the shared serial
                if (value)
                {
                    LogMessage("SharedResources::Connected", "New connection request");

                    if (Connections == 0)
                    {
                        LogMessage("SharedResources::Connected", "This is the first client");

                        // Check for a valid serial port name
                        if (Array.IndexOf(SharedSerial.AvailableCOMPorts, SharedSerial.PortName) > -1)
                        {
                            lock (lockObject)
                            {
                                // Sets serial parameters
                                SharedSerial.Speed = SerialSpeed.ps115200;
                                SharedSerial.ReceiveTimeout = 5;
                                SharedSerial.Connected = true;

                                Connections++;
                                LogMessage("SharedResources::Connected", "Connected successfully");
                            }
                        }
                        else
                        {
                            LogMessage("SharedResources::Connected", "Connection aborted, invalid serial port name");
                        }
                    }
                    else
                    {
                        lock (lockObject)
                        {
                            Connections++;
                            LogMessage("SharedResources::Connected", "Connected successfully");
                        }
                    }
                }
                else
                {
                    LogMessage("SharedResources::Connected", "Disconnect request");

                    lock (lockObject)
                    {
                        // Check if we are the last client connected
                        if (Connections == 1)
                        {
                            SharedSerial.ClearBuffers();
                            SharedSerial.Connected = false;
                            LogMessage("SharedResources::Connected", "This is the last client, disconnecting the serial port");
                        }
                        else
                        {
                            LogMessage("SharedResources::Connected", "Serial connection kept alive");
                        }

                        Connections--;
                        LogMessage("SharedResources::Connected", "Disconnected successfully");
                    }
                }
            }
        }

        public static Protocol.Response GetStatus()
        {
            LogMessage("SharedResources::GetStatus", "Getting status");
            string res = SendSerialMessage(GET_STATUS);
            LogMessage("SharedResources::GetStatus", "Received response: " + res);
            if (!String.IsNullOrEmpty(res))
            {
                Protocol.Response response = JsonConvert.DeserializeObject<Protocol.Response>(res);
                return response;
            }
            else
            {
                LogMessage("SharedResources::GetStatus", "Empty response");
            }
            return null;
        }

        public static Protocol.Response GetTemperature()
        {
            LogMessage("SharedResources::GetTemperature", "Getting external temperature");
            string res = SendSerialMessage(GET_EXT_T);
            LogMessage("SharedResources::GetTemperature", "Received response: " + res);
            if (!String.IsNullOrEmpty(res))
            {
                Protocol.Response response = JsonConvert.DeserializeObject<Protocol.Response>(res);
                return response;
            }
            else
            {
                LogMessage("SharedResources::GetTemperature", "Empty response");
            }
            return null;
        }

        public static bool Move(int position)
        {
            LogMessage("SharedResources::Move", "Moving to " + position);
            string res = SendSerialMessage(GOTO.Replace("#POS#", position.ToString()));
            Protocol.Response response = JsonConvert.DeserializeObject<Protocol.Response>(res);
            LogMessage("SharedResources::GetTemperature", "Received response: " + res);
            if (response != null)
            {
                if (response.res.cmd.MOT1.GOTO.ToLower() == "done") return true;
                return false;
            }
            else
            {
                LogMessage("SharedResources::GetTemperature", "Empty response");
            }
            return false;
        }

        public static void Stop()
        {
            LogMessage("SharedResources::Stop", "Stopping");
            string res = SendSerialMessage(STOP);
        }

        public static string SendSerialMessage(string message)
        {
            string retval = String.Empty;

            if (SharedSerial.Connected)
            {
                lock (lockObject)
                {
                    SharedSerial.ClearBuffers();
                    SharedSerial.Transmit(CMD_START + message + CMD_END);
                    LogMessage("SharedResources::SendSerialMessage", "Message: "+ CMD_START + message + CMD_END);

                    try
                    {
                        retval = Receive();
                        LogMessage("SharedResources::SendSerialMessage", "Message received: "+ retval);
                    }
                    catch (Exception e)
                    {
                        LogMessage("SharedResources::SendSerialMessage", "Serial timeout exception while receiving data: " + e.Message + "\n" + e.StackTrace);
                    }

                    LogMessage("SharedResources::SendSerialMessage", "Message sent: "+ CMD_START + message + CMD_END + " received: "+ retval);
                }
            }
            else
            {
                //throw new NotConnectedException("SendSerialMessage");
                LogMessage("SharedResources::SendSerialMessage", "NotConnectedException");
            }

            return retval;
        }

        public static void SendSerialMessageBlind(string message)
        {
            if (SharedSerial.Connected)
            {
                lock (lockObject)
                {
                    SharedSerial.Transmit(CMD_START + message + CMD_END);
                    LogMessage("SharedResources::SendSerialMessage", "Message: "+ CMD_START + message + CMD_END);
                }
            }
            else
            {
                //throw new NotConnectedException("SendSerialMessageBlind");
                LogMessage("SharedResources::SendSerialMessageBlind", "NotConnectedException");
            }
        }

        public static string Receive()
        {
            string temp = "";
            try
            {
                string s = SharedSerial.Receive();
                while (!String.IsNullOrEmpty(s))
                {
                    temp += s;
                    s = SharedSerial.Receive();
                }
            }
            catch (Exception) { }
            return temp;
        }

        internal static void LogMessage(string identifier, string message)
        {
            LogMessage(DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + ": " + identifier + ": " + message);
        }

        static readonly object fileLockObject = new object();
        internal static void LogMessage(string message)
        {
            try
            {
                lock (fileLockObject)
                {
                    if (TraceEnabled) File.AppendAllText(@"c:\temp\EsattoFocuser.log", message + "\n");
                }
            }
            catch(Exception e)
            {
                // Swallow it.
            }
        }
    }
}
