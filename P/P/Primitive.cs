using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;

namespace P
{
    abstract class Primitive
    {
        public Material material = new Material(new Vector3(1.0f), 1.0f, 0.0f, false);
        static int primitiveCount;
        int primitiveID;
        public abstract void Intersect(Ray ray);

        public abstract Vector3 GetNormal(Ray ray);
        public abstract Vector3 GetPointOnSurface(Ray ray);


        public Primitive()
        {
            primitiveID = primitiveCount;
            primitiveCount++;
        }

        public int GetID()
        {
            return primitiveID;
        }
    }
}
