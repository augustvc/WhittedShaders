using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;

namespace P
{
    class RayTracer
    {
        List<Primitive> Scene;
        List<Light> LightSources;

        public RayTracer()
        {
            Scene = new List<Primitive>();
            Scene.Add(new Sphere(new Vector3(0, -3, 8), 2, new Material(new Vector3(1, 0, 0), 0.0f, 1.0f, false)));
            //Scene.Add(new Sphere(0, 3, 7, 2, new Vector3(1, 0, 0)));
            //Scene.Add(new Sphere(10, -1, 7, 2, new Vector3(1, 0, 0)));
            //Scene.Add(new Sphere(15, -1, 7, 2, new Vector3(1, 0, 0)));
            //Scene.Add(new Sphere(20, -1, 7, 2, new Vector3(1, 0, 0)));
            //Scene.Add(new Sphere(-5, -1, 7, 2, new Vector3(1, 0, 0)));
            //Scene.Add(new Sphere(-10, -1, 7, 2, new Vector3(1, 0, 0)));
            Scene.Add(new Plane(new Vector3(0, 1, 0), -5, new Material(new Vector3(0, 1, 0), 1.0f, 0.0f, false)));
            LightSources = new List<Light>();
            LightSources.Add(new Light(new Vector3(0.0f, 8.0f, 0.0f), new Vector3(50f, 50f, 50f)));
            LightSources.Add(new Light(new Vector3(5.0f, 8.0f, 0.0f), new Vector3(50f, 50f, 50f)));
        }

        Vector3 Sample(Ray ray, int maxDepth)
        {//Intersect our ray with every primitive in the scene.
            for (int i = 0; i < Scene.Count; i++)
            {
                Scene[i].Intersect(ray);
            }

            Vector3 color = new Vector3(0.0f);

            //If the ray hit an object, we set green to 1.
            if (ray.objectHit != -1)
            {

                Vector3 normal = Scene[ray.objectHit].GetNormal(ray);
                Vector3 collisionPosition = ray.Origin + ray.t * ray.Direction;
                Vector3 shadowRayOrigin = collisionPosition + 0.0001f * normal;

                for (int li = 0; li < LightSources.Count; li++)
                {
                    float inverseDistSq = 1.0f / (LightSources[li].position - shadowRayOrigin).LengthSquared;

                    Ray shadowRay = new Ray(shadowRayOrigin, (LightSources[li].position - shadowRayOrigin).Normalized());
                    for (int i = 0; i < Scene.Count; i++)
                    {
                        Scene[i].Intersect(shadowRay);
                    }
                    if (shadowRay.objectHit == -1)
                    {
                        Material materialHit = Scene[ray.objectHit].material;

                        //TODO : Look at reflections and dielectrics instead of assuming diffuse

                        for (int j = 0; j < 3; j++)
                        {
                            float ndotl = Vector3.Dot(shadowRay.Direction, normal);
                            if (ndotl > 0.0f)
                                color[j] += LightSources[li].intensity[j] * Scene[ray.objectHit].material.color[j] * inverseDistSq * ndotl;
                        }
                    }
                }
                for (int j = 0; j < 3; j++)
                {
                    //Ambient light
                    //pixelColor[j] += Scene[ray.objectHit].color[j] * 0.05f;
                }
            }

            return color;
        }

        public float[] GenTexture(int width, int height)
        {
            float[] output = new float[width * height * 4];

            //Hardcoded camera
            Vector3 cameraPosition = Camera.getCameraPosition();
            Vector3 ViewDirection = Camera.getCameraFront();
            Vector3 cameraRight = Camera.getCameraRight();
            Vector3 cameraUp = Camera.getCameraUp();

            float screenDistance = Camera._screenDist;
            Vector3 ScreenCenter = cameraPosition + ViewDirection * screenDistance;
            Vector3 BottomLeft = ScreenCenter - cameraUp - cameraRight;
            Vector3 BottomRight = ScreenCenter - cameraUp + cameraRight;
            Vector3 TopLeft = ScreenCenter - cameraRight + cameraUp;

            Vector3 xArm = BottomRight - BottomLeft;
            Vector3 yArm = TopLeft - BottomLeft;
            yArm *= (float)height / (float)width;

            //fish eye implementation
            Vector3 Norm = Vector3.Normalize(xArm) + Vector3.Normalize(yArm);

            float pi = 3.148f;
            float aperture = 1.0f;



            double radius = Math.Atan2(Math.Sqrt(Norm.X * Norm.X + Norm.Y * Norm.Y), (double)screenDistance) / pi;
            double phi = Math.Atan2(Norm.Y, Norm.X);

            double u = radius * Math.Cos(phi) + 0.5;
            double v = radius * Math.Sin(phi) + 0.5;

            double theta = radius * aperture / 2;

            if (radius == 0)
            {
                phi = 0;
            }
            else if (Norm.X < 0)
            {
                phi = pi - Math.Asin(Norm.Y / radius);
            }
            else if (Norm.X >= 0)
            {
                phi = Math.Asin(Norm.Y / radius);
            }

            ViewDirection.X = (float)(Math.Sin(theta) * Math.Cos(phi));
            ViewDirection.Y = (float)(Math.Sin(theta) * Math.Sin(phi));
            ViewDirection.Z = (float)(Math.Cos(theta));


            int index = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {

                    float yp = (float)y / (float)height;
                    float xp = (float)x / (float)width;

                    Vector3 screenSpot = BottomLeft + xp * xArm + yp * yArm;

                    Ray ray = new Ray(cameraPosition, (screenSpot - cameraPosition).Normalized());

                    Vector4 pixelColor = new Vector4(Sample(ray, 20), 1.0f);                    

                    //Put the pixel values in the output image.
                    for (int i = 0; i < 4; i++)
                    {
                        output[index + i] = pixelColor[i];
                    }
                    index += 4;
                }
            }

            return output;
        }
    }
}
