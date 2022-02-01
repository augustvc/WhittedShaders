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
	int matrixID;
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

layout(std430, binding = 3) buffer shadowRayBuffer
{
	Ray shadowRays[];
};

layout(binding = 4) uniform atomic_uint rayCountIn;
layout(binding = 4, offset = 4) uniform atomic_uint rayCountOut;
layout(binding = 4, offset = 8) uniform atomic_uint shadowRayCount;
layout(binding = 4, offset = 12) uniform atomic_uint intersectionJob;

struct BVH
{
	float AABBs[12];

	int leftOrStart;
	int leftOrEnd; //-8 -> matrices[8]

	int rightOrStart;
	int rightOrEnd;
};

layout(std430, binding = 7) buffer BVHBuffer
{
	BVH bvhs[];
};

Ray ray;
uint rayNum = 4000000000;

float rayAABB(float maxX, float maxY, float maxZ, float minX, float minY, float minZ) {
	// Ray-AABB intersection code inspired by https://www.scratchapixel.com/lessons/3d-basic-rendering/minimal-ray-tracer-rendering-simple-shapes/ray-box-intersection
	//float tmin = (bounds[signX].x - ray.origin.x) * ray.invdir.x;
	//float tmax = (bounds[1 - signX].x - ray.origin.x) * ray.invdir.x;
	//float tymin = (bounds[signY].y - ray.origin.y) * ray.invdir.y;
	//float tymax = (bounds[1 - signY].y - ray.origin.y) * ray.invdir.y;

	float tmin = (minX - ray.origin.x) * ray.invdir.x;
	float tmax = (maxX - ray.origin.x) * ray.invdir.x;
	float tymin = (minY - ray.origin.y) * ray.invdir.y;
	float tymax = (maxY - ray.origin.y) * ray.invdir.y;

	if ((tmin > tymax) || (tymin > tmax))
		tmin = 1. / 0.;
	tmin = max(tymin, tmin);
	tmax = min(tmax, tymax);

	float tzmin = (minZ - ray.origin.z) * ray.invdir.z;
	float tzmax = (maxZ - ray.origin.z) * ray.invdir.z;

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
		if (abs(det) < 0.0000000000000000000001)
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


layout(location = 1) uniform bool anyHit;

int stackCount;
shared int stack[24 * 64];

layout(location = 5) uniform mat4 transform;

//Stack stack;
//#define GL_ARB_shader_group_vote          1
#extension GL_ARB_shader_group_vote : enable

void main() 
{
	rayNum = atomicCounterIncrement(intersectionJob);
	uint stackOffset = gl_LocalInvocationIndex * 24;

	uint maxRays = 0;
	if (anyHit) {
		maxRays = atomicCounter(shadowRayCount);
	}
	else {
		maxRays = atomicCounter(rayCountIn);
	}
	while (rayNum < maxRays) {
		if (anyHit) {
			ray = shadowRays[rayNum];
		} else {
			ray = rays[rayNum];
		}

		vec3 oldOrigin = ray.origin;
		vec3 oldDir = ray.dir;
		//transformation
		mat3 dir_matrix = inverse(mat3(transform));
		ray.origin = (inverse(transform) * vec4(ray.origin, 1)).xyz;
		ray.dir = (dir_matrix * ray.dir);
		//ray.origin.y = -ray.origin.y;
		//ray.dir.y = -ray.dir.y;
		ray.invdir = 1. / ray.dir;

		stack[stackOffset] = 0;
		stackCount = 1;
		
		int lowX = ray.dir.x > 0f ? 3 : 0;
		int lowY = ray.dir.y > 0f ? 4 : 1;
		int lowZ = ray.dir.z > 0f ? 5 : 2;
		int highX = ray.dir.x > 0f ? 0 : 3;
		int highY = ray.dir.y > 0f ? 1 : 4;
		int highZ = ray.dir.z > 0f ? 2 : 5;

		int loc = 0;
		BVH bvh = bvhs[loc];
		//Triangle ranges that have to be done (tiny stack)
		int foundTris = 0;
		int start1 = 0;
		int start2 = 0;
		int start3 = 0;
		int end1 = 0;
		int end2 = 0;
		int end3 = 0;

		while (true) {
			if (stackCount <= 0 && foundTris <= 0) {
				foundTris = 3;
				break;
			}
			while (stackCount > 0 && foundTris < 2 && (!allInvocationsARB(foundTris > 0))) {
				stackCount--;
				loc = stack[stackOffset + stackCount];

				float leftDist = rayAABB(bvhs[loc].AABBs[lowX], bvhs[loc].AABBs[lowY], bvhs[loc].AABBs[lowZ], bvhs[loc].AABBs[highX], bvhs[loc].AABBs[highY], bvhs[loc].AABBs[highZ]);
				float rightDist = rayAABB(bvhs[loc].AABBs[lowX + 6], bvhs[loc].AABBs[lowY + 6], bvhs[loc].AABBs[lowZ + 6], bvhs[loc].AABBs[highX + 6], bvhs[loc].AABBs[highY + 6], bvhs[loc].AABBs[highZ + 6]);

				bvh = bvhs[loc];
				if (leftDist > rightDist) {
					int tempI = bvh.leftOrStart;
					bvh.leftOrStart = bvh.rightOrStart;
					bvh.rightOrStart = tempI;

					tempI = bvh.leftOrEnd;
					bvh.leftOrEnd = bvh.rightOrEnd;
					bvh.rightOrEnd = tempI;

					float tempF = leftDist;
					leftDist = rightDist;
					rightDist = tempF;
				}

				//Add left later, so it will get popped first.
				if (rightDist >= 0f && rightDist < ray.t) {
					if (bvh.rightOrStart != bvh.rightOrEnd) {
						if (foundTris == 1) {
							start2 = bvh.rightOrStart;
							end2 = bvh.rightOrEnd;
						} else {
							start1 = bvh.rightOrStart;
							end1 = bvh.rightOrEnd;
						}
						foundTris++;
					}
					else {
						stack[stackOffset + stackCount] = bvh.rightOrStart;
						stackCount++;
					}
				}
				if (leftDist >= 0f && leftDist < ray.t) {
					if (bvh.leftOrStart != bvh.leftOrEnd) {
						if (foundTris == 2) {
							start3 = bvh.leftOrStart;
							end3 = bvh.leftOrEnd;
						} else if (foundTris == 1) {
							start2 = bvh.leftOrStart;
							end2 = bvh.leftOrEnd;
						} else {
							start1 = bvh.leftOrStart;
							end1 = bvh.leftOrEnd;
						}
						foundTris++;
					}
					else {
						stack[stackOffset + stackCount] = bvh.leftOrStart;
						stackCount++;
					}
				}
			}
			if (foundTris < 1) {
				//Slightly faster doing this currently, but not necessary.
				foundTris = 3;
				break;
			}
			if (foundTris == 2) {
				start3 = start2;
				end3 = end2;
			}
			if (foundTris == 1) {
				start3 = start1;
				end3 = end1;
			}

			doTris(start3, end3);
			foundTris--;
			if (anyHit) {
				if (ray.primID >= 0) {
					break;
				}
			}
		}

		ray.origin = oldOrigin;
		ray.dir = oldDir;

		if (anyHit) {
			shadowRays[rayNum] = ray;
		}
		else {
			rays[rayNum] = ray;
		}

		rayNum = atomicCounterIncrement(intersectionJob);
	}
}