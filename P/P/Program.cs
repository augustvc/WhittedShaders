﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics;
using OpenTK.Input;
using OpenTK.Graphics.OpenGL;
using OpenTK;

namespace P
{
    class Game : GameWindow
    {
        double renderCheckTime = 2.0;
        double renderCheckInterval = 2.0;

        bool useGPU = true;
        static void Main(string[] args)
        {
            using (Game game = new Game(1280, 800, "GPU Ray Tracer"))
            {
                game.Run(0.0);
            }
        }

        int VBO, VAO;

        float[] vertices = {
            -1.0f, -1.0f, 0.0f, 0.0f, 0.0f,
             1.0f, -1.0f, 0.0f, 1.0f, 0.0f,
             1.0f,  1.0f, 0.0f, 1.0f, 1.0f,
            -1.0f,  1.0f, 0.0f, 0.0f, 1.0f
        };

        Shader shader;
        RayTracer rayTracer;
        GPURayTracer gpuRayTracer;

        protected override void OnLoad(EventArgs e)
        {
            GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
            VAO = GL.GenVertexArray();
            GL.BindVertexArray(VAO);

            VBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBO);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));

            shader = new Shader("../../shader.vert", "../../shader.frag");
            shader.Use();

            rayTracer = new RayTracer();
            gpuRayTracer = new GPURayTracer();

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
            if (useGPU)
            {
                GPUFrame();
            }
            else
            {
                CPUFrame();
            }
            renderCheckTime -= e.Time;
            if(renderCheckTime < 0.0)
            {
                Console.WriteLine("Render time: " + RenderTime);
                Console.WriteLine("Render fps: " + RenderFrequency);
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
            KeyboardState ks = Keyboard.GetState();
            
            if(ks.IsKeyDown(Key.Escape))
            {
                Exit();
            }

            if(ks.IsKeyDown(Key.Y)) {
                if (newYPress)
                {
                    useGPU = !useGPU;
                    newYPress = false;
                    if (useGPU)
                    {
                        Title = "GPU Ray Tracer";
                    } else
                    {
                        Title = "CPU Ray Tracer";
                    }
                }
            } else
            {
                newYPress = true;
            }

            if(ks.IsKeyDown(Key.W))
            {
                gpuRayTracer.updateCamera(new Vector3(0.0f, 0.0f, 3.0f) * (float)e.Time);
            }
            if (ks.IsKeyDown(Key.A))
            {
                gpuRayTracer.updateCamera(new Vector3(-3f, 0.0f, 0.0f) * (float)e.Time);
            }
            if (ks.IsKeyDown(Key.S))
            {
                gpuRayTracer.updateCamera(new Vector3(0.0f, 0.0f, -3f) * (float)e.Time);
            }
            if (ks.IsKeyDown(Key.D))
            {
                gpuRayTracer.updateCamera(new Vector3(3f, 0.0f, 0.0f) * (float)e.Time);
            }

            base.OnUpdateFrame(e);
        }

        public Game(int width, int height, string title) : base(width, height, GraphicsMode.Default, title) { }
    }
}
