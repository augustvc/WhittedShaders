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
        public Vector3 color = new Vector3(0f, 0.5f, 0f);
        static int primitiveCount;
        int primitiveID;
        public virtual void Intersect(Ray ray)
        {

        }

        public virtual Vector3 GetNormal(Ray ray)
        {
            return new Vector3(0.0f);
        }

        public Primitive(Vector3 color)
        {
            this.color = color;
            primitiveID = primitiveCount;
            primitiveCount++;
        }

        public int GetID ()
        {
            return primitiveID;
        }
    }
}
