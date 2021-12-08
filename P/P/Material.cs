using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;

namespace P
{
    class Material
    {
        public Vector3 color;
        public bool isCheckerd;
        public float diffuse, specular;
        public bool dielectric;
        public float refractionIndex = 1.0f;

        //Note: for dielectrics, extinction rate = color.
        public Material(Vector3 color, float diffuse, float specular, bool dielectric, bool isCheckered = false, float refractionIndex = 1.5f)
        {
            this.color = color;
            this.isCheckerd = isCheckered;
            this.refractionIndex = refractionIndex;
            if (dielectric)
            {
                diffuse = 0;
                specular = 0;
            }
            this.dielectric = dielectric;

            this.diffuse = diffuse;
            this.specular = specular;
        }
    }
}
