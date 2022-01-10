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
        public static float[] vertices = new float[0];
        public static List<uint> indices = new List<uint>();

        public static void Init()
        {
            float[] hardcodedTriangles = new float[]{
                /*-30f, -50f, -30f,
                30f, -50f, -30f,
                -30f, -50f, 30f,

                30f, -50f, 30f,
                30f, -50f, -30f,
                -30f, -50f, 30f*/
            };

            AssimpContext assimpContext = new AssimpContext();
            string teapot = "../../models/teapot.obj";
            string man = "../../models/man.obj";
            string dragon = "../../models/xyzrgb_dragon.obj";
            Scene model = assimpContext.ImportFile(dragon);

            Console.WriteLine("... : " + model.MeshCount);
            for (int i = 0; i < model.MeshCount; i++)
            {
                List<Vector3D> verticesList = model.Meshes[i].Vertices;
                if(!model.Meshes[i].HasNormals)
                {
                    Console.WriteLine("No normals in this mesh... Setting them all to point up??");
                }

                vertices = new float[model.Meshes[i].Vertices.Count * 3];
                int j = 0;
                while (j < verticesList.Count * 3)
                {
                    Vector3D vertex = model.Meshes[i].Vertices[j / 3];
                    //Vector3D texCoords = model.Meshes[i].TextureCoordinateChannels[0][j / 8];
                    for (int k = 0; k < 3; k++)
                    {
                        vertices[j++] = vertex[k];// * 0.1f;
                    }
                }
                j = 0;
                indices = new List<uint>();
                indices.Add(0);
                indices.Add(0);
                indices.Add(0);

                int skipped = 0;
                int tris = 0;
                while(j < model.Meshes[i].FaceCount)
                {
                    if (model.Meshes[i].Faces[j].IndexCount == 4)
                    {
                        tris += 2;
                        for(int k = 0; k < 3; k++)
                        {
                            indices.Add((uint)model.Meshes[i].Faces[j].Indices[k]);
                        }
                        for(int k = 0; k < 4; k++)
                        {
                            if(k == 1)
                            {
                                continue;
                            }
                            indices.Add((uint)model.Meshes[i].Faces[j].Indices[k]);
                        }
                        indices[0] += 6;
                    }
                    else if (model.Meshes[i].Faces[j].IndexCount == 3)
                    {
                        tris++;
                        for (int k = 0; k < 3; k++)
                        {
                            indices.Add((uint)model.Meshes[i].Faces[j].Indices[k]);
                        }
                        indices[0] += 3;
                    } else
                    {
                        skipped++;
                    }
                    j++;
                }
                Console.WriteLine("Mesh imported, total triangles: " + tris);
                
                if(skipped > 0)
                {
                    Console.WriteLine(skipped + " faces had more than 4 indices, skipping them!!!");
                }
            }

            Array.Resize(ref vertices, vertices.Length + hardcodedTriangles.Length);

            for(int i = hardcodedTriangles.Length; i > 0; i--)
            {
                vertices[vertices.Length - i] = hardcodedTriangles[hardcodedTriangles.Length - i];
            }

            for (int i = vertices.Length - (hardcodedTriangles.Length); i < vertices.Length; i += 3)
            {
                indices.Add((uint)i / 3);
                indices[0]++;
            }
        }
    }
}
