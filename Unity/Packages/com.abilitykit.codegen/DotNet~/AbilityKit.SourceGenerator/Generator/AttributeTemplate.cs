using System;
using System.Collections.Generic;

namespace Share.SourceGenerator
{
    public class AttributeTemplate
    {
        private Dictionary<string, string> templates = new Dictionary<string, string>();

        public AttributeTemplate()
        {
           
        }

        public string Get(string attributeType)
        {
            if (!this.templates.TryGetValue(attributeType, out string template))
            {
                throw new Exception($"not config template: {attributeType}");
            }
            return template;
        }

        public bool Contains(string attributeType)
        {
            return this.templates.ContainsKey(attributeType);
        }
    }
}