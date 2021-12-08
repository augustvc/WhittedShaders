using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;

namespace P
{
    class Sphere : Primitive
    {
        Vector3 Location;
        float radius;
        float radiusSq;
        public override void Intersect(Ray ray)
        {
            Vector3 toOrg = Location - ray.Origin;
            float t = Vector3.Dot(toOrg, ray.Direction);
            Vector3 diff = toOrg - (t * ray.Direction);
            float dsq = Vector3.Dot(diff, diff);
            if (dsq > radiusSq)
                return;
            float sqrtThing = (float)Math.Sqrt(radiusSq - dsq);

            t -= sqrtThing;
            if(t >= 0 && t <= ray.t) { 
                ray.t = t;
                ray.objectHit = GetID();
            }
            t += sqrtThing * 2;
            if (t >= 0 && t <= ray.t)
            {
                ray.t = t;
                ray.objectHit = GetID();
            }
        }

        public override Vector3 GetNormal(Ray ray)
        {
            Vector3 collisionPoint = ray.Origin + ray.Direction * ray.t;
            Vector3 normal = (collisionPoint - Location).Normalized();

            return normal;
        }

        public Sphere(Vector3 Origin, float radius, Material mat) : base()
        {
            this.Location = Origin;
            this.radius = radius;
            radiusSq = radius * radius;
            this.material = mat;
        }

        public override Vector3 GetPointOnSurface(Ray ray)
        {
            throw new NotImplementedException();
        }

    }
}
