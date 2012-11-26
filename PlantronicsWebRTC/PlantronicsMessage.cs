using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Procurios.Public;
using System.Collections;

namespace Plantronics.UC.WebRTCDemo
{
    public class PlantronicsMessage : PlantronicsObject
    { 
        public const String MESSAGE_TYPE_UNKNOWN = "unknown";

        //IDs and constants
        public const String MESSAGE_TYPE_EVENT = "event";
        public const String MESSAGE_TYPE_COMMAND = "command";
        public const String MESSAGE_TYPE_SETTING = "setting";

        public static ReadOnlyCollection<String> EVENT_IDS;
        public static ReadOnlyCollection<String> COMMAND_IDS;
        public static ReadOnlyCollection<String> SETTING_IDS;

        //Events 
        public const String EVENT_DOCKED = "0X0E01";
        public const String EVENT_BUTTON_PRESS = "0X0600"; 
        public const String EVENT_WEAR_STATE_CHANGED = "0X0200"; 
        public const String EVENT_PROXIMITY = "0X0100";
        public const String EVENT_ACCEPT_CALL = "0X0E0C";
        public const String EVENT_CALL_TERMINATE = "0X0E11";

        //Commands
        public const String COMMAND_RING_ON = "0X0D08";
        public const String COMMAND_RING_OFF = "0X0D09";
        public const String COMMAND_MUTE_ON = "0X0D0A";
        public const String COMMAND_MUTE_OFF = "0X0D0B";
        public const String COMMAND_HANG_UP = "0X000C";

        //Settings
        public const String SETTING_HEADSET_INFO = "0X0F02";
    
        public const String ID = "id";
        public const String TYPE = "type";
        public const String PAYLOAD = "payload";

        private String type = MESSAGE_TYPE_UNKNOWN;
        private String id;
        private Dictionary<String, Object> payload = new Dictionary<String, Object>();

        static PlantronicsMessage()
        {
            List<String> _events = new List<string>() {
                EVENT_PROXIMITY, //proximity - ADDED BY CAB
                EVENT_WEAR_STATE_CHANGED, //Wear state changed
                EVENT_BUTTON_PRESS, //Button
                EVENT_DOCKED, //Docked
                EVENT_ACCEPT_CALL, //accepted incoming call
                EVENT_CALL_TERMINATE

            };
            EVENT_IDS = new ReadOnlyCollection<string>(_events);


            List<String> _commands = new List<string>{
                COMMAND_RING_ON,
                COMMAND_RING_OFF,
                COMMAND_MUTE_ON,
                COMMAND_MUTE_OFF,
                COMMAND_HANG_UP
            };
            COMMAND_IDS = new ReadOnlyCollection<string>(_commands);


            List<String> _settings = new List<string> {
                SETTING_HEADSET_INFO
            };
            SETTING_IDS = new ReadOnlyCollection<string>(_settings);
        }

        public PlantronicsMessage()
        {
        }

        //Convenience constructor for type/id
        public PlantronicsMessage(String type, String id)
        {
            this.type = type;
            this.id = id;
        }

        //property getter/setters
        public String Type
        {
            get { return type; }
            set { type = value; }
        }

        public String Id
        {
            get { return id; }
            set { id = value; }
        }

        public Dictionary<String, Object> Payload
        {
            get { return payload; }
            set { payload = value; }
        }

        //payload helper methods
        public void ResetPayload()
        {
            payload.Clear();
        }

        public void AddToPayload(String name, Object value)
        {
            payload.Add(name, value);
        }

        public void RemoveFromPayload(String name)
        {
            payload.Remove(name);
        }

        //used for serializing object to JSON
        public Hashtable GetObjectAsHashtable()
        {
            Hashtable messageAsHash = new Hashtable();
            messageAsHash.Add(TYPE, type);
            messageAsHash.Add(ID, id);
            Hashtable payloadHash = constructPayloadHash();
            messageAsHash.Add(PAYLOAD, payloadHash);

            return messageAsHash;
        }

        //creates thhe messages payload in hashtable form
        public Hashtable constructPayloadHash()
        {
            Hashtable payloadHash = new Hashtable();

            foreach (String name in payload.Keys)
            {
                Object value;
                if (payload.TryGetValue(name, out value))
                {
                    if (value is String)
                    {
                        payloadHash.Add(name, value);
                    }
                    else if (value is PlantronicsObject)
                    {
                        Hashtable obj = ((PlantronicsObject)value).GetObjectAsHashtable();
                        //get object as name/value pairs
                        payloadHash.Add(name, obj);
                    }
                    else if (value is Hashtable)
                    {
                        Hashtable obj = value as Hashtable;
                        payloadHash.Add(name, obj);
                    }
                    else
                    {
                        //get object as name/value pairs
                        payloadHash.Add(name, "unknown object type");
                    }
                }
            }

            return payloadHash;

        }

        //constructs a new message from a JSON blob
        public static PlantronicsMessage ParseMessageFromJSON(String json)
        {
            if (json == null || json.Trim().Length == 0)
            {
                return null;
            }
           
            Hashtable t = (Hashtable)JSON.JsonDecode(json);
            if (t == null)
            {
                return null;
            }

            if (!t.ContainsKey(TYPE))
            {
               return null;
            }

            PlantronicsMessage message = new PlantronicsMessage((String)t[TYPE], (String)t[ID]);

            if (t.ContainsKey(PAYLOAD))
            {
                Hashtable payload = (Hashtable)t[PAYLOAD];
                foreach (String key in payload.Keys)
                {
                    message.AddToPayload(key, payload[key]);
                }
            }
            return message;
        }

    }

}
