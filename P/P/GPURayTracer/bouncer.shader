layout(local_size_x = 64, local_size_y = 1) in;

struct Ray
{
	vec3 origin;
	vec3 dir;
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

layout(std430, binding = 2) buffer rayOutBuffer
{
	Ray raysOut[];
};

layout(std430, binding = 3) buffer shadowRayBuffer
{
	Ray shadowRays[];
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

vec3 lightPosition = vec3(0.0, 120.0, 0.0);
vec3 lightValue = vec3(6000.0, 6000.0, 6000.0);

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

layout(binding = 4) uniform atomic_uint rayCountIn;
layout(binding = 4, offset = 4) uniform atomic_uint rayCountOut;
layout(binding = 4, offset = 8) uniform atomic_uint shadowRayCount;
layout(binding = 4, offset = 12) uniform atomic_uint intersectionJob;

void main() {
	uint rayNum;
	uint totalRays = atomicCounter(rayCountIn);
	uint iter = 0;
	while (gl_GlobalInvocationID.x + (iter * 262144) < totalRays) {
		rayNum = gl_GlobalInvocationID.x + (iter * 262144);
		iter++;

		int primID = rays[rayNum].primID;

		if (primID < 0) {
			continue;
		}

		vec3 normal = vec3(0.0);
		Material mat = Material(vec3(0.0), 1.0, 0.0);
		if (primID >= 30000) {
			//Triangle from a mesh
			primID -= 30000;
			mat = Material(vec3(1.0), 1.0, 0.0);
			uint triAI = indexBuffer[primID++];
			uint triBI = indexBuffer[primID++];
			uint triCI = indexBuffer[primID++];
			vec3 triA = vec3(vertexBuffer[triAI * 8], vertexBuffer[triAI * 8 + 1], vertexBuffer[triAI * 8 + 2]);
			vec3 triB = vec3(vertexBuffer[triBI * 8], vertexBuffer[triBI * 8 + 1], vertexBuffer[triBI * 8 + 2]);
			vec3 triC = vec3(vertexBuffer[triCI * 8], vertexBuffer[triCI * 8 + 1], vertexBuffer[triCI * 8 + 2]);
			normal = normalize(cross(triC - triA, triB - triA));
			if (dot(rays[rayNum].dir, normal) > 0)
				normal = -normal;
		}
		else if (primID >= 20000) {
			mat = triangles[primID - 20000].mat;
			Triangle tri = triangles[primID - 20000];
			normal = normalize(cross(tri.b - tri.a, tri.c - tri.a));
			if (dot(rays[rayNum].dir, normal) > 0)
				normal = -normal;
		}
		else if (primID >= 10000) {
			mat = planes[primID - 10000].mat;
			normal = planes[primID - 10000].normal;
		}
		else {
			mat = spheres[primID].mat;
			normal = normalize((rays[rayNum].origin + rays[rayNum].dir * rays[rayNum].t) - spheres[primID].position);
		}

		if (mat.specular > 0.0) {
			float ndotr = -dot(normal, rays[rayNum].dir);
			raysOut[atomicCounterIncrement(rayCountOut)] = Ray(
				rays[rayNum].origin + rays[rayNum].dir * rays[rayNum].t + normal * 0.0001,
				rays[rayNum].dir + ndotr * 2 * normal,
				rays[rayNum].energy * mat.specular,
				100000, rays[rayNum].pixelX, rays[rayNum].pixelY,
				vec3(0.0),
				-1);
		}

		if (mat.diffuse > 0.0) {
			vec3 srOrigin = rays[rayNum].origin + rays[rayNum].dir * rays[rayNum].t + 0.001 * normal;
			float ndotl = max(dot(normalize(lightPosition - srOrigin), normal), 0);
			float distSq = dot(lightPosition - srOrigin, lightPosition - srOrigin);
			vec3 finalLight = ndotl * (lightValue / distSq);
			for (int i = 0; i < 3; i++) {
				finalLight[i] += 0.05;
			}

			vec3 diffuseEnergy = mat.diffuse * mat.color * finalLight;

			Ray shadowRay = Ray(
				srOrigin,
				normalize(lightPosition - srOrigin),
				diffuseEnergy * rays[rayNum].energy,
				length(lightPosition - srOrigin), rays[rayNum].pixelX, rays[rayNum].pixelY,
				mat.color * mat.diffuse * 0.05,
				-1);
			shadowRays[atomicCounterIncrement(shadowRayCount)] = shadowRay;
		}
	}
}