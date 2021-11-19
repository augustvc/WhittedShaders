using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P
{
    class RayTracer
    {
        public float[] GenTexture (int width, int height)
        {
            float[] output = new float[width * height * 4];

            int ptr = 0;
            for (int y = 0; y < height; y++)
            {
                for(int x = 0; x < width; x++)
                {
                    if(x > width / 2)
                    {
                        output[ptr] = 1.0f;
                    } else
                    {
                        output[ptr] = 0.0f;
                    }

                    if (y > height / 2)
                    {
                        output[ptr + 1] = 1.0f;
                    }
                    else
                    {
                        output[ptr + 1] = 0.0f;
                    }

                    output[ptr + 2] = 0.0f;
                    output[ptr + 3] = 1.0f;

                    ptr += 4;
                }
            }

            return output;
        }
    }
}
