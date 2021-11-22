#version 430
layout(local_size_x = 1, local_size_y = 1) in;

struct Ray
{
	vec3 origin;
	vec3 direction;
	float maxt;
};

layout(std430, binding = 5) buffer primaryRayBuffer
{
	Ray primaryRays[];
};

layout(std430, binding = 6) buffer testBuffer
{
	vec4 test[];
};

void main() {
	Ray ray = Ray(vec3(0.0, 0.0, 0.0), vec3(1.0, 1.0, 0.0), 10000.0);
	primaryRays[gl_GlobalInvocationID.x + gl_GlobalInvocationID.y * gl_NumWorkGroups.x] = ray;
	//test[gl_GlobalInvocationID.x + gl_GlobalInvocationID.y * gl_NumWorkGroups.x] = vec4(0.0, 1.0, 0.0, 1.0);
}