struct Ray
{
	vec3 origin;
	vec3 dir;
	vec3 energy;
	float t;
	uint pixelX;
	uint pixelY;
	vec3 ambient;
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

int intersect(inout Ray ray) {
	int primID = -1;
	for (int i = 0; i < spheres.length(); i++) {
		Sphere sphere = spheres[i];

		vec3 C = sphere.position - ray.origin;
		float t = dot(C, ray.dir);
		vec3 Q = C - (t * ray.dir);
		float dsq = dot(Q, Q);

		if (dsq > sphere.r2)
			continue;
		t -= sqrt(sphere.r2 - dsq);

		if (t < 0) continue;
		if (t > ray.t) continue;
		ray.t = t;
		primID = i;
	}

	for (int i = 0; i < planes.length(); i++) {
		Plane plane = planes[i];

		float denom = dot(-plane.normal, ray.dir);
		if (denom > 0.0001) {
			vec3 planeOrigin = plane.normal * plane.offset;
			vec3 diff = planeOrigin - ray.origin;
			float t = dot(diff, -plane.normal) / denom;
			if (t < 0) {
				continue;
			}
			if (t > ray.t) {
				continue;
			}
			ray.t = t;
			primID = 10000 + i;
		}
	}

	int i = 3;
	while(i <= indexBuffer[0]) {
		uint triAI = indexBuffer[i++];
		uint triBI = indexBuffer[i++];
		uint triCI = indexBuffer[i++];

		vec3 triA = vec3(vertexBuffer[triAI * 8], vertexBuffer[triAI * 8 + 1], vertexBuffer[triAI * 8 + 2]);
		vec3 triB = vec3(vertexBuffer[triBI * 8], vertexBuffer[triBI * 8 + 1], vertexBuffer[triBI * 8 + 2]);
		vec3 triC = vec3(vertexBuffer[triCI * 8], vertexBuffer[triCI * 8 + 1], vertexBuffer[triCI * 8 + 2]);
		vec3 ab = triB - triA;
		vec3 ac = triC - triA;

		vec3 cross1 = cross(ray.dir, ac);
		float det = dot(ab, cross1);
		if (abs(det) < 0.0001)
			continue;

		float detInv = 1.0 / det;
		vec3 diff = ray.origin - triA;
		float u = dot(diff, cross1) * detInv;
		if (u < 0 || u > 1) {
			continue;
		}

		vec3 cross2 = cross(diff, ab);
		float v = dot(ray.dir, cross2) * detInv;
		if (v < 0 || v > 1)
			continue;

		if (u + v > 1)
			continue;
		float t = dot(ac, cross2) * detInv;
		if (t <= 0)
			continue;

		if (t < ray.t) {
			ray.t = t;
			primID = 30000 + i - 3;
		}
	}

	for (int i = 0; i < triangles.length(); i++) {
		Triangle tri = triangles[i];

		vec3 ab = tri.b - tri.a;
		vec3 ac = tri.c - tri.a;
		vec3 cross1 = cross(ray.dir, ac);
		float det = dot(ab, cross1);
		if (abs(det) < 0.0001)
			continue;

		float detInv = 1.0 / det;
		vec3 diff = ray.origin - tri.a;
		float u = dot(diff, cross1) * detInv;
		if (u < 0 || u > 1) {
			continue;
		}

		vec3 cross2 = cross(diff, ab);
		float v = dot(ray.dir, cross2) * detInv;
		if (v < 0 || v > 1)
			continue;

		if (u + v > 1)
			continue;
		float t = dot(ac, cross2) * detInv;
		if (t <= 0)
			continue;

		if (t < ray.t) {
			primID = 20000 + i;
			ray.t = t;
		}
	}

	return primID;
}