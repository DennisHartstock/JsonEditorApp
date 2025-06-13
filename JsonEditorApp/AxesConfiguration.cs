using JsonEditorApp.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonEditorApp
{
    public class AxesConfiguration
    {
        public List<AxisNode> AxisNodes { get; set; }
        public string Type { get; set; }
        public string Alias { get; set; }
    }
}
