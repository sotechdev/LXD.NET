using System.Collections.Generic;

namespace SharpLXD.Domain
{
    public class Profile : RemoteObject
    {
        public string Name;
        public Dictionary<string, string> Config;
        public string Description;
        public Dictionary<string, Dictionary<string, string>> Devices;
    }
}
