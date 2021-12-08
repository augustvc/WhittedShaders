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
        public Random r = new Random();
        public Vector3 randomPoint = new Vector3(0.0f);



        public override void Intersect(Ray ray)
        {
            float denominator = Vector3.Dot(-normal, ray.Direction);

            if (randomPoint == new Vector3(0.0f, 0.0f, 0.0f))
            {
                randomPoint.X = (float)r.NextDouble();
                randomPoint.Y = (float)r.NextDouble();
                randomPoint.Z = (float)r.NextDouble();

                randomPoint.Normalize();
            }


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
        }

        public override Vector3 GetNormal(Ray ray)
        {
            if (Vector3.Dot(ray.Direction, normal) < 0.0)
            {
                return normal;
            }
            return -normal;
        }

        public override Vector3 GetPointOnSurface(Ray ray)
        {

            Vector3 pointToPlane = new Vector3(randomPoint.X - normal.X, randomPoint.Y - normal.Y, randomPoint.Z - normal.Z);
            float normalRandom = Vector3.Dot(pointToPlane, normal);
            Vector3 pointOnPlane = pointToPlane + normalRandom * normal;
            return pointOnPlane;

        }

        public Plane(Vector3 normal, float offset, Material mat) : base()
        {
            this.normal = normal.Normalized();
            this.offset = offset;
            this.material = mat;
        }

    }
}
