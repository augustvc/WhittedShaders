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

layout(binding = 4) uniform atomic_uint shadowRayCount;

void main() {
	//int iter = 0;
	ShadowRay ray;
	//uint totalRays = atomicCounter(shadowRayCount);
	//while (gl_GlobalInvocationID.x + iter * 524288 < totalRays) {
	ray = shadowRays[gl_GlobalInvocationID.x + gl_GlobalInvocationID.y * gl_NumWorkGroups.x];
	vec4 pixel = vec4(ray.energy, 1.0);

	ivec2 pixel_coords = ivec2(gl_GlobalInvocationID.x, gl_GlobalInvocationID.y);
	imageStore(img_output, pixel_coords, pixel);
	//iter++;
	//}
}