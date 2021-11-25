using System;
using System.Collections.Generic;
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
        Vector3 cameraOrigin = new Vector3(0.0f);

        public GPURayTracer ()
        {
            generateProgram = Shader.CreateComputeShaderProgram("../../generate.shader");
            bruteFirstHitProgram = Shader.CreateComputeShaderProgram("../../bruteFirstHit.shader");
            shadingProgram = Shader.CreateComputeShaderProgram("../../shading.shader");

            SetupBuffers(width, height);
        }

        public void updateCamera(Vector3 newPosition)
        {
            cameraOrigin += newPosition;
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
                    (1 + width * height) * (Vector4.SizeInBytes * 20), IntPtr.Zero, BufferUsageHint.StaticDraw);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1 + i, raySSBOs[i]);
            }
            if (shadowRaySSBO != -1) { GL.DeleteBuffer(shadowRaySSBO); }
            shadowRaySSBO = GL.GenBuffer();

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, shadowRaySSBO);
            GL.BufferData(BufferTarget.ShaderStorageBuffer,
               (1 + width * height) * (Vector4.SizeInBytes * 20), IntPtr.Zero, BufferUsageHint.StaticDraw);
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
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f, width, height, 0, PixelFormat.Rgba, PixelType.Float, (IntPtr)null);
            GL.BindImageTexture(0, textureHandle, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);

            SetupAtomics();
        }

        void SetupAtomics()
        {
            if (rayCounterBO == -1)
            {
                rayCounterBO = GL.GenBuffer();
            }
            GL.UseProgram(generateProgram);
            uint[] test = { 0, 0, 0 };
            GL.BindBuffer(BufferTarget.AtomicCounterBuffer, rayCounterBO);
            GL.BufferData(BufferTarget.AtomicCounterBuffer, sizeof(uint) * 3, test, BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.AtomicCounterBuffer, 4, rayCounterBO);

            GL.UseProgram(bruteFirstHitProgram);
            GL.BindBuffer(BufferTarget.AtomicCounterBuffer, rayCounterBO);
            GL.BufferData(BufferTarget.AtomicCounterBuffer, sizeof(uint) * 3, test, BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.AtomicCounterBuffer, 4, rayCounterBO);
        }

        public void GenTex(int width, int height)
        {
            if(width != this.width || height != this.height)
            {
                SetupBuffers(width, height);
                this.width = width;
                this.height = height;
            }
            GL.ClearTexImage(textureHandle, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.UseProgram(generateProgram);
            GL.Uniform3(GL.GetUniformLocation(generateProgram, "cameraOrigin"), cameraOrigin);

            GL.BindBuffer(BufferTarget.AtomicCounterBuffer, rayCounterBO);
            GL.BufferData(BufferTarget.AtomicCounterBuffer, sizeof(uint) * 3, new uint[] { 0, 0, 0 }, BufferUsageHint.StaticDraw);


            int currentBuffer = 0;

            GL.UseProgram(generateProgram);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, raySSBOs[currentBuffer]);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, raySSBOs[currentBuffer]);

            GL.DispatchCompute(width, height, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);

            GL.UseProgram(bruteFirstHitProgram);
            for (int i = 0; i < 3; i++)
            {
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, raySSBOs[currentBuffer]);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, raySSBOs[currentBuffer]);

                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, raySSBOs[1 - currentBuffer]);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, raySSBOs[1 - currentBuffer]);

                //int cid = GL.GetUniformLocation(bruteFirstHitProgram, "counterid");
                //GL.Uniform1(cid, 0);                

                GL.DispatchCompute(262144, 1, 1);
                GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);

                GL.BufferSubData(BufferTarget.AtomicCounterBuffer, new IntPtr(sizeof(uint) * currentBuffer), sizeof(uint), new uint[] { 0 });
                currentBuffer = 1 - currentBuffer;
            }

            GL.UseProgram(shadingProgram);
            GL.DispatchCompute(262144, 1, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
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
