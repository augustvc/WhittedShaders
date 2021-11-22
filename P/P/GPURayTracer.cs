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
        int changeProgram;
        int outputProgram;
        int ssbo;
        int primaryRaySSBO;
        public GPURayTracer ()
        {
            int generateShader = GL.CreateShader(ShaderType.ComputeShader);
            string genShaderSrc;
            using (StreamReader reader = new StreamReader("../../generate.shader", Encoding.UTF8))
            {
                genShaderSrc = reader.ReadToEnd();
            }
            GL.ShaderSource(generateShader, genShaderSrc);
            GL.CompileShader(generateShader);

            string log = GL.GetShaderInfoLog(generateShader);
            if (log != String.Empty)
            {
                Console.WriteLine(log);
            }
            int status;
            GL.GetShader(generateShader, ShaderParameter.CompileStatus, out status);
            //Console.WriteLine("Genshader Compile status: " + status);

            int shader2 = GL.CreateShader(ShaderType.ComputeShader);

            string shaderSrc;
            using (StreamReader reader = new StreamReader("../../output.shader", Encoding.UTF8))
            {
                shaderSrc = reader.ReadToEnd();
            }
            GL.ShaderSource(shader2, shaderSrc);
            GL.CompileShader(shader2);
            String log2 = GL.GetShaderInfoLog(shader2);
            if (log2 != String.Empty)
            {
                Console.WriteLine(log2);
            }

            changeProgram = GL.CreateProgram();
            GL.AttachShader(changeProgram, generateShader);
            GL.LinkProgram(changeProgram);
            GL.DetachShader(changeProgram, generateShader);
            GL.DeleteShader(generateShader);
            GL.GetProgram(changeProgram, GetProgramParameterName.LinkStatus, out status);

            outputProgram = GL.CreateProgram();
            GL.AttachShader(outputProgram, shader2);
            GL.LinkProgram(outputProgram);
            GL.DetachShader(outputProgram, shader2);
            GL.DeleteShader(shader2);
            ssbo = GL.GenBuffer();
            primaryRaySSBO = GL.GenBuffer();
        }
        public int GenTex(int width, int height)
        {
            int tex = GL.GenTexture();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, tex);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f, width, height, 0, PixelFormat.Rgba, PixelType.Float, (IntPtr)null);
            GL.BindImageTexture(0, tex, 0, false, 0, TextureAccess.WriteOnly, SizedInternalFormat.Rgba32f);

            GL.UseProgram(changeProgram);
            
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, width * height * Vector4.SizeInBytes, IntPtr.Zero, BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 6, ssbo);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, primaryRaySSBO);
            GL.BufferData(BufferTarget.ShaderStorageBuffer,
                width * height * (Vector4.SizeInBytes * 2 + sizeof(float)), IntPtr.Zero, BufferUsageHint.StaticDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 5, primaryRaySSBO);

            GL.DispatchCompute(width, height, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);

            GL.UseProgram(outputProgram);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 6, ssbo);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, primaryRaySSBO);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 5, primaryRaySSBO);

            GL.DispatchCompute(width, height, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);

            return tex;
        }

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                GL.DeleteProgram(changeProgram);
                GL.DeleteProgram(outputProgram);

                disposedValue = true;
            }
        }

        ~GPURayTracer()
        {
            GL.DeleteProgram(changeProgram);
            GL.DeleteProgram(outputProgram);
        }

        public void Dispose()
        {
            GL.DeleteBuffer(ssbo);
            GL.DeleteBuffer(primaryRaySSBO);
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
