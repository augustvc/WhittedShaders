layout(local_size_x = 64, local_size_y = 1) in;
layout(rgba32f, binding = 0) uniform image2D img_output;

struct Ray
{
	vec3 origin;
	vec3 dir;
	vec3 invdir;
	vec3 energy;
	float t;
	uint pixelX;
	uint pixelY;
	vec3 ambient;
	int primID;
	int bvhDebug;
};

layout(std430, binding = 3) buffer shadowRayBuffer
{
	Ray shadowRays[];
};

layout(binding = 4, offset = 8) uniform atomic_uint shadowRayCount;

void main() {
	Ray ray;

	uint totalRays = atomicCounter(shadowRayCount);
	uint iter = 0;
	while (gl_GlobalInvocationID.x + (iter * 262144) < totalRays) {
		ray = shadowRays[gl_GlobalInvocationID.x + (iter * 262144)];

		iter++;
		//intersect(ray);
		float debugVal = ray.bvhDebug;
		if (debugVal > 0f) {
			debugVal = debugVal / 255.0;
		}

		vec4 pixel = vec4(ray.energy, 1.0);
		if (ray.primID >= 0) {
			pixel = vec4(ray.ambient, 1.0);
		}

		ivec2 pixel_coords = ivec2(ray.pixelX, ray.pixelY);
		vec4 clr = imageLoad(img_output, pixel_coords);
		imageStore(img_output, pixel_coords, clr + pixel);
	}
}