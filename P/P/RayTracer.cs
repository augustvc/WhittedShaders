using System;
using OpenTK.Input;
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
            Scene.Add(new Sphere(new Vector3(0, 3, 7), 2, new Material(new Vector3(1, 0, 0), 0.0f, 1.0f, false)));
            Scene.Add(new Sphere(new Vector3(10, -1, 7), 2, new Material(new Vector3(1, 0, 0), 1.0f, 0.0f, false)));
            Scene.Add(new Sphere(new Vector3(15, -1, 7), 2, new Material(new Vector3(1, 0, 0), 1.0f, 0.0f, false)));
            Scene.Add(new Sphere(new Vector3(-5, -1, 7), 2, new Material(new Vector3(1, 0, 0), 1.0f, 0.0f, false)));
            Scene.Add(new Sphere(new Vector3(-10, -1, 7), 2, new Material(new Vector3(1, 0, 0), 1.0f, 0.0f, false)));
            Scene.Add(new Plane(new Vector3(2, 12, 0), -5, new Material(new Vector3(0, 1, 0), 1.0f, 0.0f, false, true)));
            Scene.Add(new Plane(new Vector3(0, 0, -1), -16, new Material(new Vector3(8, 8, 1), 1.0f, 0.0f, false)));
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

            if (ray.objectHit != -1)
            {
                Vector3 normal = Scene[ray.objectHit].GetNormal(ray);
                Vector3 collisionPosition = ray.Origin + ray.t * ray.Direction;
                Vector3 shadowRayOrigin = collisionPosition + 0.0001f * normal;



                Material materialHit = Scene[ray.objectHit].material;
                Vector3 materialColor = materialHit.color;

                if (materialHit.isCheckerd)
                {
                    Vector3 pointOnPlane = Scene[ray.objectHit].GetPointOnSurface(ray);
                    //Console.WriteLine(pointOnPlane);
                    Vector3 direction = shadowRayOrigin - pointOnPlane;
                    Vector3 left = RotateCCW90(normal - pointOnPlane).Normalized();
                    Vector3 right = Vector3.Cross(-left, normal).Normalized();


                    if (Math.Floor(Vector3.Dot(direction, left)) % 2 == 0 ^ Math.Floor(Vector3.Dot(direction, right)) % 2 == 0)
                    {
                        materialColor = new Vector3(0.0f);
                    }
                    else
                    {
                        materialColor = new Vector3(1.0f);
                    }


                }
                if (materialHit.diffuse > 0.0f)
                {
                    for (int li = 0; li < LightSources.Count; li++)
                    {
                        float inverseDistSq = 1.0f / (LightSources[li].position - shadowRayOrigin).LengthSquared;

                        Ray shadowRay = new Ray(shadowRayOrigin, (LightSources[li].position - shadowRayOrigin).Normalized(), (LightSources[li].position - shadowRayOrigin).Length);
                        for (int i = 0; i < Scene.Count; i++)
                        {
                            Scene[i].Intersect(shadowRay);
                        }
                        if (shadowRay.objectHit == -1)
                        {
                            float ndotl = Vector3.Dot(shadowRay.Direction, normal);

                            if (ndotl > 0.0f)
                            {
                                for (int j = 0; j < 3; j++)
                                {
                                    color[j] += materialHit.diffuse * LightSources[li].intensity[j] * materialColor[j] * inverseDistSq * ndotl;
                                }
                            }
                        }
                    }
                }

                if (materialHit.specular > 0.0f)
                {
                    Ray reflection = new Ray(shadowRayOrigin, ray.Direction + (Vector3.Dot(-ray.Direction, normal) * normal * 2));
                    color += Sample(reflection, maxDepth - 1);
                }
                for (int j = 0; j < 3; j++)
                {
                    //Ambient light
                    //color[j] += materialColor[j] * 0.05f;
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
            bool isFish = false;
            KeyboardState input = Keyboard.GetState();

            while (isFish)
            {
                if (input.IsKeyDown(Key.F))
                {
                    Vector3 Norm = Vector3.Normalize(xArm) + Vector3.Normalize(yArm);

                    float pi = 3.148f;
                    float aperture = 5.0f;
                    Vector2 center = new Vector2(ScreenCenter.X / 2, ScreenCenter.Y / 2);
                    Vector2 rel = new Vector2(ScreenCenter.X - center.X, ScreenCenter.Y - center.Y);


                    float radius = (float)Math.Atan2(Math.Sqrt(rel.X * rel.X + rel.Y * rel.Y), (float)screenDistance) / pi;
                    float phi = (float)Math.Atan2(Norm.Y, Norm.X);

                    //double u = radius * Math.Cos(phi) + 0.5;
                    //double v = radius * Math.Sin(phi) + 0.5;

                    float theta = (float)Math.Atan2(rel.X, rel.Y);
                    theta += radius * aperture;

                    if (radius == 0)
                    {
                        phi = 0;
                    }
                    else if (rel.X < 0)
                    {
                        phi = pi - (float)Math.Asin(Norm.Y / radius);
                    }
                    else if (rel.X >= 0)
                    {
                        phi = (float)Math.Asin(Norm.Y / radius);
                    }

                    ScreenCenter.X = (float)(Math.Sin(theta) * Math.Cos(phi));
                    ScreenCenter.Y = (float)(Math.Sin(theta) * Math.Sin(phi));
                    ScreenCenter.Z = (float)(Math.Cos(theta));
                    isFish = true;
                }
                else
                    isFish = false;

            }



            Parallel.For(0, height, (y) =>
            {
                for (int x = 0; x < width; x++)
                {

                    float yp = (float)y / (float)height;
                    float xp = (float)x / (float)width;

                    Vector3 screenSpot = BottomLeft + xp * xArm + yp * yArm;

                    Ray ray = new Ray(cameraPosition, (screenSpot - cameraPosition).Normalized());

                    Vector4 pixelColor = new Vector4(Sample(ray, 20), 1.0f);

                    int index = (y * width + x) * 4;
                    //Put the pixel values in the output image.
                    for (int i = 0; i < 4; i++)
                    {
                        output[index + i] = pixelColor[i];
                    }
                }
            });

            return output;
        }

        public Vector3 RotateCW90(Vector3 aDir)
        {
            return new Vector3(aDir.Z, 0, -aDir.X);
        }

        public Vector3 RotateCCW90(Vector3 aDir)
        {
            return new Vector3(-aDir.Z, 0, aDir.X);
        }
    }
}
