using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonEditorApp
{
    public class AxisNode
    {
        public string NodeId { get; set; }
        // Die Struktur von "CollisionActors" ist im Schema nicht definiert,
        // daher verwenden wir vorerst "object".
        // Wenn du die Struktur kennst, kannst du eine spezifischere Klasse erstellen.
        public object CollisionActors { get; set; }
    }
}
