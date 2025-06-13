using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonEditorApp.Json
{
    internal class JsonClasses
    {
    }

public class DeviceConfig
    {
        public string SystemId { get; set; }
        public List<AssemblyConfig> Assemblies { get; set; }
        public AxesConfiguration AxesConfiguration { get; set; }
    }

    public class AssemblyConfig
    {
        public string Assembly { get; set; }
        public string Type { get; set; }
        public string Alias { get; set; }
    }

    public class AxesConfiguration
    {
        public List<AxisNode> AxisNodes { get; set; }
        public string Type { get; set; }
        public string Alias { get; set; }
    }

    public class AxisNode
    {
        public string NodeId { get; set; }
        // Die Struktur von "CollisionActors" ist im Schema nicht definiert,
        // daher verwenden wir vorerst "object".
        // Wenn du die Struktur kennst, kannst du eine spezifischere Klasse erstellen.
        public object CollisionActors { get; set; }
    }

}
