#version 460
layout(local_size_x = 1, local_size_y = 1) in;

struct Ray
{
	vec3 origin;
	vec3 direction;
	float maxt;
	uint pixelX;
	uint pixelY;
};

layout(std430, binding = 1) buffer rayBuffer
{
	Ray rays[];
};

layout(binding = 2, offset = 0) uniform atomic_uint rayCount;
layout(binding = 2, offset = 4) uniform atomic_uint test;

vec3 cameraOrigin = vec3(0.0, 0.0, 0.0);
vec3 p1 = vec3(-1.0, -1.0, 2.0);
vec3 p2 = vec3(1.0, -1.0, 2.0);
vec3 p3 = vec3(-1.0, 1.0, 2.0);
vec3 xArm = p2 - p1;
vec3 yArm = p3 - p1;

void main() {
	yArm *= float(gl_NumWorkGroups.y) / float(gl_NumWorkGroups.x);
	//rays[atomicCounterIncrement(rayCount)] = Ray(vec3(0.0, 0.0, 0.0),
	rays[gl_GlobalInvocationID.x + gl_GlobalInvocationID.y * gl_NumWorkGroups.x] = Ray(vec3(0.0, 0.0, 0.0),
		normalize(
		p1 +
		xArm * (float(gl_GlobalInvocationID.x) / float(gl_NumWorkGroups.x)) +
		yArm * (float(gl_GlobalInvocationID.y) / float(gl_NumWorkGroups.y))
		),
		10000.0,
		gl_GlobalInvocationID.x,
		gl_GlobalInvocationID.y);
	//atomicCounterExchange(rayCount, gl_NumWorkGroups.x * gl_NumWorkGroups.y);
}