﻿using System;
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
        int firstHitProgram;
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
            firstHitProgram = Shader.CreateComputeShaderProgram("#version 430", new string[] { "GPURayTracer/BVHFirstHit.shader" });
            bounceProgram = Shader.CreateComputeShaderProgram("#version 430", new string[] { "GPURayTracer/bouncer.shader" });
            shadingProgram = Shader.CreateComputeShaderProgram("#version 430", new string[] { "GPURayTracer/BVHIntersect.shader", "GPURayTracer/shading.shader" });

            SetupRayBuffers(width, height);
        }

        void SetupRayBuffers(int width, int height)
        {
            GL.UseProgram(firstHitProgram);

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
            GL.UseProgram(firstHitProgram);
            if (vertexBO == -1) vertexBO = GL.GenBuffer();
            if (BVHBO == -1) BVHBO = GL.GenBuffer();

            Console.WriteLine("Buffer data: " + vertices.Length);
            Console.WriteLine("First: " + vertices[0]);
            Console.WriteLine("Indices data: " + indices.Count);
            Console.WriteLine("First: " + indices[0]);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, vertexBO);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 5, vertexBO);


            List<BVH> order = new List<BVH>();
            Dictionary<int, int> nrToOrder = new Dictionary<int, int>();
            List<BVH> stack = new List<BVH>();

            stack.Add(topLevelBVH.TopBVH);

            while(stack.Count > 0)
            {
                BVH next = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);

                order.Add(next);
                nrToOrder.Add(next.nodeNumber, order.Count - 1);

                if(!next.isLeaf)
                {
                    stack.Add(next.leftChild);
                    stack.Add(next.rightChild);
                }
            }

            GPUBVH[] allNodes = new GPUBVH[order.Count];
            List<uint> newIndices = new List<uint>();
            for (int i = 0; i < order.Count; i++)
            {
                if (order[i].isLeaf)
                {
                    allNodes[i] = new GPUBVH(order[i].AABBMin, order[i].AABBMax, newIndices.Count, newIndices.Count + order[i].triangleIndices.Length, 0, 0);
                    newIndices.AddRange(order[i].triangleIndices);
                } else
                {
                    allNodes[i] = new GPUBVH(order[i].AABBMin, order[i].AABBMax, 0, 0, nrToOrder[order[i].leftChild.nodeNumber], nrToOrder[order[i].rightChild.nodeNumber]);
                }                 
            }

            void setParent(int idx, int parent)
            {
                allNodes[idx] = new GPUBVH(allNodes[idx], parent);
            }

            //Set parent numbers:
            for (int i = 0; i < order.Count; i++)
            {
                if(!order[i].isLeaf)
                {
                    setParent(nrToOrder[order[i].leftChild.nodeNumber], i);
                    setParent(nrToOrder[order[i].rightChild.nodeNumber], i);
                }
            }

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, faceBO);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, newIndices.Count * sizeof(uint), newIndices.ToArray(), BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 6, faceBO);

            //gPUBVHs = new List<GPUBVH>();
            //gPUBVHs.Add(new GPUBVH(new Vector3(float.MaxValue), new Vector3(float.MinValue), 0, 2));
            //gPUBVHs.Add(new GPUBVH(new Vector3(float.MinValue), new Vector3(float.MaxValue), 0, newIndices.Count));

            Console.WriteLine("All nodes: " + allNodes.Length);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, BVHBO);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, 11 * 4 * allNodes.Length, allNodes.ToArray(), BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 7, BVHBO);

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
                
                GL.UseProgram(firstHitProgram);
                GL.BindBuffer(BufferTarget.AtomicCounterBuffer, rayCounterBO);
                GL.BindBufferBase(BufferRangeTarget.AtomicCounterBuffer, 4, rayCounterBO);
            }
        }

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

            GL.DispatchCompute(width * samplesSqrt / 32, height * samplesSqrt, 1);
            //GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
            //GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
            GL.Finish();


            int maximumBounces = 2;
            for (int i = 0; i < maximumBounces; i++)
            {
                //Reset the shadow ray counter
                GL.UseProgram(shadingProgram);
                GL.BindBufferBase(BufferRangeTarget.AtomicCounterBuffer, 4, rayCounterBO);
                GL.BufferSubData(BufferTarget.AtomicCounterBuffer, new IntPtr(sizeof(uint) * 2), sizeof(uint), new uint[] { 0 });

                //Swap the in-ray buffer with the out-ray buffer:
                GL.UseProgram(firstHitProgram);
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, raySSBOs[currentInBuffer]);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, raySSBOs[currentInBuffer]);

                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, raySSBOs[1 - currentInBuffer]);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, raySSBOs[1 - currentInBuffer]);

                //Reset the counter for the output buffer, so we can start filling it from 0:
                GL.BufferSubData(BufferTarget.AtomicCounterBuffer, new IntPtr(sizeof(uint)), sizeof(uint), new uint[] { 0 });
                //Reset the intersection job and shadow ray job counters
                GL.BufferSubData(BufferTarget.AtomicCounterBuffer, new IntPtr(sizeof(uint) * 3), sizeof(uint), new uint[] { 0 });

                //Run the programs
                GL.DispatchCompute(8704 * 2, 1, 1);
                //GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
                //GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
                GL.Finish();

                GL.UseProgram(bounceProgram);
                GL.DispatchCompute(262144 / 64, 1, 1);
                GL.Finish();

                GL.UseProgram(shadingProgram);
                GL.DispatchCompute(262144 / 64, 1, 1);
                //GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
                //GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);
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
                GL.DeleteProgram(firstHitProgram);
                GL.DeleteProgram(bounceProgram);
                GL.DeleteProgram(shadingProgram);

                disposedValue = true;
            }
        }

        ~GPURayTracer()
        {
            GL.DeleteProgram(generateProgram);
            GL.DeleteProgram(firstHitProgram);
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

    struct GPUBVH
    {
        public float minX;
        public float minY;
        public float minZ;
        public float maxX;
        public float maxY;
        public float maxZ;

        public int indicesStart;
        public int indicesEnd;

        public int parent;
        public int leftChild;
        public int rightChild;

        public GPUBVH(GPUBVH parentless, int parent)
        {
            this = parentless;
            this.parent = parent;
        }

        public GPUBVH(Vector3 AABBMin, Vector3 AABBMax, int indicesStart, int indicesEnd, int leftChild, int rightChild)
        {
            minX = AABBMin.X;
            minY = AABBMin.Y;
            minZ = AABBMin.Z;
            maxX = AABBMax.X;
            maxY = AABBMax.Y;
            maxZ = AABBMax.Z;

            this.indicesStart = indicesStart;
            this.indicesEnd = indicesEnd;

            this.parent = -1;
            this.leftChild = leftChild;
            this.rightChild = rightChild;
        }
    }
}
