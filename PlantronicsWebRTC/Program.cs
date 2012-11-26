using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Interop.Plantronics;
using Fleck;
using System.Collections;
using System.Runtime.InteropServices;
using Procurios.Public;

namespace Plantronics.UC.WebRTCDemo
{
    class Program
    {
        #region Plantronics Definitions
        static ISessionCOMManager m_sessionComManager = null;
        static IComSession m_comSession = null;
        static IDevice m_activeDevice = null;

        static ISessionCOMManagerEvents_Event m_sessionManagerEvents;
        static ICOMCallEvents_Event m_sessionEvents;
        static IDeviceCOMEvents_Event m_deviceComEvents;
        static IDeviceListenerCOMEvents_Event m_deviceListenerEvents;
        static int callId = 0;
        #endregion

        static IWebSocketServer server = null;
        static List<IWebSocketConnection> allSockets = null;
        static Hashtable offer = null;
        const String WEB_SOCKET_SERVICE_URL = "ws://localhost:8888/plantronics";


        static void Main(string[] args)
        {
            InitializePlantronics();
            
            InitializeWebSockets();

            var input = Console.ReadLine();
            while (input != "exit")
            {
                input = Console.ReadLine();
            }
            CleanUp();
        }

        /**
         * Remove all of the event listeners and release the COM objects
         */
        private static void CleanUp()
        {
            DetachDevice();

            if (m_comSession != null)
            {
                if (m_sessionEvents != null)
                {
                    // release session events
                    Marshal.ReleaseComObject(m_sessionEvents);
                    m_sessionEvents = null;
                }
                // unregister session
                m_sessionComManager.UnRegister(m_comSession);
                Marshal.ReleaseComObject(m_comSession);
                m_comSession = null;
            }
            if (m_sessionComManager != null)
            {
                Marshal.ReleaseComObject(m_sessionComManager);
                m_sessionComManager = null;
            }

            allSockets.Clear();

            if (server != null)
            {
                server.Dispose();
            }
        }
        #region WebSockets Methods
        //Initializes the web socket server that listens for incoming requests
        private static void InitializeWebSockets()
        {
            allSockets = new List<IWebSocketConnection>();
            server = new WebSocketServer(WEB_SOCKET_SERVICE_URL);

            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    Console.WriteLine("Connection Opened");
                    offer = null;
                    allSockets.Add(socket);
                };

                socket.OnClose = () =>
                {
                    Console.WriteLine("Connection Closed!");
                    offer = null;
                    allSockets.Remove(socket);
                };
                socket.OnMessage = message =>
                {
                    Console.WriteLine(message);
                    HandleMessage(message);
                };
            });
        }

        //Sends a message out to all websocket connections
        private static void BroadcastMessage(PlantronicsMessage message)
        {
            String json = JSON.JsonEncode(message.GetObjectAsHashtable());

            Console.WriteLine("Sending Message: " + json);
            foreach (var socket in allSockets.ToList())
            {
                socket.Send(json);
            }
        }

        //Responsible for processing messages that have arrived via a websocket connection
        private static void HandleMessage(String message)
        {
            if (m_activeDevice == null)
            {
                Console.WriteLine("no active device, ignoring message");
                return;
            }

            PlantronicsMessage m = PlantronicsMessage.ParseMessageFromJSON(message);
            if (m.Type == PlantronicsMessage.MESSAGE_TYPE_SETTING)
            {
                if (m.Id == PlantronicsMessage.SETTING_HEADSET_INFO)
                {
                    PlantronicsMessage response = new PlantronicsMessage(PlantronicsMessage.MESSAGE_TYPE_SETTING, PlantronicsMessage.SETTING_HEADSET_INFO);
                    Device device = new Device();
                    device.InternalName = m_activeDevice.InternalName;
                    device.ManufacturerName = m_activeDevice.ManufacturerName;
                    device.ProductName = m_activeDevice.ProductName;
                    device.VendorId = String.Format("0x{0:X}", m_activeDevice.VendorID);
                    device.ProductId = String.Format("0x{0:X}", m_activeDevice.ProductID);
                    device.VersionNumber = m_activeDevice.VersionNumber;

                    //TODO fixup CAB - make this eventually come from Spokes or a dictionary lookup
                    device.NumberOfChannels = 1;
                    device.SampleRate = 16000;

                    response.AddToPayload("device", device);
                    BroadcastMessage(response);
                }

            }
            else if (m.Type == PlantronicsMessage.MESSAGE_TYPE_COMMAND)
            {
                if (m.Id == PlantronicsMessage.COMMAND_RING_ON)
                {
                    Console.WriteLine("Ringing headset");
                    Object o = null;
                    if (!m.Payload.TryGetValue("offer", out o))
                    {
                        Console.WriteLine("Unable to get caller id from the message skipping headset ring operation");
                        return;
                    }
                    offer = o as Hashtable;
                    ContactCOM contact = new ContactCOM() { Name = offer["from"] as String };
                    callId = (int)(double)m.Payload["callId"];
                    CallCOM call = new CallCOM() { Id = callId };
                    m_comSession.CallCommand.IncomingCall(call, contact, RingTone.RingTone_Unknown, AudioRoute.AudioRoute_ToHeadset);

                }
                else if (m.Id == PlantronicsMessage.COMMAND_HANG_UP)
                {
                    CallCOM call = new CallCOM() { Id = callId };
                    m_comSession.CallCommand.TerminateCall(call);

                }
                else if (m.Id == PlantronicsMessage.COMMAND_RING_OFF)
                {
                    Console.WriteLine("Turning ring off headset");
                    m_activeDevice.HostCommand.Ring(false);

                }
                else if (m.Id == PlantronicsMessage.COMMAND_MUTE_ON)
                {
                    Console.WriteLine("Muting headset");
                    m_activeDevice.DeviceListener.Mute = true;

                }
                else if (m.Id == PlantronicsMessage.COMMAND_MUTE_OFF)
                {
                    Console.WriteLine("Unmuting headset");
                    m_activeDevice.DeviceListener.Mute = false;

                }
            }
            else if (m.Type == PlantronicsMessage.MESSAGE_TYPE_EVENT)
            {
                Console.WriteLine("Event message received");
            }

        }
        #endregion

        #region Plantronics Initialization
        private static void InitializePlantronics()
        {
            try
            {
                m_sessionComManager = new SessionComManagerClass();
                Console.WriteLine("Session Manager created");
                m_sessionManagerEvents = m_sessionComManager as ISessionCOMManagerEvents_Event;
                if (m_sessionManagerEvents != null)
                {
                    m_sessionManagerEvents.DeviceStateChanged += m_sessionComManager_DeviceStateChanged;
                    Console.WriteLine("Attached to session manager events");
                }
                else
                {
                    Console.WriteLine("Error: Unable to attach to session manager events");
                }
                ////////////////////////////////////////////////////////////////////////////////////////
                // register session to spokes
                m_comSession = m_sessionComManager.Register("COM Session");
                if (m_comSession != null)
                {
                    // attach to session call events
                    m_sessionEvents = m_comSession.CallEvents as ICOMCallEvents_Event;
                    if (m_sessionEvents != null)
                    {
                        m_sessionEvents.CallStateChanged += m_sessionEvents_CallStateChanged;
                        Console.WriteLine("Attached to session call events");
                    }
                    else
                    {
                        Console.WriteLine("Error: Unable to attach to session call events");
                    }

                    // Attach to active device and print all device information
                    m_activeDevice = m_comSession.ActiveDevice;
                    
                    AttachDevice();
                }
                else
                    Console.WriteLine("Error: Unable to register session");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                Console.Read();
            }
        }

        // attach to device events
        static private void AttachDevice()
        {
            m_activeDevice = m_comSession.ActiveDevice;
            if (m_activeDevice != null)
            {
                m_deviceComEvents = m_activeDevice.DeviceEvents as IDeviceCOMEvents_Event;

                if (m_deviceComEvents != null)
                {
                    // Attach to device events
                    Console.WriteLine("Attached to device events");
                }
                else
                {
                    Console.WriteLine("Error: unable to attach to device events");
                }
                m_deviceListenerEvents = m_activeDevice.DeviceListener as IDeviceListenerCOMEvents_Event;

                if (m_deviceListenerEvents != null)
                {

                    // Attach to device listener events
                    m_deviceListenerEvents.ATDStateChanged += m_deviceListenerEvents_Handler;
                    m_deviceListenerEvents.BaseButtonPressed += m_deviceListenerEvents_Handler;
                    m_deviceListenerEvents.BaseStateChanged += m_deviceListenerEvents_Handler;
                    m_deviceListenerEvents.HeadsetButtonPressed += m_deviceButtonEvents;
                    m_deviceListenerEvents.HeadsetStateChanged += m_deviceListenerEvents_Handler;
                    m_deviceListenerEvents.HeadsetStateChanged += m_deviceListenerEvents_HandlerMethods;

                    GetLastDonnedStatus();
                    RegisterForProximity(true);
                    Console.WriteLine("Attach to device listener events");
                }
                else
                {
                    Console.WriteLine("Error: unable to attach to device listener events");
                }

                Console.WriteLine("Attached to device");
            }

        }

        //Get last donned status of device when application first runs
        private static void GetLastDonnedStatus()
        {
            try
            {
                IHostCommandExt m_commandExt = m_activeDevice.HostCommand as IHostCommandExt;
                if (m_commandExt != null)
                {
                    HeadsetState laststate = m_commandExt.HeadsetState;
                }
            }
            catch (Exception e)
            {
                Console.Out.WriteLine("Other Exception in GetLastDonnedStatus(): " + e.ToString());
            }
        }

        // detach from device events
        static void DetachDevice()
        {
            if (m_activeDevice != null)
            {
                if (m_deviceComEvents != null)
                {
                    // unregister device event handlers
                    m_deviceListenerEvents.HeadsetButtonPressed -= m_deviceButtonEvents;
                  

                    Marshal.ReleaseComObject(m_deviceComEvents);
                    m_deviceComEvents = null;
                }
                if (m_deviceListenerEvents != null)
                {
                    // unregister device listener event handlers
                    m_deviceListenerEvents.ATDStateChanged -= m_deviceListenerEvents_Handler;
                    m_deviceListenerEvents.BaseButtonPressed -= m_deviceListenerEvents_Handler;
                    m_deviceListenerEvents.BaseStateChanged -= m_deviceListenerEvents_Handler;
                    m_deviceListenerEvents.HeadsetButtonPressed -= m_deviceListenerEvents_Handler;
                    m_deviceListenerEvents.HeadsetStateChanged -= m_deviceListenerEvents_Handler;
                    m_deviceListenerEvents.HeadsetStateChanged -= m_deviceListenerEvents_HandlerMethods;

                    RegisterForProximity(false);

                    Marshal.ReleaseComObject(m_deviceListenerEvents);
                    m_deviceListenerEvents = null;
                }

                Marshal.ReleaseComObject(m_activeDevice);
                m_activeDevice = null;

                Console.WriteLine("Detached from device");
            }
        }

        #endregion


        #region Plantronics Event Handlers

        // print session events
        static void m_sessionEvents_CallStateChanged(object sender, _CallStateEventArgs e)
        {
            string id = e.CallId != null ? e.CallId.Id.ToString() : "none";
            IHostCommandExt hostCommandExt = m_activeDevice.HostCommand as IHostCommandExt;
            //the user has accepted the call
            if (e.Action.Equals(CallState.CallState_AcceptCall))
            {

                Console.WriteLine("Call has been answered by headset - sending event to web browser, audio link state = " + hostCommandExt.AudioLinkState);
                PlantronicsMessage m = new PlantronicsMessage(PlantronicsMessage.MESSAGE_TYPE_EVENT, PlantronicsMessage.EVENT_ACCEPT_CALL);
                m.AddToPayload("offer", offer);
                BroadcastMessage(m);

            }
            else if (e.Action.Equals(CallState.CallState_TerminateCall))
            {
                Console.WriteLine("Call has been terminated, killing audio channel");
                PlantronicsMessage m = new PlantronicsMessage(PlantronicsMessage.MESSAGE_TYPE_EVENT, PlantronicsMessage.EVENT_CALL_TERMINATE);
                BroadcastMessage(m);
                offer = null;
                callId = 0;

            }

        }

        static public void m_deviceButtonEvents(object sender, _DeviceListenerEventArgs e)
        {
            PlantronicsMessage eventMessage = new PlantronicsMessage(PlantronicsMessage.MESSAGE_TYPE_EVENT, PlantronicsMessage.EVENT_BUTTON_PRESS);
            switch (e.HeadsetButtonPressed)
            {
                case HeadsetButton.HeadsetButton_Talk:
                    //WebSocket.buttonPressResponse();
                    eventMessage.AddToPayload("buttonName", "talk");
                    BroadcastMessage(eventMessage);
                    break;
                case HeadsetButton.HeadsetButton_Mute:
                    eventMessage.AddToPayload("buttonName", "mute");
                    BroadcastMessage(eventMessage);
                    break;
                case HeadsetButton.HeadsetButton_VolumeUp:
                    eventMessage.AddToPayload("buttonName", "volumeUp");
                    BroadcastMessage(eventMessage);
                    break;
                case HeadsetButton.HeadsetButton_VolumeDown:
                    eventMessage.AddToPayload("buttonName", "volumeDown");
                    BroadcastMessage(eventMessage);
                    break;
                case HeadsetButton.HeadsetButton_MuteHeld:
                    eventMessage.AddToPayload("buttonName", "muteHeld");
                    BroadcastMessage(eventMessage);
                    break;
                case HeadsetButton.HeadsetButton_VolumeUpHeld:
                    eventMessage.AddToPayload("buttonName", "volumeUpHeld");
                    BroadcastMessage(eventMessage);
                    break;
                case HeadsetButton.HeadsetButton_VolumeDownHeld:
                    eventMessage.AddToPayload("buttonName", "volumeDownHeld");
                    BroadcastMessage(eventMessage);
                    break;
                case HeadsetButton.HeadsetButton_Flash:
                    eventMessage.AddToPayload("buttonName", "flash");
                    BroadcastMessage(eventMessage);
                    break;
            }
        }
        static void m_deviceListenerEvents_HandlerMethods(object sender, _DeviceListenerEventArgs e)
        {
            PlantronicsMessage eventMessage = null;
            switch (e.DeviceEventType)
            {
                case DeviceEventType.DeviceEventType_HeadsetStateChanged:
                    switch (e.HeadsetStateChange)
                    {
                        case HeadsetStateChange.HeadsetStateChange_Don:
                            eventMessage = new PlantronicsMessage(PlantronicsMessage.MESSAGE_TYPE_EVENT, PlantronicsMessage.EVENT_WEAR_STATE_CHANGED);
                            eventMessage.Payload.Add("worn", "true");
                            BroadcastMessage(eventMessage);
                            break;
                        case HeadsetStateChange.HeadsetStateChange_Doff:
                            eventMessage = new PlantronicsMessage(PlantronicsMessage.MESSAGE_TYPE_EVENT, PlantronicsMessage.EVENT_WEAR_STATE_CHANGED);
                            eventMessage.Payload.Add("worn", "false");
                            BroadcastMessage(eventMessage);
                            break;
                        case HeadsetStateChange.HeadsetStateChange_Near:
                            eventMessage = new PlantronicsMessage(PlantronicsMessage.MESSAGE_TYPE_EVENT, PlantronicsMessage.EVENT_PROXIMITY);
                            eventMessage.Payload.Add("proximity", "near");
                            BroadcastMessage(eventMessage);
                            break;
                        case HeadsetStateChange.HeadsetStateChange_Far:
                            eventMessage = new PlantronicsMessage(PlantronicsMessage.MESSAGE_TYPE_EVENT, PlantronicsMessage.EVENT_PROXIMITY);
                            eventMessage.Payload.Add("proximity", "far");
                            BroadcastMessage(eventMessage);
                            break;
                        case HeadsetStateChange.HeadsetStateChange_ProximityDisabled:
                            break;
                        case HeadsetStateChange.HeadsetStateChange_ProximityEnabled:
                            break;
                        case HeadsetStateChange.HeadsetStateChange_ProximityUnknown:
                            break;
                        case HeadsetStateChange.HeadsetStateChange_InRange:
                            RegisterForProximity(true);
                            break;
                        case HeadsetStateChange.HeadsetStateChange_OutofRange:
                            break;
                        case HeadsetStateChange.HeadsetStateChange_Docked:
                            eventMessage = new PlantronicsMessage(PlantronicsMessage.MESSAGE_TYPE_EVENT, PlantronicsMessage.EVENT_DOCKED);
                            eventMessage.Payload.Add("docked", "true");
                            BroadcastMessage(eventMessage);
                            break;
                        case HeadsetStateChange.HeadsetStateChange_UnDocked:
                            eventMessage = new PlantronicsMessage(PlantronicsMessage.MESSAGE_TYPE_EVENT, PlantronicsMessage.EVENT_DOCKED);
                            eventMessage.Payload.Add("docked", "false");
                            BroadcastMessage(eventMessage);
                            break;
                        default:
                            break;
                    }
                    break;
                default:
                    break;
            }

        }

        static public void RegisterForProximity(bool register)
        {
            try
            {
                IHostCommandExt hostCommandExt = m_activeDevice.HostCommand as IHostCommandExt;
                if (hostCommandExt != null)
                {
                    hostCommandExt.EnableProximity(register); // enable proximity reporting for device
                    if (register)
                    {
                        hostCommandExt.GetProximity();
                    }
                }
            }
            catch (System.Exception e)
            {
                Console.WriteLine("Exception thrown while trying to enable proximity: " + e.Message);
            }
        }

        static void m_sessionComManager_DeviceStateChanged(object sender, _DeviceStateEventArgs e)
        {
            Console.WriteLine("Session Manager event: device state changed: " + e.State);

            // if our "Active device" was unplugged, detach from it and attach to new one
            if (e.State == DeviceState.DeviceState_Removed && m_activeDevice != null && string.Compare(e.DevicePath, m_activeDevice.DevicePath, true) == 0)
            {
                DetachDevice();
                AttachDevice();
            }
            else if (e.State == DeviceState.DeviceState_Added && m_activeDevice == null)
            {
                // if device is plugged, and we don't have "Active device", just attach to it
                AttachDevice();
            }
        }


        static void m_deviceListenerEvents_Handler(object sender, _DeviceListenerEventArgs e)
        {
            switch (e.DeviceEventType)
            {
                case DeviceEventType.DeviceEventType_ATDButtonPressed:
                case DeviceEventType.DeviceEventType_ATDStateChanged:
                    // Console.WriteLine(string.Format("DL Event: ATDStateChanged:{0}", e.ATDStateChange));
                    break;
                case DeviceEventType.DeviceEventType_BaseButtonPressed:
                case DeviceEventType.DeviceEventType_BaseStateChanged:
                    // Console.WriteLine(string.Format("DL Event: BaseButton:{0} BaseState:{1}", e.BaseButtonPressed, e.BaseStateChange));
                    break;
                case DeviceEventType.DeviceEventType_HeadsetButtonPressed:
                case DeviceEventType.DeviceEventType_HeadsetStateChanged:
                    // Console.WriteLine(string.Format("DL Event: HeadsetButton:{0} HeadsetState:{1}", e.HeadsetButtonPressed, e.HeadsetButtonPressed));
                    break;
                default:
                    // Console.WriteLine("DL Event");
                    break;
            }
        }
        #endregion

        #region Call Control Helper Classes
        // Call abstraction
        public class CallCOM : Interop.Plantronics.CallId
        {
            private int id = 0;
            #region ICall Members

            public int ConferenceId
            {
                get { return 0; }
                set { }
            }

            public int Id
            {
                get { return id; }
                set { id = value; }
            }

            public bool InConference
            {
                get { return false; }
                set { }
            }

            #endregion
        }

        // Contact abstraction
        public class ContactCOM : Interop.Plantronics.Contact
        {
            private string email;
            private string friendlyName;
            private string homePhone;
            private int id;
            private string mobPhone;
            private string name;
            private string phone;
            private string sipUri;
            private string workPhone;
            #region IContact Members

            public string Email
            {
                get
                {
                    return email;
                }
                set
                {
                    email = value;
                }
            }

            public string FriendlyName
            {
                get
                {
                    return friendlyName;
                }
                set
                {
                    friendlyName = value;
                }
            }

            public string HomePhone
            {
                get
                {
                    return homePhone;
                }
                set
                {
                    homePhone = value;
                }
            }

            public int Id
            {
                get
                {
                    return id;
                }
                set
                {
                    id = value;
                }
            }

            public string MobilePhone
            {
                get
                {
                    return mobPhone;
                }
                set
                {
                    mobPhone = value;
                }
            }

            public string Name
            {
                get
                {
                    return name;
                }
                set
                {
                    name = value;
                }
            }

            public string Phone
            {
                get
                {
                    return phone;
                }
                set
                {
                    phone = value;
                }
            }

            public string SipUri
            {
                get
                {
                    return sipUri;
                }
                set
                {
                    sipUri = value;
                }
            }

            public string WorkPhone
            {
                get
                {
                    return workPhone;
                }
                set
                {
                    workPhone = value;
                }
            }

            #endregion
        }
        #endregion
    }
}
