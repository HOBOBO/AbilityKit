#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace AbilityKit.HFSM.Definition
{

    public sealed class DefinitionJsonException : FormatException
    {
        public DefinitionJsonException(string path, string message, Exception? innerException = null)
            : base($"Invalid HFSM definition JSON at {path}: {message}", innerException)
        {
            Path = path ?? string.Empty;
        }

        public string Path { get; }
    }
}
