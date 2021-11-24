using System;
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


    static Vector3 front = new Vector3(0.0f, 0.0f, -1.0f);
    static Vector3 up = new Vector3(0.0f, 1.0f, 0.0f);
    static float speed = 1.5f;
    static bool Focused = true;
    static bool firstMove = true;
    static Vector2 lastPos = new Vector2(0.0f, 0.0f);
    static float sensitivity = 0.0f;
    static float pitch = 0.0f;
    static float yaw = 0.0f;
    static MouseState ms = Mouse.GetState();
    private static float _fov;
    static Vector3 cameraPosition = new Vector3(0.0f, 0.0f, -1.0f);
    static Vector3 cameraTarget = new Vector3(0.0f, 0.0f, 1.0f);
    static float cameraSpeed = 1.0f;
    static Camera()

    {


        Vector3 cameraDirection = Vector3.Normalize(cameraPosition - cameraTarget);
        Vector3 up = Vector3.UnitY;
        Vector3 cameraRight = Vector3.Normalize(Vector3.Cross(up, cameraDirection));
        Vector3 cameraUp = Vector3.Cross(cameraDirection, cameraRight);
        Matrix4 view = Matrix4.LookAt(new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 0.0f, 0.0f));




        view = Matrix4.LookAt(cameraPosition, cameraPosition + front, up);
    }

    static public Vector3 getCameraPosition()
    {
        return cameraPosition;
    }
    static public Vector3 getCameraTarget()
    {
        return cameraTarget;
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
            Console.WriteLine("w pressed");
            cameraPosition += front * cameraSpeed; //Forward 
        }

        if (input.IsKeyDown(Key.A))
        {
            Console.WriteLine("s pressed");
            cameraPosition -= front * cameraSpeed; //Backwards
        }

        if (input.IsKeyDown(Key.S))
        {
            Console.WriteLine("a pressed");
            cameraPosition -= Vector3.Normalize(Vector3.Cross(front, up)) * cameraSpeed; //Left
        }

        if (input.IsKeyDown(Key.W))
        {
            Console.WriteLine("d pressed");
            cameraPosition += Vector3.Normalize(Vector3.Cross(front, up)) * cameraSpeed; //Right
        }

        if (input.IsKeyDown(Key.Space))
        {
            Console.WriteLine("space pressed");
            cameraPosition += up * cameraSpeed; //Up 
        }

        if (input.IsKeyDown(Key.LShift))
        {
            Console.WriteLine("L.Shift pressed");
            cameraPosition -= up * cameraSpeed; //Down
        }





        if (firstMove)
        {

            lastPos = new Vector2(ms.X, ms.Y);
            firstMove = false;
        }
        else
        {
            float deltaX = ms.X - lastPos.X;
            float deltaY = ms.Y - lastPos.Y;
            lastPos = new Vector2(ms.X, ms.Y);

            yaw += deltaX * sensitivity;
            if (pitch > 89.0f)
            {
                pitch = 89.0f;
            }
            else if (pitch < -89.0f)
            {
                pitch = -89.0f;
            }
            else
            {
                pitch -= deltaX * sensitivity;
            }
        }

        front.X = (float)Math.Cos(MathHelper.DegreesToRadians(pitch)) * (float)Math.Cos(MathHelper.DegreesToRadians(yaw));
        front.Y = (float)Math.Sin(MathHelper.DegreesToRadians(pitch));
        front.Z = (float)Math.Cos(MathHelper.DegreesToRadians(pitch)) * (float)Math.Sin(MathHelper.DegreesToRadians(yaw));
        front = Vector3.Normalize(front);




    }

    public static void OnMouseMove(MouseMoveEventArgs e, GameWindow program)
    {
        Console.WriteLine("mouse movement being tracked");
        if (Focused) // check to see if the window is focused  
        {
            Mouse.SetPosition(ms.X + program.Width / 2f, ms.Y + program.Height / 2f);
        }


    }
    public static void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (ms.ScrollWheelValue >= 45.0f)
        {
            _fov = 45.0f;
        }
        else if (ms.ScrollWheelValue <= 1.0f)
        {
            _fov = 1.0f;
        }
        else
        {
            _fov = e.DeltaPrecise;
        }
        Console.WriteLine("the fov has changed");

    }


}
