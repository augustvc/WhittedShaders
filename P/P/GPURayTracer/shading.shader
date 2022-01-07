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
		//intersect(ray);
		float debugVal = ray.bvhDebug;
		if (debugVal > 0f) {
			debugVal = 0f;// debugVal / 255.0;
		}
		float debugVal2 = 0f;
		if (ray.bvhDebug > 6) {
			//debugVal2 = 1.0f;
		}
		if (ray.bvhDebug > 15) {
			//debugVal = 1.0f;
			debugVal2 = 0f;
		}

		vec4 pixel = vec4(debugVal, ray.energy.y, debugVal2, 1.0);
		if(ray.primID == -2) {
			pixel = vec4(debugVal, 0.0, debugVal2, 1.0);
		}
		if (ray.primID >= 0) {
			//pixel = vec4(ray.ambient, 1.0);
		}

		ivec2 pixel_coords = ivec2(ray.pixelX, ray.pixelY);
		vec4 clr = imageLoad(img_output, pixel_coords);
		imageStore(img_output, pixel_coords, clr + pixel);
	}
}