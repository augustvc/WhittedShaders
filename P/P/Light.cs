using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;

namespace P
{
    class Light
    {
        public Vector3 position;
        public Vector3 intensity;
        public Light(Vector3 position, Vector3 intensity)
        {
            this.position = position;
            this.intensity = intensity;
        }
    }
}
