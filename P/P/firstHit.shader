#version 430
layout(local_size_x = 1, local_size_y = 1) in;

struct Ray
{
	vec3 origin;
	vec3 dir;
	float t;
};

layout(std430, binding = 5) buffer rayBuffer
{
	uint rayCount;
	Ray rays[];
};

vec3 sphereLocation = vec3(0.0, 0.2, 4.3);
float sphereR2 = 1.0;

void main() {
	int iter = 0;
	Ray ray;
	while (gl_GlobalInvocationID.x + iter * 524288 < rayCount) {
		ray = rays[gl_GlobalInvocationID.x + iter * 524288];

		vec3 C = sphereLocation - ray.origin;
		float t = dot(C, ray.dir);
		vec3 Q = C - (t * ray.dir);
		float dsq = dot(Q, Q);


		if (dsq > sphereR2)
			return;
		t -= sqrt(sphereR2 - dsq);

		if (t < 0) return;
		if (t > ray.t) return;
		rays[gl_GlobalInvocationID.x + iter * 524288].t = t;
		iter++;
	}
}