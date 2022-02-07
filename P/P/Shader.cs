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

        static string readShader(string path)
        {
            string prefix = "";
            for(int i = 0; i < 20; i++)
            {
                try
                {
                    StreamReader reader = new StreamReader(prefix + path);
                    return reader.ReadToEnd();
                }
                catch (Exception)
                {
                    prefix = prefix + "../";
                }
            }
            Console.WriteLine("Error while trying to read shader at " + path);
            return "";
        }

        public Shader(string vertexPath, string fragmentPath)
        {
            string vsrc = readShader(vertexPath);
            string fsrc = readShader(fragmentPath);

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
        public static int CreateComputeShaderProgram(string versionString, string[] paths)
        {
            int programHandle = GL.CreateProgram();

            int shaderHandle = GL.CreateShader(ShaderType.ComputeShader);
            string shaderSrc = versionString + "\n";
            for (int i = 0; i < paths.Length; i++)
            {
                shaderSrc += readShader(paths[i]);
            }
            GL.ShaderSource(shaderHandle, shaderSrc);
            GL.CompileShader(shaderHandle);

            string log = GL.GetShaderInfoLog(shaderHandle);
            if (log != String.Empty)
            {
                Console.WriteLine("Error when compiling combination of shader with version " + versionString + " followed by");
                for (int i = 0; i < paths.Length; i++)
                {
                    Console.WriteLine(paths[i]);
                }
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
