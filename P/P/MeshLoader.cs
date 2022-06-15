/*
Copyright 2022 August van Casteren & Shreyes Jishnu Suchindran

You may use this software freely for non-commercial purposes. For any commercial purpose, please contact the authors.

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assimp;
using OpenTK;

namespace P
{
    static class MeshLoader
    {

        public static Mesh LoadMesh(string fileName)
        {
            float[] vertices = new float[0];
            List<uint> indices = new List<uint>();
            
            float[] hardcodedTriangles = new float[]{
               /* -300f, -50f, -300f,
                -300f, -50f, 300f,
                300f, -50f, -300f,

                300f, -50f, 300f,
                300f, -50f, -300f,
                -300f, -50f, 300f*/
            };

            AssimpContext assimpContext = new AssimpContext();
            string filePath = "../../models/";

            Scene model = assimpContext.ImportFile(filePath + fileName, PostProcessSteps.JoinIdenticalVertices);
            
            Console.WriteLine("... : " + model.MeshCount);
            for (int i = 0; i < model.MeshCount; i++)
            {
                List<Vector3D> verticesList = model.Meshes[i].Vertices;

                vertices = new float[model.Meshes[i].Vertices.Count * 6];
                int j = 0;
                while (j < verticesList.Count * 3)
                {
                    Vector3D vertex = model.Meshes[i].Vertices[j / 3];
                    for (int k = 0; k < 3; k++)
                    {
                        vertices[j++] = vertex[k];
                    }
                }
                j = 0;
                indices = new List<uint>();

                int skipped = 0;
                int tris = 0;
                while(j < model.Meshes[i].FaceCount)
                {
                    if (model.Meshes[i].Faces[j].IndexCount == 4)
                    {
                        //Renderer does not support quads, so we create 2 triangles instead.
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
                    }
                    else if (model.Meshes[i].Faces[j].IndexCount == 3)
                    {
                        tris++;
                        for (int k = 0; k < 3; k++)
                        {
                            indices.Add((uint)model.Meshes[i].Faces[j].Indices[k]);
                        }
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

            //Add hardcoded triangles to the mesh:
            Array.Resize(ref vertices, vertices.Length + hardcodedTriangles.Length*2);

            for(int i = hardcodedTriangles.Length; i > 0; i--)
            {
                vertices[vertices.Length / 2 - i] = hardcodedTriangles[hardcodedTriangles.Length - i];
            }

            for (int i = vertices.Length / 2 - (hardcodedTriangles.Length); i < vertices.Length / 2; i += 3)
            {
                indices.Add((uint)i / 3);
            }

            //Add normals
            uint[] indicesOccurence = new uint[(vertices.Length / 2) / 3];
            Dictionary<uint, Vector3> indicesTotal = new Dictionary<uint, Vector3>();
            for(int i = 0; i < indices.Count; i+=3)
            {
                uint triAI = indices[i];
                uint triBI = indices[i+1];
                uint triCI = indices[i+2];
                Vector3 triA = new Vector3(vertices[triAI * 3], vertices[triAI * 3 + 1], vertices[triAI * 3 + 2]);
                Vector3 triB = new Vector3(vertices[triBI * 3], vertices[triBI * 3 + 1], vertices[triBI * 3 + 2]);
                Vector3 triC = new Vector3(vertices[triCI * 3], vertices[triCI * 3 + 1], vertices[triCI * 3 + 2]);
                Vector3 normal = (Vector3.Cross(triC - triA, triB - triA)).Normalized();
                indicesOccurence[triAI]++;
                indicesOccurence[triBI]++;
                indicesOccurence[triCI]++;

                if (!indicesTotal.ContainsKey(triAI))
                {
                    indicesTotal.Add(triAI, normal);
                } else { 
                    indicesTotal[triAI] += normal;
                }
                if (!indicesTotal.ContainsKey(triBI))
                {
                    indicesTotal.Add(triBI, normal);
                } else {
                    indicesTotal[triBI] += normal;
                }
                if (!indicesTotal.ContainsKey(triCI))
                {
                    indicesTotal.Add(triCI, normal);
                } else {
                    indicesTotal[triCI] += normal;
                }
            }

            for (uint i = 0; i < indicesOccurence.Length; i++)
            {
                if (indicesTotal.ContainsKey(i))
                {
                    for (int k = 0; k < 3; k++)
                    {
                        vertices[vertices.Length / 2 + (i * 3) + k] = -indicesTotal[i][k] / indicesOccurence[i];
                    }
                }
            }

            return new Mesh(vertices, indices);
        }
    }
}
