struct Ray
{
	vec3 origin;
	vec3 dir;
	vec3 invdir;
	vec3 energy;
	float t;
	uint pixelX;
	uint pixelY;
	vec3 ambient;
	int primID;
};

layout(std430, binding = 1) buffer rayInBuffer
{
	Ray rays[];
};

layout(std430, binding = 5) buffer vertexBufferObj
{
	float vertexBuffer[];
};

layout(std430, binding = 6) buffer indexBufferObj
{
	uint indexBuffer[];
};


struct Material
{
	vec3 color;
	float diffuse;
	float specular;
};

struct Sphere
{
	vec3 position;
	Material mat;
	float r2;
};

struct Light
{
	vec3 position;
	vec3 brightness;
};

struct Triangle
{
	vec3 a, b, c;
	Material mat;
};

Triangle triangles[] = Triangle[](
	//  Triangle(vec3(0.0, 4.0, -3.0),vec3(-5.0, 4.0, 3.0), vec3(10.0, 4.0, 0.0), Material(vec3(0.0, 1.0, 0.0), 1.0, 0.0))
	 Triangle(vec3(10.0, 0.0, 5.0), vec3(10.0, 4.0, 5.0), vec3(10.0, 0.0, 10.0), Material(vec3(0.0, 1.0, 0.0), 0.01, 0.99))
);

Sphere spheres[] = Sphere[](
	  Sphere(vec3(-3.0, -1.5, 12.0), Material(vec3(1.0, 0.0, 0.0), 1.0, 0.0), 1.5 * 1.5)
	, Sphere(vec3(3.0, -1.5, 12.0), Material(vec3(1.0, 0.0, 1.0), 1.0, 0.0), 1.5 * 1.5)
	, Sphere(vec3(0.0, -1.5, 9.0), Material(vec3(1.0, 1.0, 0.0), 1.0, 0.0), 1.5 * 1.5)
	, Sphere(vec3(0.0, 4, 24.0), Material(vec3(0.0, 0.0, 0.0), 0.0, 1.0), 18)
	, Sphere(vec3(0.0, 6.5, 16.0), Material(vec3(0.0, 0.0, 0.0), 0.0, 1.0), 2)
	, Sphere(vec3(0.0, 12.5, 16.0), Material(vec3(0.0, 0.0, 0.0), 0.0, 1.0), 2)
	, Sphere(vec3(-5000.0, 0, 0), Material(vec3(0.0, 0.0, 0.0), 0.04, 0.96), 4994 * 4994)
);

struct Plane
{
	vec3 normal;
	float offset;
	Material mat;
};

Plane planes[] = Plane[](
	Plane(vec3(0.0, 1.0, 0.0), -3.0, Material(vec3(1.0, 1.0, 1.0), 1.0, 0.0))
);