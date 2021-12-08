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
        public int objectHit = -1;
        public float refractionIndex;

        public Ray(Vector3 Origin, Vector3 Direction, float maxT = float.MaxValue, float refractionIndex = 1.0f)
        {
            this.Origin = Origin;
            this.Direction = Direction;
            this.t = maxT;
            this.refractionIndex = refractionIndex;
        }
    }
}
