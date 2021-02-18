using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Newtonsoft.Json;

namespace SharpLXD.Domain
{
    public abstract class PostContainerParameter
    {
        public string ContainerName { get; set; }
        public Container Container {
            set => ContainerName = value.Name;
        }
        public string[] ProfileNames { get; set; }
        public Profile[] Profiles {
            set => ProfileNames = value.Select(x => x.Name).ToArray();
        }

        public bool IsEphemeral { get; set; }

        public Dictionary<string, string> Config { get; set; }

        public Device[] Devices { get; set; }


        protected void WriteJson(JsonWriter writer)
        {
            writer.WritePropertyName("name");
            writer.WriteValue(ContainerName);
            writer.WritePropertyName("profiles");
            writer.WriteStartArray();
            foreach (var p in ProfileNames)
            {
                writer.WriteValue(p);
            }
            writer.WriteEndArray();
            writer.WritePropertyName("ephemeral");
            writer.WriteValue(IsEphemeral);
            writer.WritePropertyName("config");
            writer.WriteStartObject();
            if (Config != null)
            {
                foreach (var c in Config)
                {
                    writer.WritePropertyName(c.Key);
                    writer.WritePropertyName(c.Value);
                }
            }
            writer.WriteEndObject();
            writer.WritePropertyName("devices");
            writer.WriteStartObject();
            if (Devices != null)
            {
                foreach (var d in Devices)
                {
                    writer.WritePropertyName(d.Name);
                    writer.WriteStartObject();
                    writer.WritePropertyName("path");
                    writer.WriteValue(d.Path);
                    writer.WritePropertyName("type");
                    writer.WriteValue(d.Type);
                    writer.WriteEndObject();
                }
            }
            writer.WriteEndObject();
        }

        internal PostContainerParameter(string containerName)
        {
            ContainerName = containerName;
            ProfileNames = new string[]{"default"};
            IsEphemeral = false;
            Config = new Dictionary<string, string>();
            Devices = new Device[0];
        }

        public abstract string GetJsonText();

        public class CopyContainerParameter : PostContainerParameter
        {
            public string SourceContainerName { get; set; }
            public bool IsContainerOnly { get; set; }

            public CopyContainerParameter(string destinationContainerName, string sourceContainerName, bool isContainerOnly) : base(destinationContainerName)
            {
                SourceContainerName = sourceContainerName;
                IsContainerOnly = isContainerOnly;
            }

            public override string GetJsonText()
            {
                StringBuilder sb = new StringBuilder();
                using (JsonWriter writer = new JsonTextWriter(new System.IO.StringWriter(sb)))
                {
                    writer.Formatting = Formatting.None;
                    writer.WriteStartObject();
                    base.WriteJson(writer);
                    writer.WritePropertyName("source");
                    writer.WriteStartObject();
                    writer.WritePropertyName("type");
                    writer.WriteValue("copy");
                    writer.WritePropertyName("container_only");
                    writer.WriteValue(IsContainerOnly);
                    writer.WritePropertyName("source");
                    writer.WriteValue(SourceContainerName);
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }
                return sb.ToString();
            }
        }
    }
}
