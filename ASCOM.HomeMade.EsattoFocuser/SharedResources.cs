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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ASCOM.HomeMade
{
    public class SharedResources
    {
        private static SharedResources _Instance = null;

        // object used for locking to prevent multiple drivers accessing common code at the same time
        private static readonly Mutex mutex = new Mutex(false);

        // Shared serial port. This will allow multiple drivers to use one single serial port.
        private static readonly ASCOM.Utilities.Serial ASCOMSerial = new ASCOM.Utilities.Serial();

        // Counter for the number of connections to the serial port
        private static int ConnectedClients = 0;

        private static ConcurrentQueue<TimeSpan> _Queue = new ConcurrentQueue<TimeSpan>();
        private static bool _Stop = false;
        private static int _TOOSLOW = 2000;
        private BackgroundWorker _Worker = null;

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
        public static SharedResources Get()
        {
            if (_Instance == null) _Instance = new SharedResources();
            return _Instance;
        }

        /// <summary>
        /// Shared serial port
        /// </summary>
        private ASCOM.Utilities.Serial SharedSerial
        {
            get
            {
                return ASCOMSerial;
            }
        }

        /// <summary>
        /// number of connections to the shared serial port
        /// </summary>
        public string COMPortName
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
                LogMessage("SharedResources::COMPortName", "New serial port name: " + value);
            }
        }

        /// <summary>
        /// number of connections to the shared serial port
        /// </summary>
        public int Connections
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

        public bool Connected
        {
            get
            {
                LogMessage("SharedResources::Connected", "SharedSerial.Connected: " + SharedSerial.Connected.ToString());
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
                            try
                            {
                                mutex.WaitOne();
                                // Sets serial parameters
                                SharedSerial.Speed = SerialSpeed.ps115200;
                                SharedSerial.ReceiveTimeout = 1;
                                SharedSerial.Connected = true;

                                Connections++;
                                LogMessage("SharedResources::Connected", "Connected successfully");

                                LogMessage("SharedResources::Connected", "Starting watchdog");
                                _Worker = new BackgroundWorker();
                                _Worker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.Worker1_DoWork);
                                LogMessage("SharedResources::Connected", "Watchdog started");
                            }
                            catch { }
                            finally { mutex.ReleaseMutex(); }
                        }
                        else
                        {
                            LogMessage("SharedResources::Connected", "Connection aborted, invalid serial port name");
                        }
                    }
                    else
                    {
                        try
                        {
                            mutex.WaitOne();
                            Connections++;
                            LogMessage("SharedResources::Connected", "Connected successfully");
                        }
                        catch { }
                        finally { mutex.ReleaseMutex(); }
                    }
                }
                else
                {
                    LogMessage("SharedResources::Connected", "Disconnect request");

                    try
                    {
                        mutex.WaitOne();
                        // Check if we are the last client connected
                        if (Connections == 1)
                        {
                            _Stop = true;
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
                    catch { }
                    finally { mutex.ReleaseMutex(); }
                }
            }
        }

        public Protocol.Response GetStatus()
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

        public Protocol.Response GetTemperature()
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

        public bool Move(int position)
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

        public void Stop()
        {
            LogMessage("SharedResources::Stop", "Stopping");
            string res = SendSerialMessage(STOP);
        }

        public string SendSerialMessage(string message)
        {
            string retval = String.Empty;

            if (SharedSerial.Connected)
            {
                try
                {
                    mutex.WaitOne();
                    LogMessage("SharedResources::SendSerialMessage", "Message: " + CMD_START + message + CMD_END);
                    SharedSerial.ClearBuffers();
                    DateTime start = DateTime.Now;
                    SharedSerial.Transmit(CMD_START + message + CMD_END);
                    DateTime end = DateTime.Now;
                    LogMessage("SharedResources::SendSerialMessage", "Transmit took " + (end - start).TotalMilliseconds + "ms");


                    try
                    {
                        LogMessage("SharedResources::SendSerialMessage", "Message received: " + retval);
                        start = DateTime.Now;
                        retval = Receive();
                        end = DateTime.Now;
                        LogMessage("SharedResources::SendSerialMessage", "Receive took " + (end - start).TotalMilliseconds + "ms");
                    }
                    catch (Exception e)
                    {
                        LogMessage("SharedResources::SendSerialMessage", "Serial timeout exception while receiving data: " + e.Message + "\n" + e.StackTrace);
                    }
                }
                catch { }
                finally { mutex.ReleaseMutex(); }
            }
            else
            {
                //throw new NotConnectedException("SendSerialMessage");
                LogMessage("SharedResources::SendSerialMessage", "NotConnectedException");
            }

            return retval;
        }

        public void SendSerialMessageBlind(string message)
        {
            if (SharedSerial.Connected)
            {
                try
                {
                    mutex.WaitOne();
                    LogMessage("SharedResources::SendSerialMessage", "Message: " + CMD_START + message + CMD_END);
                    DateTime start = DateTime.Now;
                    SharedSerial.Transmit(CMD_START + message + CMD_END);
                    DateTime end = DateTime.Now;
                    LogMessage("SharedResources::SendSerialMessage", "Transmit took " + (end - start).TotalMilliseconds + "ms");
                }
                catch { }
                finally { mutex.ReleaseMutex(); }
            }
            else
            {
                //throw new NotConnectedException("SendSerialMessageBlind");
                LogMessage("SharedResources::SendSerialMessageBlind", "NotConnectedException");
            }
        }

        public string Receive()
        {
            string temp = "";
            try
            {
                temp = SharedSerial.ReceiveTerminated("\n");
                /*
                string s = SharedSerial.Receive();
                while (!String.IsNullOrEmpty(s))
                {
                    temp += s;
                    s = SharedSerial.Receive();
                }
                */
            }
            catch (Exception) { }
            return temp;
        }

        internal void LogMessage(string identifier, string message)
        {
            LogMessage(DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + ": " + identifier + ": " + message);
        }

        static readonly object fileLockObject = new object();
        internal void LogMessage(string message)
        {
            try
            {
                lock (fileLockObject)
                {
                    if (TraceEnabled) File.AppendAllText(@"c:\temp\EsattoFocuser.log", message + "\n");
                }
            }
            catch (Exception e)
            {
                // Swallow it.
            }
        }

        private void Worker1_DoWork(object sender, DoWorkEventArgs e)
        {
            Watchdog();
        }

        private void Watchdog()
        {
            while (!_Stop)
            {
                if (_Queue.Count > 0)
                {
                    LogMessage("SharedResources::Watchdog", "Dequeueing");
                    _Queue.TryDequeue(out TimeSpan timing);
                    if (timing > TimeSpan.FromMilliseconds(_TOOSLOW))
                    {
                        LogMessage("SharedResources::Watchdog", "Timing is too high");
                        _Queue = new ConcurrentQueue<TimeSpan>();
                        DisconnectReconnect();
                    }
                }
                Thread.Sleep(500);
            }
        }

        private void DisconnectReconnect()
        {
            try
            {
                mutex.WaitOne();
                if (Connections >= 1)
                {
                    LogMessage("SharedResources::DisconnectReconnect", "Disconnecting and reconnecting port");
                    SharedSerial.ClearBuffers();
                    SharedSerial.Connected = false;
                    Thread.Sleep(1000);
                    SharedSerial.Speed = SerialSpeed.ps115200;
                    SharedSerial.ReceiveTimeout = 1;
                    SharedSerial.Connected = true;
                    LogMessage("SharedResources::DisconnectReconnect", "Port reconnected");
                }
            }
            catch { }
            finally { mutex.ReleaseMutex(); }
        }
    }
}
