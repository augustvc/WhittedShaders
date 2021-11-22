using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;

namespace P
{
    class Ray
    {
        public float t = float.MaxValue;
        public Vector3 Origin;
        public Vector3 Direction;
        public long objectHit = -1;

        public Ray(Vector3 Origin, Vector3 Direction)
        {
            this.Origin = Origin;
            this.Direction = Direction;
        }
    }
}
