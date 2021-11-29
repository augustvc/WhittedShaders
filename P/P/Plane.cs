using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;

namespace P
{
    class Plane : Primitive
    {

        Vector3 normal;

        float offset;



        public override void Intersect(Ray ray)
        {




            float denominator = Vector3.Dot(-normal, ray.Direction);

            if (denominator > 0.0001)
            {
                Vector3 planeOrigin = normal * offset;
                Vector3 diff = planeOrigin - ray.Origin;
                float t = Vector3.Dot(-normal, diff) / denominator;
                //Console.WriteLine(t);

                if (t < 0)
                {
                    return;
                }
                if (t > ray.t)
                {

                    return;
                }
                ray.t = t;
                ray.objectHit = GetID();

            }
            return;
        }

        public override Vector3 GetNormal(Ray ray)
        {
            return normal;
        }


        public Plane(Vector3 normal, float offset, Vector3 color) : base(color)
        {
            this.normal = normal.Normalized();
            this.offset = offset;


        }

        public Plane(float x, float y, float z, float offset, Vector3 color) : this(new Vector3(x, y, z), offset, color) { }


    }
}
