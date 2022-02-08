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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace P
{
    class GPURayTracer
    {
        int generateProgram;
        int bvhIntersectionProgram;
        int bounceProgram;
        int shadingProgram;
        int[] raySSBOs = { -1, -1 };
        int shadowRaySSBO = -1;
        int matricesSSBO = -1;
        int materialsSSBO = -1;
        int rayCounterBO = -1;
        int vertexBO = -1;
        int BVHBO = -1;
        int faceBO = -1;
        int width = 1;
        int height = 1;
        int textureHandle = -1;
        int samplesSqrt = 1;
        static int newSamplesSqrt = 1;

        public GPURayTracer()
        {
            generateProgram = Shader.CreateComputeShaderProgram("#version 460", new string[] { "GPURayTracer/generate.shader" });
            bvhIntersectionProgram = Shader.CreateComputeShaderProgram("#version 430", new string[] { "GPURayTracer/BVHIntersect.shader" });
            bounceProgram = Shader.CreateComputeShaderProgram("#version 430", new string[] { "GPURayTracer/bouncer.shader" });
            shadingProgram = Shader.CreateComputeShaderProgram("#version 430", new string[] { "GPURayTracer/shading.shader" });

            SetupRayBuffers(width, height);
        }

        public static void changeSamplesSqrt(int change)
        {
            newSamplesSqrt += change;
            if (newSamplesSqrt < 1)
            {
                newSamplesSqrt = 1;
            }
            if (newSamplesSqrt > 3)
            {
                newSamplesSqrt = 3;
            }
        }

        void SetupRayBuffers(int width, int height)
        {
            GL.UseProgram(bvhIntersectionProgram);

            for (int i = 0; i < 2; i++)
            {
                if (raySSBOs[i] != -1) { GL.DeleteBuffer(raySSBOs[i]); }
                raySSBOs[i] = GL.GenBuffer();

                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, raySSBOs[i]);
                GL.BufferData(BufferTarget.ShaderStorageBuffer,
                    (1 + width * height * samplesSqrt * samplesSqrt) * (Vector4.SizeInBytes * 14), IntPtr.Zero, BufferUsageHint.StaticDraw);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1 + i, raySSBOs[i]);
            }

            matricesSSBO = GL.GenBuffer();
            materialsSSBO = GL.GenBuffer();

            GL.UseProgram(shadingProgram);

            if (shadowRaySSBO != -1) { GL.DeleteBuffer(shadowRaySSBO); }
            shadowRaySSBO = GL.GenBuffer();

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, shadowRaySSBO);
            GL.BufferData(BufferTarget.ShaderStorageBuffer,
               (1 + width * height * samplesSqrt * samplesSqrt) * (Vector4.SizeInBytes * 14), IntPtr.Zero, BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 3, shadowRaySSBO);

            GL.UseProgram(shadingProgram);

            if (textureHandle != -1) { GL.DeleteTexture(textureHandle); } //We already had a texture, so delete it.
            textureHandle = GL.GenTexture();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, textureHandle);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f, width * samplesSqrt, height * samplesSqrt, 0, PixelFormat.Rgba, PixelType.Float, (IntPtr)null);
            GL.BindImageTexture(0, textureHandle, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);

            SetupAtomics();
        }

        public void LoadScene(float[] vertices, List<uint> indices, TopLevelBVH topLevelBVH, int sceneNumber)
        {
            GL.UseProgram(bvhIntersectionProgram);
            if (vertexBO == -1) vertexBO = GL.GenBuffer();
            if (BVHBO == -1) BVHBO = GL.GenBuffer();

            Console.WriteLine("Buffer data: " + vertices.Length);
            Console.WriteLine("First: " + vertices[0]);
            Console.WriteLine("Indices data: " + indices.Count);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, vertexBO);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 5, vertexBO);

            List<GPUBVH> allNodes = new List<GPUBVH>();
            List<uint> newIndices = new List<uint>();

            int addBVH(BVH bvh)
            {
                if (bvh.isLeaf)
                {
                    return -1;
                }
                BVH left = bvh.leftChild;
                BVH right = bvh.rightChild;

                allNodes.Add(new GPUBVH(left.AABBMin, left.AABBMax, right.AABBMin, right.AABBMax));
                int nodeIdx = allNodes.Count - 1;

                int leftOrStart = 0;
                int leftOrEnd = 0;
                int rightOrStart = 0;
                int rightOrEnd = 0;
                if (left.isLeaf)
                {
                    leftOrStart = newIndices.Count;
                    newIndices.AddRange(left.triangleIndices);
                    leftOrEnd = newIndices.Count;
                }
                else
                {
                    leftOrStart = leftOrEnd = addBVH(left);
                }
                if (right.isLeaf)
                {
                    rightOrStart = newIndices.Count;
                    newIndices.AddRange(right.triangleIndices);
                    rightOrEnd = newIndices.Count;
                }
                else
                {
                    rightOrStart = rightOrEnd = addBVH(right);
                }

                allNodes[nodeIdx] = new GPUBVH(allNodes[nodeIdx], leftOrStart, leftOrEnd, rightOrStart, rightOrEnd);
                return nodeIdx;
            }

            addBVH(topLevelBVH.TopBVH);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, faceBO);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, newIndices.Count * sizeof(uint), newIndices.ToArray(), BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 6, faceBO);

            Console.WriteLine("All nodes: " + allNodes.Count);
            Console.WriteLine("All indices: " + newIndices.Count);

            Vector3 lMin = topLevelBVH.TopBVH.leftChild.AABBMin;
            Vector3 lMax = topLevelBVH.TopBVH.leftChild.AABBMax;
            Vector3 rMin = topLevelBVH.TopBVH.rightChild.AABBMin;
            Vector3 rMax = topLevelBVH.TopBVH.rightChild.AABBMax;

            float[] xsL = new float[] { lMin.X, lMax.X };
            float[] ysL = new float[] { lMin.Y, lMax.Y };
            float[] zsL = new float[] { lMin.Z, lMax.Z };

            float[] xsR = new float[] { rMin.X, rMax.X };
            float[] ysR = new float[] { rMin.Y, rMax.Y };
            float[] zsR = new float[] { rMin.Z, rMax.Z };

            List<Vector3> lPoints = new List<Vector3>();
            List<Vector3> rPoints = new List<Vector3>();

            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        lPoints.Add(new Vector3(xsL[x], ysL[y], zsL[z]));
                        rPoints.Add(new Vector3(xsR[x], ysR[y], zsR[z]));
                    }
                }
            }


            List<Matrix4> finalMatrices = new List<Matrix4>();
            List<GPUMaterial> finalMaterials = new List<GPUMaterial>();

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
                for (int i = 0; i < indices.Count; i += 3)
                {
                    Vector3 position = new Vector3(vertices[indices[i] * 3], vertices[indices[i] * 3 + 1], vertices[indices[i] * 3 + 2]);
                    position += new Vector3(vertices[indices[i + 1] * 3], vertices[indices[i + 1] * 3 + 1], vertices[indices[i + 1] * 3 + 2]);
                    position += new Vector3(vertices[indices[i + 2] * 3], vertices[indices[i + 2] * 3 + 1], vertices[indices[i + 2] * 3 + 2]);
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

            List<Vector3> AABBMins = new List<Vector3>();
            List<Vector3> AABBMaxes = new List<Vector3>();
            List<int> objectIds = new List<int>();

            for (int m = 0; m < finalMatrices.Count; m++)
            {
                Vector3 AABBmin = new Vector3(float.MaxValue);
                Vector3 AABBmax = new Vector3(float.MinValue);
                for (int i = 0; i < 8; i++)
                {
                    Vector3 lPoint = lPoints[i];
                    Vector3 rPoint = rPoints[i];

                    Matrix4 matr = finalMatrices[m];
                    matr.Transpose();

                    lPoint = (matr * new Vector4(lPoint, 1.0f)).Xyz;
                    rPoint = (matr * new Vector4(rPoint, 1.0f)).Xyz;

                    AABBmin = Vector3.ComponentMin(AABBmin, lPoint);
                    AABBmax = Vector3.ComponentMax(AABBmax, lPoint);
                    AABBmin = Vector3.ComponentMin(AABBmin, rPoint);
                    AABBmax = Vector3.ComponentMax(AABBmax, rPoint);
                }
                AABBMins.Add(AABBmin);
                AABBMaxes.Add(AABBmax);
                objectIds.Add(m);
            }

            List<GPUBVH> topNodes = new List<GPUBVH>();
            int RecursiveSplit(List<int> objectIndices)
            {
                int bestAxis = 0;
                float bestRange = 0f;
                float splitPosition = 0f;

                Vector3 aabbmin = new Vector3(float.MaxValue);
                Vector3 aabbmax = new Vector3(float.MinValue);

                for (int i = 0; i < objectIndices.Count; i++)
                {
                    int index = objectIndices[i];
                    aabbmin = Vector3.ComponentMin(aabbmin, AABBMins[index]);
                    aabbmax = Vector3.ComponentMax(aabbmax, AABBMaxes[index]);
                }

                for (int k = 0; k < 3; k++)
                {
                    float range = aabbmax[k] - aabbmin[k];
                    if (range > bestRange)
                    {
                        bestAxis = k;
                        bestRange = range;
                        splitPosition = aabbmin[k] + range * 0.5f;
                    }
                }

                List<int> leftList = new List<int>();
                List<int> rightList = new List<int>();

                for (int i = 0; i < objectIndices.Count; i++)
                {
                    int index = objectIndices[i];

                    if (AABBMaxes[index][bestAxis] > splitPosition)
                    {
                        rightList.Add(index);
                    }
                    else
                    {
                        leftList.Add(index);
                    }
                }

                if (leftList.Count == 0)
                {
                    leftList.Add(rightList[rightList.Count - 1]);
                    rightList.RemoveAt(rightList.Count - 1);
                }
                if (rightList.Count == 0)
                {
                    rightList.Add(leftList[leftList.Count - 1]);
                    leftList.RemoveAt(leftList.Count - 1);
                }

                if (objectIndices.Count == 1)
                {
                    //topNodes.Add(new GPUBVH(topLevelBVH.TopBVH.leftChild.AABBMin, topLevelBVH.TopBVH.leftChild.AABBMax, topLevelBVH.TopBVH.rightChild.AABBMin, topLevelBVH.TopBVH.rightChild.AABBMax, allNodes[0].leftOrStart, -1, allNodes[0].rightOrStart, -1));

                    topNodes.Add(new GPUBVH(topLevelBVH.TopBVH.leftChild.AABBMin, topLevelBVH.TopBVH.leftChild.AABBMax, topLevelBVH.TopBVH.rightChild.AABBMin, topLevelBVH.TopBVH.rightChild.AABBMax,
                        allNodes[0].leftOrStart, -objectIndices[0] - 1, allNodes[0].rightOrStart, -objectIndices[0] - 1));


                    //Console.WriteLine("Leaf NODE, id " + objectIndices[0]);
                    return topNodes.Count - 1 + allNodes.Count;
                }

                int leftChild = RecursiveSplit(leftList);
                int rightChild = RecursiveSplit(rightList);

                Vector3 lcmin = new Vector3(float.MaxValue);
                Vector3 lcmax = new Vector3(float.MinValue);
                Vector3 rcmin = new Vector3(float.MaxValue);
                Vector3 rcmax = new Vector3(float.MinValue);

                for (int i = 0; i < leftList.Count; i++)
                {
                    int index = leftList[i];
                    lcmin = Vector3.ComponentMin(lcmin, AABBMins[index]);
                    lcmax = Vector3.ComponentMax(lcmax, AABBMaxes[index]);
                }
                for (int i = 0; i < rightList.Count; i++)
                {
                    int index = rightList[i];
                    rcmin = Vector3.ComponentMin(rcmin, AABBMins[index]);
                    rcmax = Vector3.ComponentMax(rcmax, AABBMaxes[index]);
                }

                topNodes.Add(new GPUBVH(lcmin, lcmax, rcmin, rcmax, leftChild, leftChild, rightChild, rightChild));

                return topNodes.Count - 1 + allNodes.Count;
            }

            int root = RecursiveSplit(objectIds);
            GL.ProgramUniform1(bvhIntersectionProgram, 3, root);

            Console.WriteLine("Top nodes count: " + topNodes.Count);

            for (int i = 0; i < topNodes.Count; i++)
            {
                allNodes.Add(topNodes[i]);
            }

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, BVHBO);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, 16 * 4 * allNodes.Count, allNodes.ToArray(), BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 7, BVHBO);


            //Send the transformation matrices for the objects to the gpu:
            GL.UseProgram(bvhIntersectionProgram);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, matricesSSBO);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 8, matricesSSBO);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, finalMatrices.Count * 16 * sizeof(float), finalMatrices.ToArray(), BufferUsageHint.StaticDraw);

            GL.UseProgram(bounceProgram);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, materialsSSBO);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 9, materialsSSBO);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, finalMaterials.Count * sizeof(float) * 5, finalMaterials.ToArray(), BufferUsageHint.StaticDraw);

            GL.UseProgram(bounceProgram);
            Console.WriteLine("GL uniform to " + vertices.Length / 2);
            GL.Uniform1(2, vertices.Length / 2);
        }

        void SetupAtomics()
        {
            if (rayCounterBO == -1)
            {
                rayCounterBO = GL.GenBuffer();
                GL.UseProgram(generateProgram);
                uint[] test = { 0, 0, 0, 0 };
                GL.BindBuffer(BufferTarget.AtomicCounterBuffer, rayCounterBO);
                GL.BufferData(BufferTarget.AtomicCounterBuffer, sizeof(uint) * 4, test, BufferUsageHint.StaticDraw);
                GL.BindBufferBase(BufferRangeTarget.AtomicCounterBuffer, 4, rayCounterBO);

                GL.UseProgram(bvhIntersectionProgram);
                GL.BindBuffer(BufferTarget.AtomicCounterBuffer, rayCounterBO);
                GL.BindBufferBase(BufferRangeTarget.AtomicCounterBuffer, 4, rayCounterBO);
            }
        }

        static public double firstShadowRayTime = 0.0;
        static public double totalPrimaryRayFirstHitTime = 0.0;
        static public double totalRayFirstHitTime = 0.0;
        static public double totalShadowRayTime = 0.0;
        static public double totalGenRayTime = 0.0;
        static public double totalBouncerTime = 0.0;
        static public int totalFrames = 0;

        public void GenTex(int width, int height)
        {
            //Generate a texture with a width and height matching the screen's width and height. This texture is used to display our ray-traced pixels.

            
            if (samplesSqrt != newSamplesSqrt)
            {
                //Multi sample anti-aliasing parameters changed
                samplesSqrt = newSamplesSqrt;
                SetupRayBuffers(width, height);
            }
            if (width != this.width || height != this.height)
            {
                //Screen was resized
                SetupRayBuffers(width, height);
                this.width = width;
                this.height = height;
            }

            GL.BindTexture(TextureTarget.Texture2D, textureHandle);
            GL.ClearTexImage(textureHandle, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.UseProgram(generateProgram);

            //Update camera position and screen plane
            GL.Uniform3(GL.GetUniformLocation(generateProgram, "cameraOrigin"), Camera.getCameraPosition());
            Vector3 yRange = Camera.getCameraUp() * 2 * ((float)height / (float)width);
            GL.Uniform3(GL.GetUniformLocation(generateProgram, "p1"), Camera.getCameraFront() - Camera.getCameraRight() - yRange / 2.0f);
            GL.Uniform3(GL.GetUniformLocation(generateProgram, "xArm"), Camera.getCameraRight() * 2);
            GL.Uniform3(GL.GetUniformLocation(generateProgram, "yArm"), yRange);

            GL.BindBuffer(BufferTarget.AtomicCounterBuffer, rayCounterBO);
            GL.BufferData(BufferTarget.AtomicCounterBuffer, sizeof(uint) * 4, new uint[] { 0, (uint)(width * samplesSqrt * height * samplesSqrt), 0, 0 }, BufferUsageHint.StaticDraw);

            int currentInBuffer = 0;

            GL.UseProgram(generateProgram);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, raySSBOs[currentInBuffer]);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, raySSBOs[currentInBuffer]);

            Stopwatch genSW = new Stopwatch();
            genSW.Start();
            GL.DispatchCompute((width * samplesSqrt) / 32, (height * samplesSqrt), 1);

            GL.Finish();
            genSW.Stop();
            totalGenRayTime += genSW.Elapsed.TotalMilliseconds;
            totalFrames++;


            int maximumBounces = 8;
            for (int i = 0; i < maximumBounces; i++)
            {
                //Reset the shadow ray counter
                GL.UseProgram(shadingProgram);
                GL.BindBufferBase(BufferRangeTarget.AtomicCounterBuffer, 4, rayCounterBO);
                GL.BufferSubData(BufferTarget.AtomicCounterBuffer, new IntPtr(sizeof(uint) * 2), sizeof(uint), new uint[] { 0 });

                //Swap the in-ray buffer with the out-ray buffer:
                GL.UseProgram(bvhIntersectionProgram);
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, raySSBOs[currentInBuffer]);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, raySSBOs[currentInBuffer]);

                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, raySSBOs[1 - currentInBuffer]);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, raySSBOs[1 - currentInBuffer]);

                //Reset the counter for the output buffer, so we can start filling it from 0:
                GL.BufferSubData(BufferTarget.AtomicCounterBuffer, new IntPtr(sizeof(uint)), sizeof(uint), new uint[] { 0 });
                //Reset the intersection job counter
                GL.BufferSubData(BufferTarget.AtomicCounterBuffer, new IntPtr(sizeof(uint) * 3), sizeof(uint), new uint[] { 0 });

                Stopwatch sw = new Stopwatch();
                sw.Start();

                //Run the programs
                GL.Uniform1(1, 0);
                GL.DispatchCompute(8704 * 4 / 64, 1, 1);
                GL.Finish();
                sw.Stop();
                if (i == 0)
                {
                    totalPrimaryRayFirstHitTime += sw.Elapsed.TotalMilliseconds;
                }
                totalRayFirstHitTime += sw.Elapsed.TotalMilliseconds;

                sw.Restart();
                GL.UseProgram(bounceProgram);
                GL.DispatchCompute(262144 / 64, 1, 1);
                GL.Finish();
                sw.Stop();
                totalBouncerTime += sw.Elapsed.TotalMilliseconds;

                sw.Restart();
                GL.UseProgram(bvhIntersectionProgram);
                //Reset the intersection job counter
                GL.BufferSubData(BufferTarget.AtomicCounterBuffer, new IntPtr(sizeof(uint) * 3), sizeof(uint), new uint[] { 0 });
                GL.Uniform1(1, 1);
                GL.DispatchCompute(8704 * 4 / 64, 1, 1);
                GL.Finish();
                sw.Stop();
                if (i == 0)
                {
                    firstShadowRayTime += sw.Elapsed.TotalMilliseconds;
                }
                totalShadowRayTime += sw.Elapsed.TotalMilliseconds;

                GL.UseProgram(shadingProgram);
                GL.DispatchCompute(262144 / 64, 1, 1);
                GL.Finish();

                //Swap counters
                GL.CopyBufferSubData(BufferTarget.AtomicCounterBuffer, BufferTarget.AtomicCounterBuffer,
                    IntPtr.Zero, new IntPtr(sizeof(uint) * 3), sizeof(uint));
                GL.CopyBufferSubData(BufferTarget.AtomicCounterBuffer, BufferTarget.AtomicCounterBuffer,
                    new IntPtr(sizeof(uint)), IntPtr.Zero, sizeof(uint));
                GL.CopyBufferSubData(BufferTarget.AtomicCounterBuffer, BufferTarget.AtomicCounterBuffer,
                    new IntPtr(sizeof(uint) * 3), new IntPtr(sizeof(uint)), sizeof(uint));

                currentInBuffer = 1 - currentInBuffer;
            }
        }

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                GL.DeleteProgram(generateProgram);
                GL.DeleteProgram(bvhIntersectionProgram);
                GL.DeleteProgram(bounceProgram);
                GL.DeleteProgram(shadingProgram);

                disposedValue = true;
            }
        }

        ~GPURayTracer()
        {
            GL.DeleteProgram(generateProgram);
            GL.DeleteProgram(bvhIntersectionProgram);
            GL.DeleteProgram(bounceProgram);
            GL.DeleteProgram(shadingProgram);
        }

        public void Dispose()
        {
            for (int i = 0; i < 2; i++)
            {
                GL.DeleteBuffer(raySSBOs[i]);
            }
            GL.DeleteBuffer(shadowRaySSBO);
            GL.DeleteBuffer(matricesSSBO);
            GL.DeleteBuffer(materialsSSBO);
            GL.DeleteBuffer(rayCounterBO);
            GL.DeleteBuffer(vertexBO);
            GL.DeleteBuffer(BVHBO);
            GL.DeleteBuffer(faceBO);
            GL.DeleteTexture(textureHandle);
            Dispose(true);
            GC.SuppressFinalize(this);
        }




    }

    struct GPUMaterial
    {
        float red;
        float green;
        float blue;
        float diffuse;
        float specular;
        public GPUMaterial(float r, float g, float b, float diff, float spec)
        {
            red = r; green = g; blue = b;
            diffuse = diff;
            specular = spec;
        }
    }

    unsafe struct GPUBVH
    {
        public fixed float AABBs[12];

        public int leftOrStart;
        public int leftOrEnd;

        public int rightOrStart;
        public int rightOrEnd;

        public GPUBVH(GPUBVH oldVersion, int leftOrStartp, int leftOrEndp, int rightOrStartp, int rightOrEndp)
        {
            this = oldVersion;
            leftOrStart = leftOrStartp;
            leftOrEnd = leftOrEndp;
            rightOrStart = rightOrStartp;
            rightOrEnd = rightOrEndp;
        }

        public GPUBVH(Vector3 AABBMinL, Vector3 AABBMaxL, Vector3 AABBMinR, Vector3 AABBMaxR)
        {
            AABBs[0] = AABBMinL.X;
            AABBs[1] = AABBMinL.Y;
            AABBs[2] = AABBMinL.Z;
            AABBs[3] = AABBMaxL.X;
            AABBs[4] = AABBMaxL.Y;
            AABBs[5] = AABBMaxL.Z;
            AABBs[6] = AABBMinR.X;
            AABBs[7] = AABBMinR.Y;
            AABBs[8] = AABBMinR.Z;
            AABBs[9] = AABBMaxR.X;
            AABBs[10] = AABBMaxR.Y;
            AABBs[11] = AABBMaxR.Z;

            leftOrStart = 0;
            leftOrEnd = 3;
            rightOrStart = 0;
            rightOrEnd = 3;
        }

        public GPUBVH(Vector3 AABBMinL, Vector3 AABBMaxL, Vector3 AABBMinR, Vector3 AABBMaxR, int leftOrStartp, int leftOrEndp, int rightOrStartp, int rightOrEndp)
        {
            AABBs[0] = AABBMinL.X;
            AABBs[1] = AABBMinL.Y;
            AABBs[2] = AABBMinL.Z;
            AABBs[3] = AABBMaxL.X;
            AABBs[4] = AABBMaxL.Y;
            AABBs[5] = AABBMaxL.Z;
            AABBs[6] = AABBMinR.X;
            AABBs[7] = AABBMinR.Y;
            AABBs[8] = AABBMinR.Z;
            AABBs[9] = AABBMaxR.X;
            AABBs[10] = AABBMaxR.Y;
            AABBs[11] = AABBMaxR.Z;

            leftOrStart = leftOrStartp;
            leftOrEnd = leftOrEndp;
            rightOrStart = rightOrStartp;
            rightOrEnd = rightOrEndp;
        }
    }
}