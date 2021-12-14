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
        TopLevelBVH topLevelBVH;
        Material testMaterial = new Material(new Vector3(0.9f), 1f, 0f, false);

        public RayTracer(TopLevelBVH topLevelBVH)
        {
            this.topLevelBVH = topLevelBVH;
            Scene = new List<Primitive>();
            Scene.Add(new Sphere(new Vector3(0, 0, 4), 2, new Material(new Vector3(0, 0, 0), 0.0f, 0.0f, true, false, 2.1f)));
            Scene.Add(new Sphere(new Vector3(4, 0, 8), 2, new Material(new Vector3(1, 1, 1), 0.0f, 1.0f, false)));
            Scene.Add(new Plane(new Vector3(0, 1, 0), -5, new Material(new Vector3(0, 1, 1), 1.0f, 0.0f, false, true)));
            Scene.Add(new Plane(new Vector3(0, 0, -1), -55, new Material(new Vector3(8, 8, 1), 1.0f, 0.0f, false)));
            LightSources = new List<Light>();
            LightSources.Add(new Light(new Vector3(0.0f, 38.0f, 0.0f), new Vector3(500f, 500f, 500f)));
            LightSources.Add(new Light(new Vector3(5.0f, 38.0f, 0.0f), new Vector3(500f, 500f, 500f)));
        }


        Vector3 Sample(Ray ray, int maxDepth, bool debugging = false)
        {//Intersect our ray with every primitive in the scene.
            if(maxDepth < 0)
            {
                return new Vector3(0f);
            }

            topLevelBVH.TopBVH.nearestIntersection(ray, topLevelBVH.vertices);
            if (ray.debuggingRay)
            {
                //Console.WriteLine("Ray iters: " + ray.bvhChecks);
            }

            for (int i = 0; i < Scene.Count; i++)
            {
                Scene[i].Intersect(ray);
            }

            if(debugging)
            {
                //Console.WriteLine("ray with dir: " + ray.Direction + " hit primitive number: " + ray.objectHit + " after " + ray.t);
            }

            Material materialHit = testMaterial;
            Vector3 normal;

            if (ray.objectHit != -1)
            {
                normal = Scene[ray.objectHit].GetNormal(ray);
                materialHit = Scene[ray.objectHit].material;
            } else if(ray.triangleHit.Length == 3)
            {
                normal = Vector3.Cross(ray.triangleHit[2] - ray.triangleHit[0], ray.triangleHit[1] - ray.triangleHit[0]).Normalized();

                materialHit = testMaterial;
            } else   
            {
                return new Vector3(0f);
            }
            if (Vector3.Dot(ray.Direction, normal) > 0)
                normal = -normal;

            Vector3 collisionPosition = ray.Origin + ray.t * ray.Direction;
            Vector3 shadowRayOrigin = collisionPosition + 0.0001f * normal;
            Vector3 materialColor = materialHit.color;

            Vector3 color = new Vector3(0f);

            if (materialHit.isCheckerd)
            {
                Vector3 pointOnPlane = Scene[ray.objectHit].GetPointOnSurface(ray);
                //Console.WriteLine(pointOnPlane);
                Vector3 direction = shadowRayOrigin - pointOnPlane;
                Vector3 left = Vector3.Cross(direction, normal);
                Vector3 right = -left;
                left.Normalize();
                right.Normalize();

                if (Math.Floor(shadowRayOrigin.X) % 2 == 0 ^ Math.Floor(shadowRayOrigin.Z) % 2 == 0)
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
                    if (shadowRay.objectHit == -1 && shadowRay.triangleHit.Length == 0)
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

            float specular = materialHit.specular;

            if(materialHit.dielectric)
            {
                float n1 = ray.refractionIndex;
                float n2 = materialHit.refractionIndex;
                    
                if(Vector3.Dot(ray.Direction, normal) > 0)
                {
                    if(debugging)
                    {
                        Console.WriteLine("Pointing out");
                    }
                    n2 = 1.0f; //Pointing out
                    normal = -normal;
                } else if(debugging)
                {
                    Console.WriteLine("Not pointing out");
                }
                if (debugging)
                {
                    Console.WriteLine("ray direction: " + ray.Direction);
                    Console.WriteLine("normal: " + normal);
                    Console.WriteLine("N1: " + n1);
                    Console.WriteLine("n2: " + n2);
                    Console.WriteLine("Ray t: " + ray.t);
                }

                float n1Overn2 = n1 / n2;

                float dot = Vector3.Dot(-ray.Direction, normal);
                float k = 1f - (n1Overn2 * n1Overn2) * (1f - dot * dot);
                if (k >= 0)
                {
                    Vector3 tdir = (n1Overn2 * ray.Direction + normal * (n1Overn2 * dot - (float)Math.Sqrt(k))).Normalized();
                    Ray transmission = new Ray(ray.Origin + ray.t * ray.Direction + tdir * 0.001f, tdir, float.MaxValue, n2);

                    float cosThetaT = Vector3.Dot(-normal, tdir);

                    float sPolarizedSqrt = (n1 * dot - n2 * cosThetaT) /
                                (n1 * dot + n2 * cosThetaT);

                    float pPolarizedSqrt = (n1 * cosThetaT - n2 * dot) /
                                            (n1 * cosThetaT + n2 * dot);

                    specular = 0.5f * (sPolarizedSqrt * sPolarizedSqrt + pPolarizedSqrt * pPolarizedSqrt);
                        
                    if(debugging)
                    {
                        Console.WriteLine("Specular: " + specular);
                    }

                    color += (1f - specular) * Sample(transmission, maxDepth - 1, debugging);
                } else
                {
                    specular = 1.0f;
                }                 
            }

            if (specular > 0.0f)
            {
                Ray reflection = new Ray(shadowRayOrigin, ray.Direction + (Vector3.Dot(-ray.Direction, normal) * normal * 2));
                color += specular * Sample(reflection, maxDepth - 1, false);
            }

            for (int j = 0; j < 3; j++)
            {
                //Ambient light
                //color[j] += materialColor[j] * 0.05f; 
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


            var aap = 1;
            Parallel.For(0, height * aap, (y) =>
            {
                for (int x = 0; x < width * aap; x++)
                {

                    float yp = (float)y / ((float)height * aap);
                    float xp = (float)x / ((float)width * aap);

                    Vector3 screenSpot = BottomLeft + xp * xArm + yp * yArm;

                    Ray ray = new Ray(cameraPosition, (screenSpot - cameraPosition).Normalized());

                    int recursionDepth = 4;

                    if ((y == height / 2) && (x == width / 2))
                    {
                        //Console.WriteLine("Doing the middle pixel:");
                        //Console.WriteLine("----------------------------------------------");
                        ray.debuggingRay = true;
                    }
                    Vector4 pixelColor = new Vector4(Sample(ray, recursionDepth, false), 1.0f);
                    if ((y == height / 2) && (x == width / 2))
                    {
                        pixelColor = new Vector4(0.0f, 0.0f, 1.0f, 1.0f);
                        //Console.WriteLine("BVH checks done: " + ray.bvhChecks);
                    }

                    int index = ((y / aap) * width + (x / aap)) * 4;
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
