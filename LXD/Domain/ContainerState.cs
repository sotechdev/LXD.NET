using Newtonsoft.Json;
using System.Collections.Generic;

namespace LXD.Domain
{
    public class ContainerState : RemoteObject
    {
        public string Status { get; set; }
        [JsonProperty(PropertyName = "status_code")]
        public int StatusCode { get; set; }
        public Dictionary<string, StateDisk> Disk { get; set; }
        public StateMemory Memory { get; set; }
        public Dictionary<string, StateNetwork> Network { get; set; }
        public long Pid { get; set; }
        public long Processes { get; set; }

        public struct StateDisk
        {
            public long Usage { get; set; }
        }

        public struct StateMemory
        {
            public long Usage { get; set; }
            [JsonProperty(PropertyName = "usage_peak")]
            public long UsagePeak { get; set; }
            [JsonProperty(PropertyName = "swap_usage")]
            public long SwapUsage { get; set; }
            [JsonProperty(PropertyName = "swap_usage_peak")]
            public long SwapUsagePeak { get; set; }
        }

        public struct StateNetwork
        {
            public NetworkAddress[] Addresses { get; set; }
            public NetworkCounter Counters { get; set; }
            [JsonProperty(PropertyName = "hwaddr")]
            public string HardwareAddress { get; set; }
            [JsonProperty(PropertyName = "host_name")]
            public string HostName { get; set; }
            [JsonProperty(PropertyName = "mtu")]
            public string MTU { get; set; }
            public string State { get; set; }
            public string Type { get; set; }

            public struct NetworkAddress
            {
                public string Family { get; set; }
                public string Address { get; set; }
                public string Netmask { get; set; }
                public string Scope { get; set; }
            }

            public struct NetworkCounter
            {
                [JsonProperty(PropertyName = "bytes_received")]
                public long BytesReceived { get; set; }
                [JsonProperty(PropertyName = "bytes_sent")]
                public long BytesSent { get; set; }
                [JsonProperty(PropertyName = "packets_received")]
                public long PacketsReceived { get; set; }
                [JsonProperty(PropertyName = "packets_sent")]
                public long PacketsSent { get; set; }
            }
        }

        public struct StateCPU
        {
            public long Usage;
        }
    }
}
