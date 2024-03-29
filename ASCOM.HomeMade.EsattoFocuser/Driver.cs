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
#define Focuser

using ASCOM;
using ASCOM.Astrometry;
using ASCOM.Astrometry.AstroUtils;
using ASCOM.DeviceInterface;
using ASCOM.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ASCOM.HomeMade
{
    /// <summary>
    /// ASCOM Focuser Driver for HomeMade.
    /// </summary>
    [Guid("835503b2-4cbd-4675-b5aa-40f2af06180a")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId(Focuser.driverID)]
    public class Focuser : IFocuserV2
    {
        /// <summary>
        /// ASCOM DeviceID (COM ProgID) for this driver.
        /// The DeviceID is used by ASCOM applications to load the driver at runtime.
        /// </summary>
        internal const string driverID = "ASCOM.HomeMade.EsattoFocuser";

        /// <summary>
        /// Driver description that displays in the ASCOM Chooser.
        /// </summary>
        private static string driverDescription = "ASCOM HomeMade Driver for Esatto";

        internal static string comPortProfileName = "COM Port"; // Constants used for Profile persistence
        internal static string comPortDefault = "COM1";
        internal static string traceStateProfileName = "Trace Level";
        internal static string traceStateDefault = "false";

        internal static string comPort; // Variables to hold the currrent device configuration

        /// <summary>
        /// Private variable to hold the connected state
        /// </summary>
        private bool connectedState;
        private bool isMoving = false;

        private Thread statusThread = null;
        private DeviceStatus deviceStatus = null;
        public bool stopGetStatus = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="HomeMade"/> class.
        /// Must be public for COM registration.
        /// </summary>
        public Focuser()
        {
            ReadProfile(); // Read device configuration from the ASCOM Profile store
            SharedResources.LogMessage("Focuser", "Starting initialisation");

            SharedResources.LogMessage("Focuser", "Port is " + comPort);

            connectedState = false; // Initialise connected to false

            SharedResources.LogMessage("Focuser", "Completed initialisation");
        }


        //
        // PUBLIC COM INTERFACE IFocuserV2 IMPLEMENTATION
        //

        #region Common properties and methods.

        /// <summary>
        /// Displays the Setup Dialog form.
        /// If the user clicks the OK button to dismiss the form, then
        /// the new settings are saved, otherwise the old values are reloaded.
        /// THIS IS THE ONLY PLACE WHERE SHOWING USER INTERFACE IS ALLOWED!
        /// </summary>
        public void SetupDialog()
        {
            // consider only showing the setup dialog if not connected
            // or call a different dialog if connected
            if (IsConnected)
                System.Windows.Forms.MessageBox.Show("Already connected, just press OK");

            using (SetupDialogForm F = new SetupDialogForm())
            {
                var result = F.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    WriteProfile(); // Persist device configuration values to the ASCOM Profile store
                }
            }
        }

        public ArrayList SupportedActions
        {
            get
            {
                SharedResources.LogMessage("SupportedActions Get", "Returning empty arraylist");
                return new ArrayList();
            }
        }

        public string Action(string actionName, string actionParameters)
        {
            LogMessage("", "Action {0}, parameters {1} not implemented", actionName, actionParameters);
            throw new ASCOM.ActionNotImplementedException("Action " + actionName + " is not implemented by this driver");
        }

        public void CommandBlind(string command, bool raw)
        {
            //CheckConnected("CommandBlind");
            SharedResources.LogMessage("CommandBlind", "Command: " + command + ", raw: " + raw.ToString());

            if (raw)
            {
                SharedResources.SendSerialMessageBlind(command);
            }
            else
            {
                switch (command)
                {
                    default:
                        throw new MethodNotImplementedException("MethodNotImplementedException: CommandBlind");
                }
            }
        }

        public bool CommandBool(string command, bool raw)
        {
            //CheckConnected("CommandBool");
            //SharedResources.LogMessage(MotorID.ToString() + "::CommandBool", "Command: {0}, raw: {1}", command, raw.ToString());

            if (raw)
            {
                return Convert.ToBoolean(CommandString(command, true));
            }
            else
            {
                switch (command)
                {
                    default:
                        throw new MethodNotImplementedException("MethodNotImplementedException: CommandBool");
                }
            }
        }


        public string CommandString(string command, bool raw)
        {
            //CheckConnected("CommandString");
            //SharedResources.LogMessage(MotorID.ToString() + "::CommandString", "Command: {0}, raw: {1}", command, raw.ToString());

            if (raw)
            {
                return SharedResources.SendSerialMessage(command);
            }
            else
            {
                switch (command)
                {
                    default:
                        throw new MethodNotImplementedException("MethodNotImplementedException: CommandString");
                }
            }
        }

        public void Dispose()
        {
            // Clean up the tracelogger and util objects
        }

        public bool Connected
        {
            get
            {
                return connectedState;
            }
            set
            {
                if (value == connectedState) { return; }

                if (value)
                {
                    SharedResources.LogMessage("Connected", "Starting a new serial connection");

                    // Check if we are the first client using the shared serial
                    if (SharedResources.Connections == 0)
                    {
                        SharedResources.LogMessage("Connected", "We are the first connected client, setting serial port name");
                    }

                    SharedResources.COMPortName = comPort;
                    SharedResources.Connected = true;

                    // Check if we are the first client using the shared serial
                    InitDevice();

                    statusThread = new Thread(new ThreadStart(() =>
                    {
                        GetStatusThreadJob();
                    }));
                    statusThread.IsBackground = true;
                    statusThread.Start();

                    connectedState = true;
                    SharedResources.LogMessage("Connected", "Connected successfully");
                }
                else
                {
                    SharedResources.LogMessage("Connected", "Disconnecting the serial connection");
                    stopGetStatus = true;
                    statusThread = null;
                    connectedState = false;
                    SharedResources.Connected = false;
                    SharedResources.LogMessage("Connected", "Disconnected successfully");
                }
            }
        }

        private void GetStatusThreadJob()
        {
            while (!stopGetStatus)
            {
                GetStatusAndTemperature();
                Thread.Sleep(500);
            }
        }

        private void GetStatusAndTemperature()
        {
            try
            {
                SharedResources.LogMessage("GetStatusAndTemperature", "Getting temperature");
                DeviceStatus temp = ParseTemperature(SharedResources.GetTemperature());
                SharedResources.LogMessage("GetStatusAndTemperature", "Getting status");
                DeviceStatus status = ParseStatus(SharedResources.GetStatus());
                if (status != null)
                {
                    if (temp != null)
                    {
                        status.externalTemperature = temp.externalTemperature;
                    }
                    else
                    {
                        if (deviceStatus != null) status.externalTemperature = deviceStatus.externalTemperature;
                    }
                    if (status.speed > 0) isMoving = true;
                    else isMoving = false;
                }
                if (status != null) deviceStatus = status;
            }
            catch (Exception e)
            {
                SharedResources.LogMessage("GetStatusAndTemperature", "Error: " + e.Message + "\n" + e.StackTrace);
            }
        }

        private DeviceStatus ParseStatus(Protocol.Response response)
        {
            DeviceStatus status = new DeviceStatus();
            if (response != null)
            {
                status.OK = true;
                status.connected = true;
                if (response.res != null)
                    if (response.res.get != null)
                        if (response.res.get.MOT1 != null)
                        {
                            status.maxPosition = response.res.get.MOT1.CAL_MAXPOS;
                            status.position = response.res.get.MOT1.ABS_POS;
                            status.maxStep = response.res.get.MOT1.CAL_MAXPOS;
                            status.internalTemperature = double.Parse(response.res.get.MOT1.NTC_T.Replace(',', '.'), CultureInfo.InvariantCulture);
                            status.speed = response.res.get.MOT1.SPEED;
                            status.busy = response.res.get.MOT1.STATUS.BUSY == 1;
                        }
            }
            else
            {
                return null;
            }
            return status;
        }

        private DeviceStatus ParseTemperature(Protocol.Response response)
        {
            DeviceStatus status = new DeviceStatus();
            if (response != null)
            {
                status.OK = true;
                status.connected = true;
                if (response.res != null)
                    if (response.res.get != null)
                    {
                        status.externalTemperature = response.res.get.EXT_T;
                    }
            }
            else
            {
                return null;
            }
            return status;
        }
        private void InitDevice()
        {
            try
            {
                while (deviceStatus == null)
                {
                    GetStatusAndTemperature();
                    Thread.Sleep(300);
                }
            }
            catch (Exception e)
            {
                LogMessage("InitDevice", "Error: " + e.Message + "\n" + e.StackTrace);
                throw new ASCOM.DriverException(e.Message + "\n" + e.StackTrace);
            }
        }

        public string Description
        {
            // TODO customise this device description
            get
            {
                SharedResources.LogMessage("Description Get", driverDescription);
                return driverDescription;
            }
        }

        public string DriverInfo
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                // TODO customise this driver description
                string driverInfo = "Esatto ASCOM driver. Version: " + String.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", version.Major, version.Minor, version.Revision);
                SharedResources.LogMessage("DriverInfo Get", driverInfo);
                return driverInfo;
            }
        }

        public string DriverVersion
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string driverVersion = String.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", version.Major, version.Minor, version.Revision);
                SharedResources.LogMessage("DriverVersion Get", driverVersion);
                return driverVersion;
            }
        }

        public short InterfaceVersion
        {
            // set by the driver wizard
            get
            {
                LogMessage("InterfaceVersion Get", "2");
                return Convert.ToInt16("2");
            }
        }

        public string Name
        {
            get
            {
                string name = "HomeMade EsattoFocuser";
                SharedResources.LogMessage("Name Get", name);
                return name;
            }
        }

        #endregion

        #region IFocuser Implementation

        public bool Absolute
        {
            get
            {
                SharedResources.LogMessage("Absolute Get", true.ToString());
                return true; // This is an absolute focuser
            }
        }

        public void Halt()
        {
            SharedResources.LogMessage("Halt", "Not implemented");
            CheckConnected("Halt");
            SharedResources.Stop();
            SharedResources.LogMessage("Halt", "Stop motor movement");
        }

        public bool IsMoving
        {
            get
            {
                SharedResources.LogMessage("IsMoving", "Checking is focuser is moving");
                CheckConnected("IsMoving");

                SharedResources.LogMessage("IsMoving", "Focuser is " + (isMoving ? "" : "not") + " moving");
                return isMoving;
            }
        }

        public bool Link
        {
            get
            {
                SharedResources.LogMessage("Link Get", this.Connected.ToString());
                return this.Connected; // Direct function to the connected method, the Link method is just here for backwards compatibility
            }
            set
            {
                SharedResources.LogMessage("Link Set", value.ToString());
                this.Connected = value; // Direct function to the connected method, the Link method is just here for backwards compatibility
            }
        }

        public int MaxIncrement
        {
            get
            {
                SharedResources.LogMessage("MaxIncrement Get", deviceStatus.maxStep.ToString());
                return deviceStatus.maxStep; // Maximum change in one move
            }
        }

        public int MaxStep
        {
            get
            {
                SharedResources.LogMessage("MaxStep Get", deviceStatus.maxPosition.ToString());
                return deviceStatus.maxPosition; // Maximum extent of the focuser, so position range is 0 to 10,000
            }
        }

        public void Move(int Position)
        {
            SharedResources.LogMessage("Move", "Move motor to " + Position);

            CheckConnected("Move");

            if (Position > deviceStatus.maxPosition) { Position = deviceStatus.maxPosition; }
            if (Position == deviceStatus.position) return;

            isMoving = true;
            if (!SharedResources.Move(Position))
            {
                isMoving = false;
                SharedResources.LogMessage("Move", "Move error");
            }

            SharedResources.LogMessage("Move", "Move order done");
        }


        public int Position
        {
            get
            {
                SharedResources.LogMessage("Position", "Getting current position");
                CheckConnected("Position");

                return deviceStatus.position;
            }
        }


        public double StepSize
        {
            get
            {
                SharedResources.LogMessage("StepSize Get", "Not implemented");
                throw new ASCOM.PropertyNotImplementedException("StepSize", false);
            }
        }

        public bool TempComp
        {
            get
            {
                SharedResources.LogMessage("TempComp Get", false.ToString());
                return false;
            }
            set
            {
                SharedResources.LogMessage("TempComp Set", "Not implemented");
                throw new ASCOM.PropertyNotImplementedException("TempComp", false);
            }
        }

        public bool TempCompAvailable
        {
            get
            {
                SharedResources.LogMessage("TempCompAvailable Get", false.ToString());
                return false; // Temperature compensation is not available in this driver
            }
        }

        public double Temperature
        {
            get
            {
                SharedResources.LogMessage("Temperature Get", true.ToString());
                CheckConnected("Temperature");

                SharedResources.LogMessage("Temperature Get", "Temperature is " + deviceStatus.externalTemperature);
                return deviceStatus.externalTemperature;
            }
        }

        #endregion

        #region Private properties and methods
        // here are some useful properties and methods that can be used as required
        // to help with driver development

        #region ASCOM Registration

        // Register or unregister driver for ASCOM. This is harmless if already
        // registered or unregistered. 
        //
        /// <summary>
        /// Register or unregister the driver with the ASCOM Platform.
        /// This is harmless if the driver is already registered/unregistered.
        /// </summary>
        /// <param name="bRegister">If <c>true</c>, registers the driver, otherwise unregisters it.</param>
        private static void RegUnregASCOM(bool bRegister)
        {
            using (var P = new ASCOM.Utilities.Profile())
            {
                P.DeviceType = "Focuser";
                if (bRegister)
                {
                    P.Register(driverID, driverDescription);
                }
                else
                {
                    P.Unregister(driverID);
                }
            }
        }

        /// <summary>
        /// This function registers the driver with the ASCOM Chooser and
        /// is called automatically whenever this class is registered for COM Interop.
        /// </summary>
        /// <param name="t">Type of the class being registered, not used.</param>
        /// <remarks>
        /// This method typically runs in two distinct situations:
        /// <list type="numbered">
        /// <item>
        /// In Visual Studio, when the project is successfully built.
        /// For this to work correctly, the option <c>Register for COM Interop</c>
        /// must be enabled in the project settings.
        /// </item>
        /// <item>During setup, when the installer registers the assembly for COM Interop.</item>
        /// </list>
        /// This technique should mean that it is never necessary to manually register a driver with ASCOM.
        /// </remarks>
        [ComRegisterFunction]
        public static void RegisterASCOM(Type t)
        {
            RegUnregASCOM(true);
        }

        /// <summary>
        /// This function unregisters the driver from the ASCOM Chooser and
        /// is called automatically whenever this class is unregistered from COM Interop.
        /// </summary>
        /// <param name="t">Type of the class being registered, not used.</param>
        /// <remarks>
        /// This method typically runs in two distinct situations:
        /// <list type="numbered">
        /// <item>
        /// In Visual Studio, when the project is cleaned or prior to rebuilding.
        /// For this to work correctly, the option <c>Register for COM Interop</c>
        /// must be enabled in the project settings.
        /// </item>
        /// <item>During uninstall, when the installer unregisters the assembly from COM Interop.</item>
        /// </list>
        /// This technique should mean that it is never necessary to manually unregister a driver from ASCOM.
        /// </remarks>
        [ComUnregisterFunction]
        public static void UnregisterASCOM(Type t)
        {
            RegUnregASCOM(false);
        }

        #endregion

        /// <summary>
        /// Returns true if there is a valid connection to the driver hardware
        /// </summary>
        private bool IsConnected
        {
            get
            {
                // TODO check that the driver hardware connection exists and is connected to the hardware
                return connectedState;
            }
        }

        /// <summary>
        /// Use this function to throw an exception if we aren't connected to the hardware
        /// </summary>
        /// <param name="message"></param>
        private void CheckConnected(string message)
        {
            if (!IsConnected)
            {
                throw new ASCOM.NotConnectedException(message);
            }
        }

        /// <summary>
        /// Read the device configuration from the ASCOM Profile store
        /// </summary>
        internal void ReadProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Focuser";
                SharedResources.TraceEnabled = Convert.ToBoolean(driverProfile.GetValue(driverID, traceStateProfileName, string.Empty, traceStateDefault));
                comPort = driverProfile.GetValue(driverID, comPortProfileName, string.Empty, comPortDefault);
            }
        }

        /// <summary>
        /// Write the device configuration to the  ASCOM  Profile store
        /// </summary>
        internal void WriteProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Focuser";
                driverProfile.WriteValue(driverID, traceStateProfileName, SharedResources.TraceEnabled.ToString());
                driverProfile.WriteValue(driverID, comPortProfileName, comPort.ToString());
            }
        }

        /// <summary>
        /// Log helper function that takes formatted strings and arguments
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        internal static void LogMessage(string identifier, string message, params object[] args)
        {
            var msg = string.Format(message, args);
            SharedResources.LogMessage(identifier, msg);
        }
        #endregion
    }
}
