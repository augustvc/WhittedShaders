﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace P
{
    class GPURayTracer
    {
        int generateProgram;
        int bruteFirstHitProgram;
        int shadingProgram;
        int[] raySSBOs = { -1, -1 };
        int shadowRaySSBO = -1;
        int rayCounterBO = -1;
        int width = 1;
        int height = 1;
        int textureHandle = -1;
        int samplesSqrt = 2;

        public GPURayTracer ()
        {
            generateProgram = Shader.CreateComputeShaderProgram("#version 460", new string[] { "GPURayTracer/generate.shader" });
            bruteFirstHitProgram = Shader.CreateComputeShaderProgram("#version 430", new string[] { "GPURayTracer/intersect.shader", "GPURayTracer/bruteFirstHit.shader" });
            shadingProgram = Shader.CreateComputeShaderProgram("#version 430", new string[] { "GPURayTracer/intersect.shader", "GPURayTracer/shading.shader" });

            SetupBuffers(width, height);
        }

        void SetupBuffers(int width, int height)
        {
            GL.UseProgram(bruteFirstHitProgram);

            for (int i = 0; i < 2; i++)
            {
                if (raySSBOs[i] != -1) { GL.DeleteBuffer(raySSBOs[i]); }
                raySSBOs[i] = GL.GenBuffer();

                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, raySSBOs[i]);
                GL.BufferData(BufferTarget.ShaderStorageBuffer,
                    (1 + width * height * samplesSqrt * samplesSqrt) * (Vector4.SizeInBytes * 14), IntPtr.Zero, BufferUsageHint.StaticDraw);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1 + i, raySSBOs[i]);
            }
            if (shadowRaySSBO != -1) { GL.DeleteBuffer(shadowRaySSBO); }
            shadowRaySSBO = GL.GenBuffer();

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, shadowRaySSBO);
            GL.BufferData(BufferTarget.ShaderStorageBuffer,
               (1 + width * height * samplesSqrt * samplesSqrt) * (Vector4.SizeInBytes * 14), IntPtr.Zero, BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 3, shadowRaySSBO);

            GL.UseProgram(shadingProgram);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, shadowRaySSBO);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 3, shadowRaySSBO);

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
                
                GL.UseProgram(bruteFirstHitProgram);
                GL.BindBuffer(BufferTarget.AtomicCounterBuffer, rayCounterBO);
                GL.BindBufferBase(BufferRangeTarget.AtomicCounterBuffer, 4, rayCounterBO);

                GL.UseProgram(shadingProgram);
                GL.BindBuffer(BufferTarget.AtomicCounterBuffer, rayCounterBO);
                GL.BindBufferBase(BufferRangeTarget.AtomicCounterBuffer, 4, rayCounterBO);
            }
        }

        public void GenTex(int width, int height)
        {
            if (width != this.width || height != this.height)
            {
                SetupBuffers(width, height);
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
            
            //GL.Uniform1(GL.GetUniformLocation(generateProgram, "aa"), aa);


            GL.BindBuffer(BufferTarget.AtomicCounterBuffer, rayCounterBO);
            GL.BufferData(BufferTarget.AtomicCounterBuffer, sizeof(uint) * 4, new uint[] { 0, 0, 0, 0 }, BufferUsageHint.StaticDraw);
 
            int currentInBuffer = 0;

            GL.UseProgram(generateProgram);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, raySSBOs[currentInBuffer]);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, raySSBOs[currentInBuffer]);

            GL.DispatchCompute(width * samplesSqrt, height * samplesSqrt, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);


            int maximumBounces = 32;
            for (int i = 0; i < maximumBounces; i++)
            {
                //Reset the shadow ray counter
                GL.UseProgram(shadingProgram);
                GL.BindBufferBase(BufferRangeTarget.AtomicCounterBuffer, 4, rayCounterBO);
                GL.BufferSubData(BufferTarget.AtomicCounterBuffer, new IntPtr(sizeof(uint) * 2), sizeof(uint), new uint[] { 0 });

                //Swap the in-ray buffer with the out-ray buffer:
                GL.UseProgram(bruteFirstHitProgram);
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, raySSBOs[currentInBuffer]);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, raySSBOs[currentInBuffer]);

                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, raySSBOs[1 - currentInBuffer]);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, raySSBOs[1 - currentInBuffer]);

                //Reset the counter for the output buffer, so we can start filling it from 0:
                GL.BufferSubData(BufferTarget.AtomicCounterBuffer, new IntPtr(sizeof(uint)), sizeof(uint), new uint[] { 0 });

                //Run the programs
                GL.DispatchCompute(262144 / 64, 1, 1);
                GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);

                GL.UseProgram(shadingProgram);
                GL.DispatchCompute(262144 / 64, 1, 1);
                GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);

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
                GL.DeleteProgram(bruteFirstHitProgram);
                GL.DeleteProgram(shadingProgram);

                disposedValue = true;
            }
        }

        ~GPURayTracer()
        {
            GL.DeleteProgram(generateProgram);
            GL.DeleteProgram(bruteFirstHitProgram);
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
            GL.DeleteTexture(textureHandle);
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
