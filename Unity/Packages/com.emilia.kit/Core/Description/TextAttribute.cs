using System;

namespace Emilia.Kit
{
    [AttributeUsage(AttributeTargets.All)]
    public class TextAttribute : Attribute
    {
        public string text;

        public TextAttribute(string text)
        {
            this.text = text;
        }
    }
}