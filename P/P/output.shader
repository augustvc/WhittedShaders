#version 430
layout(local_size_x = 1, local_size_y = 1) in;
layout(rgba32f, binding = 0) uniform image2D img_output;

struct Ray
{
	vec3 origin;
	vec3 direction;
	float t;
};

layout(std430, binding = 5) buffer rayBuffer
{
	uint rayCount;
	Ray rays[];
};

void main() {
	Ray ray = rays[gl_GlobalInvocationID.x + gl_GlobalInvocationID.y * gl_NumWorkGroups.x];
	vec4 pixel = vec4(0.0, 1.0 / ray.t, 0.0, 1.0);
	ivec2 pixel_coords = ivec2(gl_GlobalInvocationID.xy);

	imageStore(img_output, pixel_coords, pixel);
}