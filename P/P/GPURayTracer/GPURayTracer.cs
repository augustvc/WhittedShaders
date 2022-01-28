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
        int rayCounterBO = -1;
        int vertexBO = -1;
        int BVHBO = -1;
        int faceBO = -1;
        int width = 1;
        int height = 1;
        int textureHandle = -1;
        int samplesSqrt = 1;

        public GPURayTracer ()
        {
            generateProgram = Shader.CreateComputeShaderProgram("#version 460", new string[] { "GPURayTracer/generate.shader" });
            bvhIntersectionProgram = Shader.CreateComputeShaderProgram("#version 430", new string[] { "GPURayTracer/BVHIntersect.shader" });
            bounceProgram = Shader.CreateComputeShaderProgram("#version 430", new string[] { "GPURayTracer/bouncer.shader" });
            shadingProgram = Shader.CreateComputeShaderProgram("#version 430", new string[] { "GPURayTracer/shading.shader" });

            SetupRayBuffers(width, height);
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

        public void SetupTriangleBuffers(float[] vertices, List<uint> indices, TopLevelBVH topLevelBVH)
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
                if(left.isLeaf)
                {
                    leftOrStart = newIndices.Count;
                    newIndices.AddRange(left.triangleIndices);
                    leftOrEnd = newIndices.Count;
                } else
                {
                    leftOrStart = leftOrEnd = addBVH(left);
                }
                if(right.isLeaf)
                {
                    rightOrStart = newIndices.Count;
                    newIndices.AddRange(right.triangleIndices);
                    rightOrEnd = newIndices.Count;
                } else
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

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, BVHBO);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, 16 * 4 * allNodes.Count, allNodes.ToArray(), BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 7, BVHBO);

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
        static public double totalGenRayTime= 0.0;
        static public double totalBouncerTime = 0.0;
        static public int totalFrames = 0;
        public void GenTex(int width, int height)
        {
            if (width != this.width || height != this.height)
            {
                SetupRayBuffers(width, height);
                this.width = width;
                this.height = height;
            }

            GL.BindTexture(TextureTarget.Texture2D, textureHandle);
            GL.ClearTexImage(textureHandle, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.UseProgram(generateProgram);

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
            //GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
            //GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
            GL.Finish();
            genSW.Stop();
            totalGenRayTime += genSW.Elapsed.TotalMilliseconds;
            totalFrames++;
            //Console.WriteLine("Generating rays took " + genSW.ElapsedMilliseconds + " ms");

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
                //Console.WriteLine("Finishing up time: " + sw.Elapsed);
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
            GL.DeleteBuffer(rayCounterBO);
            GL.DeleteBuffer(vertexBO);
            GL.DeleteBuffer(BVHBO);
            GL.DeleteBuffer(faceBO);
            GL.DeleteTexture(textureHandle);
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    unsafe struct GPUBVH
    {
        //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
        fixed float AABBs[12];

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
