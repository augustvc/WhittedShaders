layout(local_size_x = 64, local_size_y = 1) in;
layout(rgba32f, binding = 0) uniform image2D img_output;



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
		int primID = intersect(ray);
		vec4 pixel = vec4(ray.energy, 1.0);
		if (primID >= 0) {
			pixel = vec4(ray.ambient, 1.0);
		}

		for (int i = 0; i < 10000; i++) {
			if (i % 1000 == 384) {
				//pixel += vec4(0.0, 0.0, 0.0, 0.0001);
			}
		}

		ivec2 pixel_coords = ivec2(ray.pixelX, ray.pixelY);
		vec4 clr = imageLoad(img_output, pixel_coords);
		imageStore(img_output, pixel_coords, clr + pixel);
	}
}