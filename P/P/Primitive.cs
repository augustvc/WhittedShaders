using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;

namespace P
{
    class Primitive
    {
        Vector3 color;
        static long primitiveCount;
        long primitiveID;
        public virtual void Intersect(Ray ray)
        {

        }

        public Primitive(Vector3 color)
        {
            this.color = color;
            primitiveID = primitiveCount;
            primitiveCount++;
        }

        public long GetID ()
        {
            return primitiveID;
        }
    }
}
