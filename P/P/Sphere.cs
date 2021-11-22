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
        Vector3 Origin;
        float radius;
        float radiusSq;
        public override void Intersect(Ray ray)
        {
            Vector3 toOrg = Origin - ray.Origin;
            float t = Vector3.Dot(toOrg, ray.Direction);
            Vector3 diff = toOrg - (t * ray.Direction);
            float dsq = Vector3.Dot(diff, diff);
            if(dsq > radiusSq)
                return;
            t -= (float)Math.Sqrt(radiusSq - dsq);

            if (t < 0) return;
            if (t > ray.t) return;
            ray.t = t;
            ray.objectHit = GetID();
        }

        public override Vector3 GetNormal(Ray ray)
        {
            Vector3 collisionPoint = ray.Origin + ray.Direction * ray.t;
            return (collisionPoint - Origin).Normalized();
        }

        public Sphere(Vector3 Origin, float radius, Vector3 color) : base(color)
        {
            this.Origin = Origin;
            this.radius = radius;
            radiusSq = radius * radius;
        }

        public Sphere(float x, float y, float z, float radius, Vector3 color) : this(new Vector3(x, y, z), radius, color) { }
    }
}
