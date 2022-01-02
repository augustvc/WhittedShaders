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

int bvhLocation = 1;
uint rayNum = 4000000000;

float rayAABB(int location) {
	// Ray-AABB intersection code inspired by https://www.scratchapixel.com/lessons/3d-basic-rendering/minimal-ray-tracer-rendering-simple-shapes/ray-box-intersection
	int signX = rays[rayNum].dir.x > 0f ? 1 : 0;
	int signY = rays[rayNum].dir.y > 0f ? 1 : 0;
	int signZ = rays[rayNum].dir.z > 0f ? 1 : 0;

	vec3[2] bounds = { vec3(bvhs[location].maxX, bvhs[location].maxY, bvhs[location].maxZ),
		vec3(bvhs[location].minX, bvhs[location].minY, bvhs[location].minZ) };

	float tmin = (bounds[signX].x - rays[rayNum].origin.x) * rays[rayNum].invdir.x;
	float tmax = (bounds[1 - signX].x - rays[rayNum].origin.x) * rays[rayNum].invdir.x;
	float tymin = (bounds[signY].y - rays[rayNum].origin.y) * rays[rayNum].invdir.y;
	float tymax = (bounds[1 - signY].y - rays[rayNum].origin.y) * rays[rayNum].invdir.y;

	if ((tmin > tymax) || (tymin > tmax))
		return 1. / 0.;
	if (tymin > tmin)
		tmin = tymin;
	if (tymax < tmax)
		tmax = tymax;

	float tzmin = (bounds[signZ].z - rays[rayNum].origin.z) * rays[rayNum].invdir.z;
	float tzmax = (bounds[1 - signZ].z - rays[rayNum].origin.z) * rays[rayNum].invdir.z;

	if ((tmin > tzmax) || (tzmin > tmax))
		return 1. / 0.;
	if (tzmin > tmin)
		tmin = tzmin;
	if (tzmax < tmax)
		tmax = tzmax;

	if (tmin < 0f && tmax >= 0f)
	{
		return 0f;
	}
	return tmin;
}

void main() {
	int previousLocation = 0;
	bool rayOutdated = true;

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
			if (bvhs[bvhLocation].indicesStart != bvhs[bvhLocation].indicesEnd) {
				//Make sure we're not in a leaf
				previousLocation = bvhLocation;
				bvhLocation = bvhLocation / 2;
			}
			
			int near = bvhLocation * 2;
			int far = near + 1;
			float nearDist = rayAABB(near);
			float farDist = rayAABB(far);

			if (nearDist > farDist) {
				int tempi = far;
				far = near;
				near = tempi;

				float tempf = farDist;
				farDist = nearDist;
				nearDist = tempf;
			}

			if (previousLocation == far) {
				if (bvhLocation <= 1) {
					rayOutdated = true;
					break;
				}
				previousLocation = bvhLocation;
				bvhLocation = bvhLocation / 2;
			}
			else {
				int nextChild = near;
				float nextDist = nearDist;
				if (previousLocation > bvhLocation) {
					nextChild = far;
					nextDist = farDist;
				}

				if (nextDist >= 0f && nextDist < rays[rayNum].t) {
					//Traverse the node
					previousLocation = bvhLocation;
					bvhLocation = nextChild;
				}
				else {
					//Don't traverse this node, act like we came from it
					previousLocation = nextChild;
				}
			}

			if (bvhs[bvhLocation].indicesStart != bvhs[bvhLocation].indicesEnd) {
				//Found a leaf to check out. We're done
				break;
			}
		}

		if (rayOutdated) {
			continue;
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