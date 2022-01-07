layout(local_size_x = 64, local_size_y = 1) in;

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

layout(std430, binding = 1) buffer rayInBuffer
{
	Ray rays[];
};

layout(std430, binding = 5) buffer vertexBufferObj
{
	float vertexBuffer[];
};

layout(std430, binding = 6) buffer indexBufferObj
{
	uint indexBuffer[];
};

struct Material
{
	vec3 color;
	float diffuse;
	float specular;
};

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
	float AABB[6];

	int indicesStart;
	int indicesEnd;

	int parent;
	int leftChild;
	int rightChild;
};

layout(std430, binding = 7) buffer BVHBuffer
{
	BVH bvhs[];
};

Ray ray;
uint rayNum = 4000000000;

float rayAABB(int location) {
	// Ray-AABB intersection code inspired by https://www.scratchapixel.com/lessons/3d-basic-rendering/minimal-ray-tracer-rendering-simple-shapes/ray-box-intersection
	int signX = ray.dir.x > 0f ? 1 : 0;
	int signY = ray.dir.y > 0f ? 1 : 0;
	int signZ = ray.dir.z > 0f ? 1 : 0;

	vec3[2] bounds = { vec3(bvhs[location].AABB[3], bvhs[location].AABB[4], bvhs[location].AABB[5]),
		vec3(bvhs[location].AABB[0], bvhs[location].AABB[1], bvhs[location].AABB[2]) };

	float tmin = (bounds[signX].x - ray.origin.x) * ray.invdir.x;
	float tmax = (bounds[1 - signX].x - ray.origin.x) * ray.invdir.x;
	float tymin = (bounds[signY].y - ray.origin.y) * ray.invdir.y;
	float tymax = (bounds[1 - signY].y - ray.origin.y) * ray.invdir.y;

	if ((tmin > tymax) || (tymin > tmax))
		tmin = 1. / 0.;
	tmin = max(tymin, tmin);
	tmax = min(tmax, tymax);

	float tzmin = (bounds[signZ].z - ray.origin.z) * ray.invdir.z;
	float tzmax = (bounds[1 - signZ].z - ray.origin.z) * ray.invdir.z;

	if ((tmin > tzmax) || (tzmin > tmax))
		tmin = 1. / 0.;
	tmin = max(tzmin, tmin);
	tmax = min(tzmax, tmax);

	if (tmin < 0f && tmax >= 0f)
	{
		tmin = 0f;
	}
	return tmin;
}

void doTris(int start, int end) {
	int i = start;
	while (i <= end) {
		uint triAI = indexBuffer[i++];
		uint triBI = indexBuffer[i++];
		uint triCI = indexBuffer[i++];

		vec3 triA = vec3(vertexBuffer[triAI * 3], vertexBuffer[triAI * 3 + 1], vertexBuffer[triAI * 3 + 2]);
		vec3 triB = vec3(vertexBuffer[triBI * 3], vertexBuffer[triBI * 3 + 1], vertexBuffer[triBI * 3 + 2]);
		vec3 triC = vec3(vertexBuffer[triCI * 3], vertexBuffer[triCI * 3 + 1], vertexBuffer[triCI * 3 + 2]);
		vec3 ab = triB - triA;
		vec3 ac = triC - triA;

		vec3 cross1 = cross(ray.dir, ac);
		float det = dot(ab, cross1);
		if (abs(det) < 0.0001)
			continue;

		float detInv = 1.0 / det;
		vec3 diff = ray.origin - triA;
		float u = dot(diff, cross1) * detInv;
		if (u < 0 || u > 1) {
			continue;
		}

		vec3 cross2 = cross(diff, ab);
		float v = dot(ray.dir, cross2) * detInv;
		if (v < 0 || v > 1)
			continue;

		if (u + v > 1)
			continue;
		float t = dot(ac, cross2) * detInv;
		if (t <= 0)
			continue;

		if (t < ray.t) {
			ray.t = t;
			ray.primID = 30000 + i - 3;
		}
	}
}

//Dragon bvh nodes: 39053

void main() {
	rayNum = atomicCounterIncrement(intersectionJob);
	ray = rays[rayNum];

	int stack[20];
	int stackCount = 1;
	stack[0] = 0;
	int loc = 0;
	ray.bvhDebug = 0;
	while (stackCount > 0) {
		ray.bvhDebug++;
		stackCount--;
		loc = stack[stackCount];
		if (bvhs[loc].indicesStart != bvhs[loc].indicesEnd) {
			doTris(bvhs[loc].indicesStart, bvhs[loc].indicesEnd);
		} else {
			int left = bvhs[loc].leftChild;
			float leftDist = rayAABB(left);
			int right = bvhs[loc].rightChild;
			float rightDist = rayAABB(bvhs[loc].rightChild);

			if (rightDist < leftDist) {
				int tempI = left;
				left = right;
				right = tempI;
				float tempF = leftDist;
				leftDist = rightDist;
				rightDist = tempF;
			}

			if(rightDist >= 0f && rightDist < ray.t) {
				stack[stackCount] = right;
				stackCount++;
			}
			if(leftDist >= 0f && leftDist < ray.t) {
				stack[stackCount] = left;
				stackCount++;
			}
		}
	}

	rays[rayNum] = ray;
}