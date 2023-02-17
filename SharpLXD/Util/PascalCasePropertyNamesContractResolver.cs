﻿using Humanizer;
using Newtonsoft.Json.Serialization;
using System.Linq;

namespace SharpLXD
{
    internal class PascalCasePropertyNamesContractResolver : DefaultContractResolver
    {
        protected override string ResolvePropertyName(string propertyName)
        {
            if (char.IsLower(propertyName.First()))
            {
                // Property already have a customized name.
                return propertyName;
            }

            return propertyName.Underscore();
        }
    }
}
