using Newtonsoft.Json;

namespace SharpLXD.Domain
{
    public class Certificate : RemoteObject
    {
        [JsonProperty("certificate")]
        public string Content;
        public string Fingerprint;
        public string Type;
    }
}
