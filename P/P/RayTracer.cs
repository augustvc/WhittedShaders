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
            Scene.Add(new Sphere(0, -1, 8, 2, new Vector3(1, 0, 0)));
            LightSources = new List<Light>();
            LightSources.Add(new Light(new Vector3(0.0f, 8.0f, 0.0f), new Vector3(20f, 20f, 20f)));
        }

        public float[] GenTexture(int width, int height)
        {
            float[] output = new float[width * height * 4];

            //Hardcoded camera
            Vector3 cameraPosition = Camera.getCameraPosition();
            Vector3 ViewDirection = Camera.getCameraFront();
            Vector3 cameraRight = Camera.getCameraRight();
            Vector3 cameraUp = Camera.getCameraUp();

            float screenDistance = 2f;
            Vector3 ScreenCenter = cameraPosition + ViewDirection * screenDistance;
            Vector3 BottomLeft = ScreenCenter - cameraUp - cameraRight;
            Vector3 BottomRight = ScreenCenter - cameraUp + cameraRight;
            Vector3 TopLeft = ScreenCenter - cameraRight + cameraUp;

            Vector3 xArm = BottomRight - BottomLeft;
            Vector3 yArm = TopLeft - BottomLeft;
            yArm *= (float)height / (float)width;

            int index = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float[] pixelColor = new float[4];

                    float yp = (float)y / (float)height;
                    float xp = (float)x / (float)width;

                    Vector3 screenSpot = BottomLeft + xp * xArm + yp * yArm;

                    Ray ray = new Ray(cameraPosition, (screenSpot - cameraPosition).Normalized());

                    //Intersect our ray with every primitive in the scene.
                    for (int i = 0; i < Scene.Count; i++)
                    {
                        Scene[i].Intersect(ray);
                    }

                    //If the ray hit an object, we set green to 1.
                    if (ray.objectHit != -1)
                    {
                        Vector3 normal = Scene[ray.objectHit].GetNormal(ray);
                        Vector3 collisionPosition = ray.Origin + ray.t * ray.Direction;
                        Vector3 shadowRayOrigin = collisionPosition + 0.0001f * normal;

                        for (int li = 0; li < LightSources.Count; li++) {
                            float inverseDistSq = 1.0f / (LightSources[li].position - shadowRayOrigin).LengthSquared;
                            
                            Ray shadowRay = new Ray(shadowRayOrigin, (LightSources[li].position - shadowRayOrigin).Normalized());
                            for (int i = 0; i < Scene.Count; i++)
                            {
                                Scene[i].Intersect(shadowRay);
                            }
                            if (shadowRay.objectHit == -1)
                            {
                                for(int j = 0; j < 3; j++)
                                {
                                    float ndotl = Vector3.Dot(shadowRay.Direction, normal);
                                    if (ndotl > 0.0f)
                                        pixelColor[j] += LightSources[li].intensity[j] * Scene[ray.objectHit].color[j] * inverseDistSq * ndotl;
                                }
                            }
                        }
                        for (int j = 0; j < 3; j++)
                        {
                            //Ambient light
                            pixelColor[j] += Scene[ray.objectHit].color[j] * 0.05f;
                        }
                    }

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
