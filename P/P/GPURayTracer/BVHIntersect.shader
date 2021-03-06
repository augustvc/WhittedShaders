/*
Copyright 2022 August van Casteren & Shreyes Jishnu Suchindran

You may use this software freely for non-commercial purposes. For any commercial purpose, please contact the authors.

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
layout(local_size_x = 96, local_size_y = 1) in;

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
	uint matrixID;
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
	int leftOrEnd;

	int rightOrStart;
	int rightOrEnd;
};
//If leftOrEnd is negative, it points to a matrix, and leftOrStart points to a bvh. Same logic with the right side.  leftOrStart = -8 -> use matrices[7], -1 -> matrices[0]
//If leftOrStart is equal to leftOrEnd, they both point to a bvh as well.
//In the other cases, leftOrEnd is positive and so is leftOrStart. And from start to end is the range of triangles they point to, it is a leaf node.

layout(std430, binding = 8) buffer MatrixBuffer
{
	mat4 matrices[];
};

layout(std430, binding = 7) buffer BVHBuffer
{
	BVH bvhs[];
};

Ray ray;
uint rayNum = 4000000000;
uint matrixNum = 0;

float rayAABB(vec3 tmaxs, vec3 tmins) {
	// Ray-AABB intersection code inspired by https://www.scratchapixel.com/lessons/3d-basic-rendering/minimal-ray-tracer-rendering-simple-shapes/ray-box-intersection
	tmins = (tmins - ray.origin) / ray.dir;
	tmaxs = (tmaxs - ray.origin) / ray.dir;

	tmins.x = max(tmins.x, tmins.y);
	tmins.x = max(tmins.x, tmins.z);
	tmaxs.x = min(tmaxs.x, tmaxs.y);
	tmaxs.x = min(tmaxs.x, tmaxs.z);

	if (tmins.x < 0f && tmaxs.x >= 0f)
	{
		tmins.x = 0f;
	}
	if (tmins.x > tmaxs.x) {
		// There is no intersection, return infinity so it gets tossed when comparing tmin to ray.t
		tmins.x = 1. / 0.;
	}
	return tmins.x;
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
			ray.primID = i - 3;
			ray.matrixID = matrixNum;
		}
	}
}


layout(location = 1) uniform bool anyHit;
layout(location = 3) uniform int treeRoot;

// The traversal stack. Any nodes we want to traverse get put on here. Its size significantly alters performance, so it is better to keep it as small as possible.
// But don't make it too small! If you have a large scene with a deep BVH, it will overflow and potentially crash the program.
// Something to look into later would be using a buffer to store and retrieve the 2nd (barely used) half of the stack, so that the array can be smaller.
// Also, in earlier versions I used a shared int instead of an int[] as it was faster. 
// But for some reason the reverse seems to be the case now, so I swapped them back.
int stackCount;
int stack[32];

int restoreAt;

#extension GL_ARB_shader_group_vote : enable

// This shader has two functionalities.
// One of them is taking input rays and finding the very first primitive they intersect (if any). (a.k.a. First hit)
// The other one is taking input shadow rays and figuring out if the path from their origin to the light source is obstructed or not. (a.k.a. Any hit)
void main() 
{
	rayNum = atomicCounterIncrement(intersectionJob);

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

		matrixNum = 0;

		vec3 untransformedOrigin = ray.origin;
		vec3 untransformedDir = ray.dir;

		// The traversal stack. Put the root of the BVH on it as that's where we start traversing.
		stack[0] = treeRoot;
		stackCount = 1;

		int lowX = ray.dir.x > 0f ? 3 : 0;
		int lowY = ray.dir.y > 0f ? 4 : 1;
		int lowZ = ray.dir.z > 0f ? 5 : 2;
		int highX = ray.dir.x > 0f ? 0 : 3;
		int highY = ray.dir.y > 0f ? 1 : 4;
		int highZ = ray.dir.z > 0f ? 2 : 5;

		int loc = treeRoot;
		BVH bvh = bvhs[loc];

		// Triangle ranges that have to be done (tiny stack). This is used to do speculative traversal.
		// Having multiple values to potentially assign to allows us to traverse a new BVH node, even if we already had a range of triangles lined up to test.
		// I am intentionally not using an array for this, as I read online that GPUs can not dynamically index arrays in a register. It seems to perform well enough.
		int foundTris = 0;
		int start1 = 0;
		int start2 = 0;
		int start3 = 0;
		int end1 = 0;
		int end2 = 0;
		int end3 = 0;

		restoreAt = -1;

		while (true) {
			if (stackCount <= 0 && foundTris <= 0) {
				foundTris = 3;
				break;
			}
			while (stackCount > 0 && foundTris < 2 && (!allInvocationsARB(foundTris > 0))) {
				stackCount--;
				loc = stack[stackCount];
				
				if (stackCount == restoreAt) {
					//We've exited our object. Restore the untransformed ray.

					if (foundTris > 0) {
						//We still have triangles left to do in our transformed state before we can untransform the ray.
						//Exit the traversal loop and test triangles first.
						stackCount++;
						break;
					}

					ray.origin = untransformedOrigin;
					ray.dir = untransformedDir;

					lowX = ray.dir.x > 0f ? 3 : 0;
					lowY = ray.dir.y > 0f ? 4 : 1;
					lowZ = ray.dir.z > 0f ? 5 : 2;
					highX = ray.dir.x > 0f ? 0 : 3;
					highY = ray.dir.y > 0f ? 1 : 4;
					highZ = ray.dir.z > 0f ? 2 : 5;
					restoreAt = -1;
				}

				if (bvhs[loc].leftOrEnd < 0) {
					restoreAt = stackCount - 1;

					matrixNum = -(bvhs[loc].leftOrEnd + 1);

					mat3 dir_matrix = inverse(mat3(matrices[matrixNum]));
					ray.origin = (inverse(matrices[matrixNum]) * vec4(ray.origin, 1)).xyz;
					ray.dir = dir_matrix * ray.dir;

					lowX = ray.dir.x > 0f ? 3 : 0;
					lowY = ray.dir.y > 0f ? 4 : 1;
					lowZ = ray.dir.z > 0f ? 5 : 2;
					highX = ray.dir.x > 0f ? 0 : 3;
					highY = ray.dir.y > 0f ? 1 : 4;
					highZ = ray.dir.z > 0f ? 2 : 5;
				}

				float leftDist = rayAABB(
					vec3(bvhs[loc].AABBs[lowX], bvhs[loc].AABBs[lowY], bvhs[loc].AABBs[lowZ]),
					vec3(bvhs[loc].AABBs[highX], bvhs[loc].AABBs[highY], bvhs[loc].AABBs[highZ]));
				
				float rightDist = rayAABB(
					vec3(bvhs[loc].AABBs[lowX + 6], bvhs[loc].AABBs[lowY + 6], bvhs[loc].AABBs[lowZ + 6]),
					vec3(bvhs[loc].AABBs[highX + 6], bvhs[loc].AABBs[highY + 6], bvhs[loc].AABBs[highZ + 6]));

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

				//Add left later, so it will get popped and therefore traversed first.
				if (rightDist >= 0f && rightDist < ray.t) {
					if (bvh.rightOrStart != bvh.rightOrEnd && (bvh.rightOrEnd >= 0)) {
						if (foundTris == 1) {
							start2 = bvh.rightOrStart;
							end2 = bvh.rightOrEnd;
						}
						else {
							start1 = bvh.rightOrStart;
							end1 = bvh.rightOrEnd;
						}
						foundTris++;
					}
					else {
						stack[stackCount] = bvh.rightOrStart;
						stackCount++;
					}
				}
				if (leftDist >= 0f && leftDist < ray.t) {
					if (bvh.leftOrStart != bvh.leftOrEnd && (bvh.leftOrEnd >= 0)) {
						if (foundTris == 2) {
							start3 = bvh.leftOrStart;
							end3 = bvh.leftOrEnd;
						}
						else if (foundTris == 1) {
							start2 = bvh.leftOrStart;
							end2 = bvh.leftOrEnd;
						}
						else {
							start1 = bvh.leftOrStart;
							end1 = bvh.leftOrEnd;
						}
						foundTris++;
					}
					else {
						stack[stackCount] = bvh.leftOrStart;
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
					// Shadow ray was obstructed. That's all we need to know about this ray.
					break;
				}
			}
		}

		// Transform the ray back to its original form, so that when we process it later for e.g. N-dot-L shading, it doesn't use wrong values.
		ray.origin = untransformedOrigin;
		ray.dir = untransformedDir;

		// Update the ray in the buffer that we originally pulled to do our calculations on.
		if (anyHit) {
			shadowRays[rayNum] = ray;
		}
		else {
			rays[rayNum] = ray;
		}

		rayNum = atomicCounterIncrement(intersectionJob);
	}
}