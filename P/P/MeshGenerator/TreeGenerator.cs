using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P.MeshGenerator
{
    public static class TreeGenerator
    {
        /// <summary>
        /// R: Tree root
        /// B: Branch
        /// T: Straight trunk segment
        /// L: Leaf
        /// [: Add state to stack
        /// ]: Pop state from stack
        /// +: Turn right
        /// -: Turn left
        /// </summary>


        public static Mesh GenerateTree(int seed = 0)
        {
            Random rnd = new Random(seed);

            int iterations = 30;

            string lsystem = "R";

            for(int i = 0; i < iterations; i++)
            {
                string newSystem = "";
                for(int j = 0; j < lsystem.Length; j++)
                {
                    switch (lsystem[j]) {
                        case 'R':
                            newSystem += "RT";
                            break;
                        case 'T':
                            newSystem += "T";
                            break;
                    }
                }
                lsystem = newSystem;
            }

            return CreateMesh(lsystem);
        }

        public static Mesh CreateMesh(string lsystem)
        {
            Console.WriteLine("Creating mesh from lsystem " + lsystem);

            List<float> vertices = new List<float>();
            List<uint> indices = new List<uint>();
            State state = new State(new Vector3(0f), new Vector3(0f, 1f, 0f), new Vector3(1f, 0f, 0f));

            void add(Vector3 vertex)
            {
                indices.Add((uint)vertices.Count / 3);
                vertices.Add(vertex.X); vertices.Add(vertex.Y); vertices.Add(vertex.Z);
            }

            List<State> stack = new List<State>();

            for(int i = 0; i < lsystem.Length; i++)
            {
                switch(lsystem[i])
                {
                    case 'T':
                        add(state.position);
                        add(state.position + state.direction + state.right);
                        add(state.position + state.direction - state.right);
                        state.position += state.direction;
                        break;
                    case 'L':
                        break;
                    case '[':
                        stack.Add(state);
                        break;
                    case ']':
                        state = stack[stack.Count - 1];
                        stack.RemoveAt(stack.Count - 1);
                        break;
                }
            }

            //TODO: Filter out duplicate vertex entries from vertices
            Mesh mesh = new Mesh(vertices.ToArray(), indices);
            return mesh;
        }
    }

    struct State
    {
        public Vector3 position;
        public Vector3 direction;
        public Vector3 right;

        public State(Vector3 pos, Vector3 dir, Vector3 right)
        {
            position = pos;
            direction = dir;
            this.right = right;
        }
    }
}
