using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL;

namespace P
{
    class Shader : IDisposable
    {
        int Handle;

        public Shader(string vertexPath, string fragmentPath)
        {
            string vsrc;
            using (StreamReader reader = new StreamReader(vertexPath, Encoding.UTF8))
            {
                vsrc = reader.ReadToEnd();
            }

            string fsrc;
            using (StreamReader reader = new StreamReader(fragmentPath, Encoding.UTF8))
            {
                fsrc = reader.ReadToEnd();
            }

            int VertexShader = GL.CreateShader(ShaderType.VertexShader);
            int FragmentShader = GL.CreateShader(ShaderType.FragmentShader);

            GL.ShaderSource(VertexShader, vsrc);
            GL.ShaderSource(FragmentShader, fsrc);

            GL.CompileShader(VertexShader);

            string vlog = GL.GetShaderInfoLog(VertexShader);
            if(vlog != String.Empty)
            {
                Console.WriteLine(vlog);
            }

            GL.CompileShader(FragmentShader);

            string flog = GL.GetShaderInfoLog(FragmentShader);
            if (flog != String.Empty)
            {
                Console.WriteLine(flog);
            }

            Handle = GL.CreateProgram();

            GL.AttachShader(Handle, VertexShader);
            GL.AttachShader(Handle, FragmentShader);
            GL.LinkProgram(Handle);

            GL.DetachShader(Handle, VertexShader);
            GL.DetachShader(Handle, FragmentShader);
            GL.DeleteShader(FragmentShader);
            GL.DeleteShader(VertexShader);
        }

        public void Use()
        {
            GL.UseProgram(Handle);
        }
        public int GetAttribLocation(string attribName)
        {
            return GL.GetAttribLocation(Handle, attribName);
        }

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                GL.DeleteProgram(Handle);

                disposedValue = true;
            }
        }

        ~Shader()
        {
            GL.DeleteProgram(Handle);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        public static int CreateComputeShaderProgram(string path)
        {
            int programHandle = GL.CreateProgram();

            int shaderHandle = GL.CreateShader(ShaderType.ComputeShader);
            string shaderSrc;
            using (StreamReader reader = new StreamReader(path, Encoding.UTF8))
            {
                shaderSrc = reader.ReadToEnd();
            }
            GL.ShaderSource(shaderHandle, shaderSrc);
            GL.CompileShader(shaderHandle);

            string log = GL.GetShaderInfoLog(shaderHandle);
            if (log != String.Empty)
            {
                Console.WriteLine(log);
            }

            GL.AttachShader(programHandle, shaderHandle);
            GL.LinkProgram(programHandle);
            GL.DetachShader(programHandle, shaderHandle);
            GL.DeleteShader(shaderHandle);

            return programHandle;
        }
    }
}
