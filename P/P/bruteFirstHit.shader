#version 430
layout(local_size_x = 1, local_size_y = 1) in;

struct Ray
{
	vec3 origin;
	vec3 dir;
	float t;
	uint pixelX;
	uint pixelY;
};

struct ShadowRay
{
	vec3 origin;
	vec3 dir;
	vec3 energy;
	float t;
	uint pixelX;
	uint pixelY;
};

layout(std430, binding = 1) buffer rayInBuffer
{
	Ray rays[];
};

layout(std430, binding = 2) buffer rayOutBuffer
{
	Ray raysOut[];
};

layout(binding = 4) uniform atomic_uint rayCount[];
layout(binding = 4, offset = 8) uniform atomic_uint shadowRayCount;


layout(std430, binding = 3) buffer shadowRayBuffer
{
	ShadowRay shadowRays[];
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

Sphere spheres[] = Sphere[](
	  Sphere(vec3(-3.0, -1.0, 12.0), Material(vec3(1.0, 0.0, 0.0), 1.0, 0.0), 4.0)
	, Sphere(vec3(3.0, -1.0, 12.0), Material(vec3(1.0, 0.0, 0.0), 1.0, 0.0), 4.0)
);

struct Plane
{
	vec3 normal;
	float offset;
	Material mat;
};

Plane planes[] = Plane[](
	Plane(vec3(0.0, 1.0, 0.0), -3.0, Material(vec3(0.0, 1.0, 0.0), 1.0, 0.0))
);

Light lights[] = Light[](
	  Light(vec3(0.0, 8.0, 0.0), vec3(20.0, 20.0, 20.0))
	, Light(vec3(0.0, -8.0, 0.0), vec3(2.0, 0.0, 16.0))
);

vec3 lightPosition = vec3(0.0, 8.0, 0.0);
vec3 lightValue = vec3(20.0, 20.0, 20.0);

uint counterid = 0;

void main() {
	uint iter = 0;
	uint totalRays = atomicCounter(rayCount[0]);
	while (gl_GlobalInvocationID.x + (iter * 262144) < totalRays) {
		uint rayNum = gl_GlobalInvocationID.x + iter * 262144;

		raysOut[atomicCounterIncrement(rayCount[1 - 0])] = rays[rayNum];

		int primID = -1;
		vec3 normal = vec3(0.0);
		for (int i = 0; i < spheres.length(); i++) {
			Sphere sphere = spheres[i];

			vec3 C = sphere.position - rays[rayNum].origin;
			float t = dot(C, rays[rayNum].dir);
			vec3 Q = C - (t * rays[rayNum].dir);
			float dsq = dot(Q, Q);

			if (dsq > sphere.r2)
				continue;
			t -= sqrt(sphere.r2 - dsq);

			if (t < 0) continue;
			if (t > rays[rayNum].t) continue;
			rays[rayNum].t = t;
			normal = normalize((rays[rayNum].origin + rays[rayNum].dir * t) - sphere.position);
			primID = i;
		}

		for (int i = 0; i < planes.length(); i++) {
			Plane plane = planes[i];

			float denom = dot(-plane.normal, rays[rayNum].dir);
			if (denom > 0.0001) {
				vec3 planeOrigin = plane.normal * plane.offset;
				vec3 diff = planeOrigin - rays[rayNum].origin;
				float t = dot(diff, -plane.normal) / denom;
				if (t < 0) {
					continue;
				}
				if (t > rays[rayNum].t) {
					continue;
				}
				rays[rayNum].t = t;
				primID = 10000 + i;
				normal = plane.normal;
			}
		}

		if (primID < 0) {
			///return;
		}

		vec3 srOrigin = rays[rayNum].origin + rays[rayNum].dir * rays[rayNum].t + 0.0001 * normal;
		float ndotl = max(dot(normalize(lightPosition - srOrigin), normal), 0);
		float distSq = dot(lightPosition - srOrigin, lightPosition - srOrigin);
		vec3 finalLight = ndotl * (lightValue / distSq);
		for (int i = 0; i < 3; i++) {
			finalLight[i] += 0.05;
		}

		Material mat = Material(vec3(0.0), 1.0, 0.0);
		if (primID >= 10000) {
			mat = planes[primID - 10000].mat;
		}
		else {
			mat = spheres[primID].mat;
		}

		vec3 diffuseEnergy = mat.diffuse * mat.color * finalLight;

		ShadowRay shadowRay = ShadowRay(srOrigin, normalize(lightPosition - srOrigin), diffuseEnergy, length(lightPosition - srOrigin), rays[rayNum].pixelX, rays[rayNum].pixelY);
		shadowRays[atomicCounterIncrement(shadowRayCount)] = shadowRay;

		iter++;
	}
}