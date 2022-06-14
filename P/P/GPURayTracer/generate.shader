/*
Copyright 2022 August van Casteren & Shreyes Jishnu Suchindran

You may use this software freely for non-commercial purposes. For any commercial purpose, please contact the authors.

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
layout(local_size_x = 32, local_size_y = 1) in;

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

layout(binding = 4) uniform atomic_uint rayCountIn;

uniform vec3 cameraOrigin = vec3(0.0, 0.0, 0.0);
uniform vec3 p1 = vec3(-1.0, -1.0, 2.0);
uniform vec3 xArm = vec3(2.0, 0.0, 0.0);
uniform vec3 yArm = vec3(0.0, 2.0, 0.0);

void main() {
	vec3 dir = normalize(
		p1 +
		xArm * (float(gl_GlobalInvocationID.x) / float(gl_NumWorkGroups.x * gl_WorkGroupSize.x)) +
		yArm * (float(gl_GlobalInvocationID.y) / float(gl_NumWorkGroups.y * gl_WorkGroupSize.y))
	);
	
	rays[atomicCounterIncrement(rayCountIn)] = Ray(
		cameraOrigin,
		dir,
		vec3(1.0),
		(1. / 0.),
		gl_GlobalInvocationID.x,
		gl_GlobalInvocationID.y,
		vec3(0.0),
		-1,
		0);
}