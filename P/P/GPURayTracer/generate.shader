layout(local_size_x = 1, local_size_y = 1) in;

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

layout(binding = 4) uniform atomic_uint rayCountIn;

uniform vec3 cameraOrigin = vec3(0.0, 0.0, 0.0);
uniform vec3 p1 = vec3(-1.0, -1.0, 2.0);
uniform vec3 xArm = vec3(2.0, 0.0, 0.0);
uniform vec3 yArm = vec3(0.0, 2.0, 0.0);

void main() {
	rays[atomicCounterIncrement(rayCountIn)] = Ray(
		cameraOrigin,
		normalize(
			p1 +
			xArm * (float(gl_GlobalInvocationID.x) / float(gl_NumWorkGroups.x * gl_WorkGroupSize.x)) +
			yArm * (float(gl_GlobalInvocationID.y) / float(gl_NumWorkGroups.y * gl_WorkGroupSize.y))
		),
		vec3(1.0),
		(1. / 0.),
		gl_GlobalInvocationID.x,
		gl_GlobalInvocationID.y,
		vec3(0.0),
		-1);
}