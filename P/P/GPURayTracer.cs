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
        int outputProgram;
        int firstHitProgram;
        int raySSBO = -1;
        int width = 1;
        int height = 1;
        int textureHandle = -1;
        public GPURayTracer ()
        {
            generateProgram = Shader.CreateComputeShaderProgram("../../generate.shader");
            firstHitProgram = Shader.CreateComputeShaderProgram("../../firstHit.shader");
            outputProgram = Shader.CreateComputeShaderProgram("../../output.shader");

            SetupBuffers(width, height);
        }        

        void SetupBuffers(int width, int height)
        {
            if(raySSBO != -1) { GL.DeleteBuffer(raySSBO); }
            raySSBO = GL.GenBuffer();

            GL.UseProgram(generateProgram);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, raySSBO);
            GL.BufferData(BufferTarget.ShaderStorageBuffer,
               (1 + width * height) * (Vector4.SizeInBytes * 3), IntPtr.Zero, BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 5, raySSBO);

            GL.UseProgram(firstHitProgram);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, raySSBO);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 5, raySSBO);            

            GL.UseProgram(outputProgram);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, raySSBO);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 5, raySSBO);

            if(textureHandle != -1) { GL.DeleteTexture(textureHandle); } //We already had a texture, so delete it.
            textureHandle = GL.GenTexture();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, textureHandle);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f, width, height, 0, PixelFormat.Rgba, PixelType.Float, (IntPtr)null);
            GL.BindImageTexture(0, textureHandle, 0, false, 0, TextureAccess.WriteOnly, SizedInternalFormat.Rgba32f);
        }

        public void GenTex(int width, int height)
        {
            if(width != this.width || height != this.height)
            {
                SetupBuffers(width, height);
            }

            GL.UseProgram(generateProgram);
            GL.DispatchCompute(width, height, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);

            GL.UseProgram(firstHitProgram);
            GL.DispatchCompute(524288, 1, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);

            GL.UseProgram(outputProgram);
            GL.DispatchCompute(width, height, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
        }

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                GL.DeleteProgram(generateProgram);
                GL.DeleteProgram(firstHitProgram);
                GL.DeleteProgram(outputProgram);

                disposedValue = true;
            }
        }

        ~GPURayTracer()
        {
            GL.DeleteProgram(generateProgram);
            GL.DeleteProgram(firstHitProgram);
            GL.DeleteProgram(outputProgram);
        }

        public void Dispose()
        {
            GL.DeleteBuffer(raySSBO);
            GL.DeleteTexture(textureHandle);
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
