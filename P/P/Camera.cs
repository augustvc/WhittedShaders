﻿using System;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics;
using OpenTK.Input;
using OpenTK.Graphics.OpenGL;
using OpenTK;

public static class Camera
{
    static Vector3 cameraFront = new Vector3(0.00001f, 0.00001f, 1.00001f);


    static bool Focused = true;

    static float sensitivity = 0.1f;
    static float pitch = 0.0f;
    static float yaw = 90.0f;

    public static float _screenDist = 2f;
    static Vector3 cameraPosition = new Vector3(0.0f, 3.0f, -12.0f);

    static float cameraSpeed = 4.0f;
    static Vector3 cameraRight = new Vector3(1.0f, 0.0f, 0.0f);
    static Vector3 cameraUp = new Vector3(0.0f, 1.0f, 0.0f);

    static public Vector3 getCameraPosition()
    {
        return cameraPosition;
    }

    static public Vector3 getCameraFront()
    {
        return cameraFront;
    }
    static public Vector3 getCameraUp()
    {
        return cameraUp;
    }
    static public Vector3 getCameraRight()
    {
        return cameraRight;
    }

    static public float getCameraFOV()
    {
        return Camera._screenDist;
    }


    static Camera()
    {

    }

    static public void OnUpdateFrame(FrameEventArgs e)
    {
        if (!Focused)
        {
            return;
        }

        KeyboardState input = Keyboard.GetState();
        if (input.IsKeyDown(Key.D))
        {
            cameraPosition += cameraRight * cameraSpeed * (float)e.Time; //Forward 
        }

        if (input.IsKeyDown(Key.A))
        {

            cameraPosition -= cameraRight * cameraSpeed * (float)e.Time; //Backwards
        }

        if (input.IsKeyDown(Key.S))
        {
            cameraPosition -= cameraFront * cameraSpeed * (float)e.Time; //Left
        }

        if (input.IsKeyDown(Key.W))
        {
            cameraPosition += cameraFront * cameraSpeed * (float)e.Time; //Right
        }

        if (input.IsKeyDown(Key.Space))
        {
            cameraPosition += cameraUp * cameraSpeed * (float)e.Time; //Up 
        }

        if (input.IsKeyDown(Key.LShift))
        {
            cameraPosition -= cameraUp * cameraSpeed * (float)e.Time; //Down
        }
    }

    static float prevX;
    static float prevY;


    public static void OnMouseMove(GameWindow program)
    {
        MouseState ms = Mouse.GetState();

        Focused = program.Focused;
        if (Focused) // check to see if the window is focused  
        {
            if (ms.IsButtonDown(MouseButton.Left))
            {
                float deltaX = ms.X - prevX;
                float deltaY = ms.Y - prevY;

                pitch += deltaY * sensitivity;
                yaw += deltaX * sensitivity;

                if (pitch > 89.0f)
                {
                    pitch = 89.0f;
                }
                else if (pitch < -89.0f)
                {
                    pitch = -89.0f;
                }

                cameraFront.X = (float)Math.Cos(MathHelper.DegreesToRadians(pitch)) * (float)Math.Cos(MathHelper.DegreesToRadians(yaw));
                cameraFront.Y = (float)Math.Sin(MathHelper.DegreesToRadians(pitch));
                cameraFront.Z = (float)Math.Cos(MathHelper.DegreesToRadians(pitch)) * (float)Math.Sin(MathHelper.DegreesToRadians(yaw));
                cameraFront = Vector3.Normalize(cameraFront);

                cameraRight.X = (float)Math.Cos(MathHelper.DegreesToRadians(pitch)) * (float)Math.Cos(MathHelper.DegreesToRadians(yaw - 90));
                cameraRight.Y = 0;
                cameraRight.Z = (float)Math.Cos(MathHelper.DegreesToRadians(pitch)) * (float)Math.Sin(MathHelper.DegreesToRadians(yaw - 90));
                cameraRight = Vector3.Normalize(cameraRight);

                cameraUp.X = (float)Math.Cos(MathHelper.DegreesToRadians(pitch + 90)) * (float)Math.Cos(MathHelper.DegreesToRadians(yaw));
                cameraUp.Y = (float)Math.Sin(MathHelper.DegreesToRadians(pitch + 90));
                cameraUp.Z = (float)Math.Cos(MathHelper.DegreesToRadians(pitch + 90)) * (float)Math.Sin(MathHelper.DegreesToRadians(yaw));
                cameraUp = Vector3.Normalize(cameraUp);
            }
        }

        prevX = ms.X;
        prevY = ms.Y;
    }
    public static void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (e.DeltaPrecise >= 0)
        {
            _screenDist *= 1.1f;

        }
        else
        {
            _screenDist /= 1.1f;
        }
        if (_screenDist >= 10.0f)
        {

            _screenDist = 10.0f;
        }
        else if (_screenDist <= 0.01f)
        {
            _screenDist = 0.01f;
        }

    }
}
