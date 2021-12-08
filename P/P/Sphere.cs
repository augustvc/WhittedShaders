﻿using System;
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
            t -= (float)Math.Sqrt(radiusSq - dsq);

            if (t < 0) return;
            if (t > ray.t) return;
            ray.t = t;
            ray.objectHit = GetID();
        }

        public override Vector3 GetNormal(Ray ray)
        {
            Vector3 collisionPoint = ray.Origin + ray.Direction * ray.t;
            Vector3 normal = (collisionPoint - Location).Normalized();

            if (Vector3.Dot(ray.Direction, normal) < 0.0)
            {
                return normal;
            }
            return -normal;
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
