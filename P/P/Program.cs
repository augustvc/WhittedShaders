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
                game.VSync = VSyncMode.Off;
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
        RayTracer rayTracer;
        GPURayTracer gpuRayTracer;

        System.Timers.Timer timer = new System.Timers.Timer(4);

        private void MouseUpdate(Object source, ElapsedEventArgs e)
        {
            Camera.OnMouseMove(this);
        }

        protected override void OnLoad(EventArgs e)
        {
            //timer.Elapsed += MouseUpdate;
            //timer.AutoReset = true;
            //timer.Enabled = true;

            //TargetUpdateFrequency = 60.0;
            Console.WriteLine("update period: " + this.UpdatePeriod);
            Console.WriteLine("update freq: " + this.UpdateFrequency);

            MeshLoader.Init();

            Mesh loadedObj = new Mesh(MeshLoader.vertices, MeshLoader.indices);
            Mesh tree = MeshGenerator.TreeGenerator.GenerateTree();

            Mesh usedMesh = loadedObj;

            TopLevelBVH topLevelBVH = new TopLevelBVH(usedMesh.vertices, usedMesh.indices.ToArray());
            FourWayBVH topFourWayBVH = new FourWayBVH(topLevelBVH.TopBVH);

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

            rayTracer = new RayTracer(topLevelBVH,topFourWayBVH);
            gpuRayTracer = new GPURayTracer();
            gpuRayTracer.SetupTriangleBuffers(usedMesh.vertices, usedMesh.indices, topLevelBVH);

            base.OnLoad(e);
        }

        protected override void OnUnload(EventArgs e)
        {
            timer.Stop();
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.DeleteBuffer(VBO);
            shader.Dispose();
            gpuRayTracer.Dispose();
            base.OnUnload(e);
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            if (!useGPU)
            {
                CPUFrame();
            }
            else
            {
                totalGPUTime += e.Time;
                GPUFrameCounter++;
                GPUFrame();
            }
            renderCheckTime -= e.Time;
            if (renderCheckTime < 0.0)
            {
                Console.WriteLine("Total primary ray bvh checks: " + RayTracer.totalPrimaryBVHChecks);
                Console.WriteLine("Avg Render time: " + RayTracer.averageFrameTime);
                Console.WriteLine("Render fps: " + RenderFrequency);
                Console.WriteLine("GPU average fps since launch: " + (GPUFrameCounter / totalGPUTime));
                Console.WriteLine("GPU average ray generation execution time (ms): " + GPURayTracer.totalGenRayTime / GPURayTracer.totalFrames);
                Console.WriteLine("GPU average primary ray firsthit shader execution time (ms): " + GPURayTracer.totalPrimaryRayFirstHitTime / GPURayTracer.totalFrames);
                Console.WriteLine("GPU average shadow ray shader execution time per frame (ms): " + GPURayTracer.totalShadowRayTime / GPURayTracer.totalFrames);
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

        void CPUFrame()
        {
            GL.Clear(ClearBufferMask.ColorBufferBit);

            float[] pixels = rayTracer.GenTexture(Width, Height);

            int thandle = GL.GenTexture();
            shader.Use();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, thandle);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            float[] borderColor = { 1.0f, 1.0f, 0.0f, 1.0f };
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, borderColor);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, Width, Height, 0, PixelFormat.Rgba, PixelType.Float, pixels);
            GL.GenerateTextureMipmap(thandle);

            GL.DrawArrays(PrimitiveType.Quads, 0, 4);

            Context.SwapBuffers();

            GL.DeleteTexture(thandle);
        }

        protected override void OnResize(EventArgs e)
        {
            GL.Viewport(0, 0, Width, Height);
            base.OnResize(e);
        }

        bool newYPress = true;
        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            Camera.OnMouseMove(this);
            //Keyboard Movement

            KeyboardState ks = Keyboard.GetState();

            if (ks.IsKeyDown(Key.Escape))
            {
                Exit();
            }

            if (ks.IsKeyDown(Key.Y))
            {
                if (newYPress)
                {
                    useGPU = !useGPU;
                    newYPress = false;
                    if (useGPU)
                    {
                        Title = "GPU Ray Tracer";
                    }
                    else
                    {
                        Title = "CPU Ray Tracer";
                    }
                }
            }
            else
            {
                newYPress = true;
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
