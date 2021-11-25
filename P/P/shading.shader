#version 430
layout(local_size_x = 1, local_size_y = 1) in;
layout(rgba32f, binding = 0) uniform image2D img_output;

struct Ray
{
	vec3 origin;
	vec3 direction;
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

layout(std430, binding = 3) buffer shadowRayBuffer
{
	ShadowRay shadowRays[];
};

layout(binding = 4, offset = 8) uniform atomic_uint shadowRayCount;

void main() {
	ShadowRay ray;

	uint lastShadowRayID = atomicCounter(shadowRayCount);

	uint totalRays = atomicCounter(shadowRayCount);
	uint iter = 0;
	while (gl_GlobalInvocationID.x + (iter * 262144) < totalRays) {
		ray = shadowRays[gl_GlobalInvocationID.x + (iter * 262144)];
		vec4 pixel = vec4(ray.energy, 1.0);

		ivec2 pixel_coords = ivec2(ray.pixelX, ray.pixelY);
		vec4 clr = imageLoad(img_output, pixel_coords);
		imageStore(img_output, pixel_coords, clr + pixel);
		iter++;
	}
}