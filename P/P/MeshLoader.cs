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
            /*vertices = new float[8 * 4];
            vertices[0] = 0;
            vertices[1] = 6;
            vertices[2] = 0;
            vertices[3] = 0;
            vertices[4] = 1;
            vertices[5] = 0;

            int test = 8;
            vertices[0 + test] = 10;
            vertices[1 + test] = 6;
            vertices[2 + test] = 0;
            vertices[3 + test] = 0;
            vertices[4 + test] = 1;
            vertices[5 + test] = 0;
            
            test = 16;
            vertices[0 + test] = 0;
            vertices[1 + test] = 6;
            vertices[2 + test] = 10;
            vertices[3 + test] = 0;
            vertices[4 + test] = 1;
            vertices[5 + test] = 0;

            test = 24;
            vertices[0 + test] = 0;
            vertices[1 + test] = 0;
            vertices[2 + test] = 0;
            vertices[3 + test] = 0;
            vertices[4 + test] = 1;
            vertices[5 + test] = 0;

            indices = new List<uint>();
            indices.Add(6);
            indices.Add(0);
            indices.Add(1);
            indices.Add(2);
            indices.Add(0);
            indices.Add(1);
            indices.Add(3);
            return;*/
            AssimpContext assimpContext = new AssimpContext();
            Scene model = assimpContext.ImportFile("../../models/teapot.obj");

            Console.WriteLine("... : " + model.MeshCount);
            for (int i = 0; i < model.MeshCount; i++)
            {
                List<Vector3D> vertices = model.Meshes[i].Vertices;

                MeshLoader.vertices = new float[8 * model.Meshes[i].Vertices.Count];
                int j = 0;
                while(j < vertices.Count * 8)
                {
                    Vector3D vertex = model.Meshes[i].Vertices[j / 8];
                    Vector3D normal = model.Meshes[i].Normals[j / 8];
                    //Vector3D texCoords = model.Meshes[i].TextureCoordinateChannels[0][j / 8];
                    for (int k = 0; k < 3; k++)
                    {
                        MeshLoader.vertices[j++] = vertex[k];
                    }
                    for(int k = 0; k < 3; k++)
                    {
                        MeshLoader.vertices[j++] = normal[k];
                    }
                    for(int k = 0; k < 2; k++)
                    {
                        j++; //MeshLoader.vertices[j++] = texCoords[k];
                    }
                }
                j = 0;
                indices = new List<uint>();
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
                        for(int k = 1; k < 4; k++)
                        {
                            indices.Add((uint)model.Meshes[i].Faces[j].Indices[k]);
                        }
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
        }
    }
}
