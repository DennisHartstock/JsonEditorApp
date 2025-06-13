using JsonEditorApp.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonEditorApp
{
    public class DeviceConfig
    {
        public string SystemId { get; set; }
        public List<AssemblyConfig> Assemblies { get; set; }
        public AxesConfiguration AxesConfiguration { get; set; }
    }
}
