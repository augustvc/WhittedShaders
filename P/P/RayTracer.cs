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

        public RayTracer()
        {
            Scene = new List<Primitive>();
            Scene.Add(new Sphere(0, -1, 8, 2, new Vector3(1, 0, 0)));
        }

        public float[] GenTexture(int width, int height)
        {
            float[] output = new float[width * height * 4];

            //Hardcoded camera
            Vector3 cameraPosition = Camera.getCameraPosition();
            Vector3 ViewDirection = Camera.getCameraTarget();
            float screenDistance = 2f;
            Vector3 ScreenCenter = cameraPosition + ViewDirection * screenDistance;
            Vector3 BottomLeft = ScreenCenter + new Vector3(-1, -1, 0);
            Vector3 BottomRight = ScreenCenter + new Vector3(1, -1, 0);
            Vector3 TopLeft = ScreenCenter + new Vector3(-1, 1, 0);

            Vector3 xArm = BottomRight - BottomLeft;
            Vector3 yArm = TopLeft - BottomLeft;

            int index = 0;
            Console.WriteLine(cameraPosition);
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
                        pixelColor[1] = 1.0f;
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
