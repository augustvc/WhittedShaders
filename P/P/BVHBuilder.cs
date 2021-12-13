using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;

namespace P
{
    class TopLevelBVH
    {
        float[] vertices;
        BVH TopBVH;

        public TopLevelBVH(float[] vertices, List<uint> indices)
        {
            this.vertices = vertices;
            TopBVH = new BVH(vertices, indices);
            RecursiveSplit(ref TopBVH);
        }

        void RecursiveSplit(ref BVH bvh)
        {
            int splitAxis = 0;
            float biggestRange = 0f;
            Vector3 range = bvh.AABBMax - bvh.AABBMin;
            for(var a = 0; a < 3; a++)
            {
                if(range[a] > biggestRange)
                {
                    biggestRange = range[a];
                    splitAxis = a;
                }
            }

            float middle = bvh.AABBMin[splitAxis] + 0.5f * range[splitAxis];

            List<uint> leftIndices = new List<uint>();
            List<uint> rightIndices = new List<uint>();

            for(int i = 0; i < bvh.triangleIndices.Count; i+=3)
            {
                bool leftTri = true;
                for(int j = i; j < i + 3; j++)
                {
                    if(vertices[bvh.triangleIndices[j] * 8 + splitAxis] > middle)
                    {
                        leftTri = false;
                    }
                }
                if(leftTri)
                {
                    leftIndices.AddRange(bvh.triangleIndices.GetRange(i, 3));
                } else
                {
                    rightIndices.AddRange(bvh.triangleIndices.GetRange(i, 3));
                }
            }

            if(leftIndices.Count == 0 || rightIndices.Count == 0)
            {
                return;
            }

            BVH leftBVH = new BVH(vertices, leftIndices);
            BVH rightBVH = new BVH(vertices, rightIndices);

            if(leftBVH.SAH + rightBVH.SAH < bvh.SAH)
            {
                //Good split. Let's finalize it and try to split further
                bvh.AddChild(leftBVH);
                bvh.AddChild(rightBVH);
                RecursiveSplit(ref leftBVH);
                RecursiveSplit(ref rightBVH);
            }
        }
    }

    class BVH
    {
        public Vector3 AABBMin;
        public Vector3 AABBMax;
        public bool isLeaf = true;
        List<BVH> children = new List<BVH>();

        public float SAH;
        public List<uint> triangleIndices = new List<uint>();

        public BVH(float[] vertices, List<uint> indices)
        {
            MakeAABB(vertices, indices, out AABBMin, out AABBMax);
            SAH = CalculateHalfSA(AABBMin, AABBMax) * indices.Count;
            triangleIndices = indices;
        }

        public void AddChild(BVH child)
        {
            isLeaf = false;
            children.Add(child);
        }

        public static float CalculateHalfSA(Vector3 AABBMin, Vector3 AABBMax)
        {
            Vector3 diff = AABBMax - AABBMin;
            return diff[0] * diff[1] + diff[0] * diff[2] + diff[1] * diff[2];
        }

        public void MakeAABB(float[] vertices, List<uint> indices, out Vector3 AABBMin, out Vector3 AABBMax)
        {
            AABBMin = new Vector3(float.MaxValue);
            AABBMax = new Vector3(float.MinValue);

            for (var i = 0; i < indices.Count; i++)
            {
                for (var a = 0; a < 3; a++)
                {
                    if (vertices[indices[i] * 8 + a] < AABBMin[a])
                    {
                        AABBMin[a] = vertices[indices[i] * 8 + a];
                    }
                    if(vertices[indices[i] * 8 + a] > AABBMax[a])
                    {
                        AABBMax[a] = vertices[indices[i] * 8 + a];
                    }
                }
            }
        }
    }
}
