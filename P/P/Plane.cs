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


        public Plane(Vector3 normal, float offset, Material mat) : base()
        {
            this.normal = normal.Normalized();
            this.offset = offset;


        }

    }
}
