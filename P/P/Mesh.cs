using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P
{
    public class Mesh
    {
        public float[] vertices;
        public List<uint> indices;

        public Mesh(float[] vertices, List<uint> indices)
        {
            this.vertices = vertices;
            this.indices = indices;
        }
    }
}
