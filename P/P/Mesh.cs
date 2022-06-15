/*
Copyright 2022 August van Casteren & Shreyes Jishnu Suchindran

You may use this software freely for non-commercial purposes. For any commercial purpose, please contact the authors.

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P
{
    public class Mesh
    {
        public float[] vertices;
        public uint[] indices;

        public Mesh(float[] vertices, List<uint> indices)
        {
            this.vertices = vertices;
            this.indices = indices.ToArray();
        }
        public Mesh(float[] vertices, uint[] indices)
        {
            this.vertices = vertices;
            this.indices = indices;
        }
    }
}
