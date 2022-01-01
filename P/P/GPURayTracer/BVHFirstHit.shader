layout(local_size_x = 64, local_size_y = 1) in;

layout(std430, binding = 2) buffer rayOutBuffer
{
	Ray raysOut[];
};

layout(binding = 4) uniform atomic_uint rayCountIn;
layout(binding = 4, offset = 4) uniform atomic_uint rayCountOut;
layout(binding = 4, offset = 8) uniform atomic_uint shadowRayCount;
layout(binding = 4, offset = 12) uniform atomic_uint intersectionJob;

struct BVH
{
	float minX;
	float minY;
	float minZ;
	float maxX;
	float maxY;
	float maxZ;

	int indicesStart;
	int indicesEnd;
};

layout(std430, binding = 7) buffer BVHBuffer
{
	BVH bvhs[];
};

layout(std430, binding = 3) buffer shadowRayBuffer
{
	Ray shadowRays[];
};

//Light lights[] = Light[](
	//  Light(vec3(0.0, 12.0, 0.0), vec3(156.0, 156.0, 156.0))
	//, Light(vec3(0.0, -8.0, 0.0), vec3(2.0, 0.0, 16.0))
//);

vec3 lightPosition = vec3(0.0, 120.0, 0.0);
vec3 lightValue = vec3(6000.0, 6000.0, 6000.0);

void main() {
	int bvhLocation = 1;
	int previousLocation = 0;
	bool rayOutdated = true;
	uint rayNum = 4000000000;

	while (true) {
		if (rayOutdated) {
			rayNum = atomicCounterIncrement(intersectionJob);
			if (rayNum >= atomicCounter(rayCountIn)) {
				break;
			}
			bvhLocation = 1;
			previousLocation = 0;
			rayOutdated = false;
		}
		
		while (true) {
			if ((bvhs[bvhLocation].indicesStart != bvhs[bvhLocation].indicesEnd) || (previousLocation == bvhLocation * 2 + 1)) {
				//If in a leaf, or came from right child
				if (bvhLocation <= 1) {
					//In root, there is no parent.
					rayOutdated = true;
					break;
				}
				previousLocation = bvhLocation;
				bvhLocation = bvhLocation / 2;
			}
			else {
				int addition = 0;
				if (previousLocation > bvhLocation) {
					addition = 1;
				}
				previousLocation = bvhLocation;
				bvhLocation = bvhLocation * 2 + addition;				
			}

			if (bvhs[bvhLocation].indicesStart != bvhs[bvhLocation].indicesEnd) {
				//Found a leaf to check out. We're done
				break;
			}
		}

		int i = bvhs[bvhLocation].indicesStart;
		while(i <= bvhs[bvhLocation].indicesEnd) {
			uint triAI = indexBuffer[i++];
			uint triBI = indexBuffer[i++];
			uint triCI = indexBuffer[i++];

			vec3 triA = vec3(vertexBuffer[triAI * 8], vertexBuffer[triAI * 8 + 1], vertexBuffer[triAI * 8 + 2]);
			vec3 triB = vec3(vertexBuffer[triBI * 8], vertexBuffer[triBI * 8 + 1], vertexBuffer[triBI * 8 + 2]);
			vec3 triC = vec3(vertexBuffer[triCI * 8], vertexBuffer[triCI * 8 + 1], vertexBuffer[triCI * 8 + 2]);
			vec3 ab = triB - triA;
			vec3 ac = triC - triA;

			vec3 cross1 = cross(rays[rayNum].dir, ac);
			float det = dot(ab, cross1);
			if (abs(det) < 0.0001)
				continue;

			float detInv = 1.0 / det;
			vec3 diff = rays[rayNum].origin - triA;
			float u = dot(diff, cross1) * detInv;
			if (u < 0 || u > 1) {
				continue;
			}

			vec3 cross2 = cross(diff, ab);
			float v = dot(rays[rayNum].dir, cross2) * detInv;
			if (v < 0 || v > 1)
				continue;

			if (u + v > 1)
				continue;
			float t = dot(ac, cross2) * detInv;
			if (t <= 0)
				continue;

			if (t < rays[rayNum].t) {
				rays[rayNum].t = t;
				rays[rayNum].primID = 30000 + i - 3;
			}
		}
	}
}