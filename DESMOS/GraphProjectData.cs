using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DESMOS;

public class GraphProjectData
{
    public float Scale { get; set; } = 50f;
    public float OffsetX { get; set; } = 0f;
    public float OffsetY { get; set; } = 0f;
    public List<GraphFunction> Functions { get; set; } = new();
    public List<GraphParameter> Parameters { get; set; } = new(); 
}

