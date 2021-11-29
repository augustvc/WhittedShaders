using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assimp;

namespace P
{
    static class MeshLoader
    {
        public static float[] mesh = new float[0];

        public static void Init()
        {
            AssimpContext assimpContext = new AssimpContext();
            Scene model = assimpContext.ImportFile("../../models/Anvil.obj");

            Console.WriteLine("... : " + model.MeshCount);
            for (int i = 0; i < model.MeshCount; i++)
            {
                Console.WriteLine("Mesh " + i + ": " + model.Meshes[i].Name);
            }
        }
    }
}
