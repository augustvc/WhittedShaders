using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;

namespace P
{
    static class GameScene
    {
        public static List<Matrix4> finalMatrices = new List<Matrix4>();
        public static List<GPUMaterial> finalMaterials = new List<GPUMaterial>();

        public static uint[] scene5Indices = new uint[0];
        public static float[] scene5Vertices = new float[0];

        public static void LoadDefaultScene(uint sceneNumber)
        {
            finalMaterials = new List<GPUMaterial>();
            finalMatrices = new List<Matrix4>();

            //A scene is a combination of matrices and materials.
            //Every matrix and material you add, is turned into an in-game object with that transformation matrix and material
            if (sceneNumber == 1)
            {
                for (int j = 0; j < 1000; j++)
                {
                    for (int i = 1; i <= 1000; i++)
                    {
                        finalMatrices.Add(Matrix4.CreateRotationY(-0.5f * 3.141592f) * Matrix4.CreateTranslation(i * 100, 0, j * 200) * Matrix4.CreateRotationX(1000 * 0.0001f * i));
                        finalMaterials.Add(new GPUMaterial(1f - (j / 1000f), 1f - (i / 1000f), 1, 1f, 0f));
                    }
                }
                Camera.SetPosition(new Vector3(0f, 0f, 0f), 0f, 0f);
            }
            else if (sceneNumber == 2)
            {
                for (int j = 0; j < 2500; j++)
                {
                    for (int i = 0; i < 2500; i++)
                    {
                        finalMatrices.Add(Matrix4.CreateTranslation(i * 100, 0, j * 100));
                        finalMaterials.Add(new GPUMaterial(1f, 1f, 1f, 1f, 0f));
                    }
                }
                Camera.SetPosition(new Vector3(-130f, 48f, -18.6f), -2f, 10.1f);
            }
            else if (sceneNumber == 3)
            {
                finalMatrices.Add(Matrix4.Identity);
                finalMaterials.Add(new GPUMaterial(1f, 1f, 1f, 0.2f, 0.8f));
                finalMatrices.Add(Matrix4.CreateTranslation(0f, 0f, 100f));
                finalMaterials.Add(new GPUMaterial(1f, 0f, 1f, 0.2f, 0.8f));

                Camera.SetPosition(new Vector3(0f, 30f, -32.9f), -6.3f, 91f);
            }
            else if (sceneNumber == 4)
            {
                finalMatrices.Add(Matrix4.Identity);
                finalMaterials.Add(new GPUMaterial(1f, 1f, 1f, 0f, 1f));
                float red = 1f;
                float green = 0f;
                float blue = 0f;
                for (int i = 0; i < 10; i++)
                {
                    finalMatrices.Add(Matrix4.CreateTranslation(-200f, 0f, 0f) * Matrix4.CreateRotationY(-(i * 6.28f) / 10f + 3.14f));
                    finalMaterials.Add(new GPUMaterial(red, green, blue, 1f, 0f));
                    float blend = 0.2f;
                    blue += blend * green;
                    green -= blend * green;
                    green += blend * red;
                    red -= blend * red;
                    blue -= blend * blue;
                }
                Camera.SetPosition(new Vector3(-102.8f, 119.5f, 16.4f), -35.4f, 10f);
            }
            else if (sceneNumber == 5)
            {
                for (int i = 0; i < scene5Indices.Length; i += 3)
                {
                    Vector3 position = new Vector3(scene5Vertices[scene5Indices[i] * 3], scene5Vertices[scene5Indices[i] * 3 + 1], scene5Vertices[scene5Indices[i] * 3 + 2]);
                    position += new Vector3(scene5Vertices[scene5Indices[i + 1] * 3], scene5Vertices[scene5Indices[i + 1] * 3 + 1], scene5Vertices[scene5Indices[i + 1] * 3 + 2]);
                    position += new Vector3(scene5Vertices[scene5Indices[i + 2] * 3], scene5Vertices[scene5Indices[i + 2] * 3 + 1], scene5Vertices[scene5Indices[i + 2] * 3 + 2]);
                    position *= 100f;
                    finalMatrices.Add(Matrix4.CreateTranslation(position));
                    finalMaterials.Add(new GPUMaterial(0.8f, 0f, 0f, 1f, 0f));
                }
                Camera.SetPosition(new Vector3(2000f, 0f, -12000f), 6.6f, 81.2f);
            }
            else if (sceneNumber == 6)
            {
                finalMatrices.Add(Matrix4.Identity);
                finalMaterials.Add(new GPUMaterial(0.04f, 0.17f, 0.10f, 1f, 0f));
                Camera.SetPosition(new Vector3(129f, 48f, -80f), -17.7f, -218f);
            }
        }
    }
}
