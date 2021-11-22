#version 430
layout(local_size_x = 1, local_size_y = 1) in;
layout(rgba32f, binding = 0) uniform image2D img_output;

layout(std430, binding = 6) buffer testBuffer
{
	vec4 test[];
};

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

void main() {
	vec3 dir = primaryRays[gl_GlobalInvocationID.x + gl_GlobalInvocationID.y * gl_NumWorkGroups.x].direction;
	vec4 pixel = vec4(primaryRays[gl_GlobalInvocationID.x + gl_GlobalInvocationID.y * gl_NumWorkGroups.x].maxt, 0.0, 0.0, 1.0);
	ivec2 pixel_coords = ivec2(gl_GlobalInvocationID.xy);

	imageStore(img_output, pixel_coords, pixel);
}