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
	while (i < end) {
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

struct Stack
{
	int nodes[20];
};

struct StupidStack
{
	int zero;
	int one;
	int two;
	int three;
	int four;
	int five;
	int six;
	int seven;
	int eight;
	int nine;
	int ten;
	int eleven;
	int twelve;
	int thirteen;
	int fourteen;
	int fifteen;
	int sixteen;
	int seventeen;
	int eighteen;
	int nineteen;
};

StupidStack stupidStack;

int getStack(int idx) {
	switch (idx) {
		case 0:
			return stupidStack.zero;
		case 1:
			return stupidStack.one;
		case 2:
			return stupidStack.two;
		case 3:
			return stupidStack.three;
		case 4:
			return stupidStack.four;
		case 5:
			return stupidStack.five;
		case 6:
			return stupidStack.six;
		case 7:
			return stupidStack.seven;
		case 8:
			return stupidStack.eight;
		case 9:
			return stupidStack.nine;
		case 10:
			return stupidStack.ten;
		case 11:
			return stupidStack.eleven;
		case 12:
			return stupidStack.twelve;
		case 13:
			return stupidStack.thirteen;
		case 14:
			return stupidStack.fourteen;
		case 15:
			return stupidStack.fifteen;
		case 16:
			return stupidStack.sixteen;
		case 17:
			return stupidStack.seventeen;
		case 18:
			return stupidStack.eighteen;
		case 19:
			return stupidStack.nineteen;
	}
}

void setStack(int idx, int value) {
	switch (idx) {
	case 0:
		stupidStack.zero = value;
		return;
	case 1:
		stupidStack.one = value;
		return;
	case 2:
		stupidStack.two = value;
		return;
	case 3:
		stupidStack.three = value;
		return;
	case 4:
		stupidStack.four = value;
		return;
	case 5:
		stupidStack.five = value;
		return;
	case 6:
		stupidStack.six = value;
		return;
	case 7:
		stupidStack.seven = value;
		return;
	case 8:
		stupidStack.eight = value;
		return;
	case 9:
		stupidStack.nine = value;
		return;
	case 10:
		stupidStack.ten = value;
		return;
	case 11:
		stupidStack.eleven = value;
		return;
	case 12:
		stupidStack.twelve = value;
		return;
	case 13:
		stupidStack.thirteen = value;
		return;
	case 14:
		stupidStack.fourteen = value;
		return;
	case 15:
		stupidStack.fifteen = value;
		return;
	case 16:
		stupidStack.sixteen = value;
		return;
	case 17:
		stupidStack.seventeen = value;
		return;
	case 18:
		stupidStack.eighteen = value;
		return;
	case 19:
		stupidStack.nineteen = value;
		return;
	}
}

int stackCount;
shared int stack[20 * 64];

//Stack stack;

void main() {
	rayNum = atomicCounterIncrement(intersectionJob);
	uint stackOffset = gl_LocalInvocationIndex * 20;

	uint maxRays = atomicCounter(rayCountIn);
	while (rayNum < maxRays) {
		ray = rays[rayNum];

		setStack(0, 0);
		stack[stackOffset] = 0;
		stackCount = 1;

		int loc = 0;
		ray.bvhDebug = 0;
		while (stackCount > 0) {
			if (stackCount > ray.bvhDebug)
				ray.bvhDebug = stackCount;
			stackCount--;
			loc = stack[stackOffset + stackCount];
			if (bvhs[loc].indicesStart != bvhs[loc].indicesEnd) {
				doTris(bvhs[loc].indicesStart, bvhs[loc].indicesEnd);
			}
			else {
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

				if (rightDist >= 0f && rightDist < ray.t) {
					stack[stackOffset + stackCount] = right;
					stackCount++;
				}
				if (leftDist >= 0f && leftDist < ray.t) {
					stack[stackOffset + stackCount] = left;
					stackCount++;
				}
			}
		}
		rays[rayNum] = ray;

		rayNum = atomicCounterIncrement(intersectionJob);
	}
}