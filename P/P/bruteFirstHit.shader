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

layout(std430, binding = 1) buffer rayBuffer
{
	Ray rays[];
};

layout(binding = 2) uniform atomic_uint rayCount;

layout(std430, binding = 3) buffer shadowRayBuffer
{
	ShadowRay shadowRays[];
};

layout(binding = 4) uniform atomic_uint shadowRayCount;

struct Sphere
{
	vec3 position;
	vec3 color;
	float r2;
};

struct Light
{
	vec3 position;
	vec3 brightness;
};

Sphere spheres[] = Sphere[](
	  Sphere(vec3(-3.0, -1.0, 8.0), vec3(1.0, 0.0, 0.0), 4.0)
	, Sphere(vec3(3.0, -1.0, 8.0), vec3(1.0, 0.0, 0.0), 4.0)
);

Light lights[] = Light[](
	  Light(vec3(0.0, 8.0, 0.0), vec3(20.0, 20.0, 20.0))
	, Light(vec3(0.0, -8.0, 0.0), vec3(2.0, 0.0, 16.0))
);

vec3 lightPosition = vec3(0.0, 8.0, 0.0);
vec3 lightValue = vec3(20.0, 20.0, 20.0);

void main() {
	uint rayNum = atomicCounterIncrement(rayCount);
	shadowRays[rayNum] =
		ShadowRay(vec3(0.0, 0.0, 0.0), vec3(0.0, 1.0, 0.0), vec3(1.0, 1.0, 1.0), 1.0, rays[rayNum].pixelX, rays[rayNum].pixelY);
	//int iter = 0;
	
	//uint totalRays = atomicCounter(rayCount);
	//while (gl_GlobalInvocationID.x + iter * 524288 < totalRays) {

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
	if (primID < 0) {
		//return;
	}

	vec3 srOrigin = rays[rayNum].origin + rays[rayNum].dir * rays[rayNum].t + 0.0001 * normal;
	float ndotl = max(dot(normalize(lightPosition - srOrigin), normal), 0);
	float inverseDistSq = dot(lightPosition - srOrigin, lightPosition - srOrigin);
	vec3 finalLight = ndotl * (lightValue / inverseDistSq);
	for (int i = 0; i < 3; i++) {
		finalLight[i] += 0.05;
	}

	ShadowRay shadowRay = ShadowRay(srOrigin, normalize(lightPosition - srOrigin), vec3(spheres[primID].color * finalLight), length(lightPosition - srOrigin), rays[rayNum].pixelX, rays[rayNum].pixelY);
	shadowRays[rayNum] = shadowRay;

	//iter++;
	//}
}