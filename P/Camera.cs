using System;
using System.Numerics;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics;
using OpenTK.Input;
using OpenTK.Graphics.OpenGL;
using OpenTK;

public class Camera
{
	public Camera()
	{
		int a

		Vector3 cameraPosition = new Vector3(0.0f, 0.0f, 0.0f);
		Vector3 cameraTarget = new Vector3(0.0f, 0.0f, 0.0f);
		Vector3 cameraDirection = Vector3.Normalize(cameraPosition - cameraTarget);
		Vector3 up = Vector3.UnitY;
		Vector3 cameraRight = Vectore3.Normalize(Vector3.Cross(up, cameraDirection));
		Vector3 cameraUp = Vector3.Cross(cameraDirection, cameraRight);
		Matrix4 view = Matrix4.Lookat(new Vector3(0.0f, 0.0f, 0.0f),
			new Vector3(0.0f, 0.0f, 0.0f),
			new Vector3(0.0f, 0.0f, 0.0f));
		float speed = 1.5f;

		Vector3 position = new Vector3(0.0f, 0.0f, 3.0f);
		Vector3 front = new Vector3(0.0f, 0.0f, -1.0f);
		Vector3 up = new Vector3(0.0f, 1.0f, 0.0f);

		view = Matrix4.LookAt(position, position + front, up);
	}

	bool Focused = true;
	protected override void OnUpdateFrame(FrameEventArgs e)
	{
		if (!Focused)
		{
			return;
		}

		KeyboardState input = Keyboard.GetState();
		if (input.IsKeyDown(Key.W))
		{
			position += front * speed; //Forward 
		}

		if (input.IsKeyDown(Key.S))
		{
			position -= front * speed; //Backwards
		}

		if (input.IsKeyDown(Key.A))
		{
			position -= Vector3.Normalize(Vector3.Cross(front, up)) * speed; //Left
		}

		if (input.IsKeyDown(Key.D))
		{
			position += Vector3.Normalize(Vector3.Cross(front, up)) * speed; //Right
		}

		if (input.IsKeyDown(Key.Space))
		{
			position += up * speed; //Up 
		}

		if (input.IsKeyDown(Key.LShift))
		{
			position -= up * speed; //Down
		}
	}
}
