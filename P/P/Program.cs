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
using OpenTK.Graphics;
using OpenTK.Input;
using OpenTK.Graphics.OpenGL;
using OpenTK;
using System.Threading;
using System.Timers;
using System.Diagnostics;

namespace P
{
    class Game : GameWindow
    {
        double renderCheckTime = 2.0;
        double renderCheckInterval = 5.0;

        double totalGPUTime;
        long GPUFrameCounter;

        static bool useGPU = true;
        static void Main(string[] args)
        {
            string title = "CPU Ray Tracer";
            if (useGPU)
            {
                title = "GPU Ray Tracer";
            }
            using (Game game = new Game(1920, 1000, title))
            {
                //game.VSync = VSyncMode.Off;
                game.Run(0.0, 0.0);
            }
        }

        int VBO, VAO;

        float[] screenEdgeVertices = {
            -1.0f, -1.0f, 0.0f, 0.0f, 0.0f,
             1.0f, -1.0f, 0.0f, 1.0f, 0.0f,
             1.0f,  1.0f, 0.0f, 1.0f, 1.0f,
            -1.0f,  1.0f, 0.0f, 0.0f, 1.0f
        };

        Shader shader;
        GPURayTracer gpuRayTracer;

        private void MouseUpdate(Object source, ElapsedEventArgs e)
        {
            Camera.OnMouseMove(this);
        }

        Mesh usedMesh;
        ObjectBVH objectBVH;

        protected override void OnLoad(EventArgs e)
        {
            //timer.Elapsed += MouseUpdate;
            //timer.AutoReset = true;
            //timer.Enabled = true;

            //TargetUpdateFrequency = 60.0;
            Console.WriteLine("update period: " + this.UpdatePeriod);
            Console.WriteLine("update freq: " + this.UpdateFrequency);

            //Mesh teapotMesh = MeshLoader.LoadMesh("teapot.obj");
            //Mesh manMesh = MeshLoader.LoadMesh("man.obj");
            Mesh dragonMesh = MeshLoader.LoadMesh("xyzrgb_dragon.obj");
            //Mesh bunnyMesh = MeshLoader.LoadMesh("bunny.obj");

            usedMesh = dragonMesh;
            objectBVH = new ObjectBVH(usedMesh);

            GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
            VAO = GL.GenVertexArray();
            GL.BindVertexArray(VAO);

            VBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBO);
            GL.BufferData(BufferTarget.ArrayBuffer, screenEdgeVertices.Length * sizeof(float), screenEdgeVertices, BufferUsageHint.StaticDraw);

            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));

            shader = new Shader("shader.vert", "shader.frag");
            shader.Use();

            gpuRayTracer = new GPURayTracer();
            GameScene.LoadDefaultScene(3);
            gpuRayTracer.LoadScene(usedMesh.vertices, usedMesh.indices, objectBVH);

            base.OnLoad(e);
        }

        protected override void OnUnload(EventArgs e)
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.DeleteBuffer(VBO);
            shader.Dispose();
            gpuRayTracer.Dispose();
            base.OnUnload(e);
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            totalGPUTime += e.Time;
            GPUFrameCounter++;
            GPUFrame();

            renderCheckTime -= e.Time;
            if (renderCheckTime < 0.0)
            {
                Console.WriteLine("Render fps: " + RenderFrequency);
                Console.WriteLine("GPU average fps since scene load: " + (GPUFrameCounter / totalGPUTime));
                Console.WriteLine("GPU average ray generation execution time (ms): " + GPURayTracer.totalGenRayTime / GPURayTracer.totalFrames);
                Console.WriteLine("GPU average primary ray firsthit shader execution time (ms): " + GPURayTracer.totalPrimaryRayFirstHitTime / GPURayTracer.totalFrames);
                Console.WriteLine("GPU average shadow ray shader execution time per frame (ms): " + GPURayTracer.firstShadowRayTime / GPURayTracer.totalFrames);
                Console.WriteLine("GPU average TOTAL ray firsthit shader execution time (ms): " + GPURayTracer.totalRayFirstHitTime / GPURayTracer.totalFrames);
                Console.WriteLine("GPU average TOTAL shadow ray shader execution time per frame (ms): " + GPURayTracer.totalShadowRayTime / GPURayTracer.totalFrames);
                Console.WriteLine("GPU average TOTAL bounce shader execution time per frame (ms): " + GPURayTracer.totalBouncerTime / GPURayTracer.totalFrames);
                renderCheckTime = renderCheckInterval;
            }
            
            base.OnRenderFrame(e);
        }

        void GPUFrame()
        {
            gpuRayTracer.GenTex(Width, Height);

            shader.Use();
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.DrawArrays(PrimitiveType.Quads, 0, 4);
            Context.SwapBuffers();
        }

        protected override void OnResize(EventArgs e)
        {
            GL.Viewport(0, 0, Width, Height);
            base.OnResize(e);
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            Camera.OnMouseMove(this);
            //Keyboard Movement

            KeyboardState ks = Keyboard.GetState();

            if (ks.IsKeyDown(Key.Escape))
            {
                Exit();
            }

            void resetCounters()
            {
                GPUFrameCounter = 0;
                totalGPUTime = 0.0;
                GPURayTracer.totalGenRayTime = 0.0;
                GPURayTracer.totalFrames = 0;
                GPURayTracer.totalPrimaryRayFirstHitTime = 0.0;
                GPURayTracer.firstShadowRayTime = 0.0;
                GPURayTracer.totalRayFirstHitTime = 0.0;
                GPURayTracer.totalShadowRayTime = 0.0;
                GPURayTracer.totalBouncerTime = 0.0;

                renderCheckTime = renderCheckInterval;
            }

            if (Focused)
            {
                if (ks.IsKeyDown(Key.Number1))
                {
                    GameScene.LoadDefaultScene(1);
                    gpuRayTracer.LoadScene(usedMesh.vertices, usedMesh.indices, objectBVH);
                    resetCounters();
                }
                if (ks.IsKeyDown(Key.Number2))
                {
                    GameScene.LoadDefaultScene(2);
                    gpuRayTracer.LoadScene(usedMesh.vertices, usedMesh.indices, objectBVH);
                    resetCounters();
                }
                if (ks.IsKeyDown(Key.Number3))
                {
                    GameScene.LoadDefaultScene(3);
                    gpuRayTracer.LoadScene(usedMesh.vertices, usedMesh.indices, objectBVH);
                    resetCounters();
                }
                if (ks.IsKeyDown(Key.Number4))
                {
                    GameScene.LoadDefaultScene(4);
                    gpuRayTracer.LoadScene(usedMesh.vertices, usedMesh.indices, objectBVH);
                    resetCounters();
                }
                if (ks.IsKeyDown(Key.Number5))
                {
                    GameScene.scene5Indices = usedMesh.indices;
                    GameScene.scene5Vertices = usedMesh.vertices;
                    GameScene.LoadDefaultScene(5);
                    gpuRayTracer.LoadScene(usedMesh.vertices, usedMesh.indices, objectBVH);
                    resetCounters();
                }
                if (ks.IsKeyDown(Key.Number6))
                {
                    GameScene.LoadDefaultScene(6);
                    gpuRayTracer.LoadScene(usedMesh.vertices, usedMesh.indices, objectBVH);
                    resetCounters();
                }
            }

            Camera.OnUpdateFrame(e);

            base.OnUpdateFrame(e);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {

            Camera.OnMouseWheel(e);
            base.OnMouseWheel(e);

        }

        public Game(int width, int height, string title) : base(width, height, GraphicsMode.Default, title) { }
    }
}
