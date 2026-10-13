using Dorsal.Core.Match;
using UnityEngine;

namespace Dorsal.Unity
{
    /// <summary>
    /// Estadio genérico construido por código: gradas escalonadas con público (textura procedural),
    /// vallas publicitarias con marcas ficticias y torres de focos. Sin modelos ni marcas de terceros.
    /// </summary>
    public static class StadiumBuilder
    {
        public static void Build(Transform parent, Color homeColor, int seed, int quality)
        {
            _crowdMaterial = null; // cada estadio con su público
            var root = new GameObject("Estadio").transform;
            root.SetParent(parent, false);
            var crowd = CrowdTexture(seed, homeColor, quality == 0 ? 128 : 256);
            float L = Pitch.HalfLength, W = Pitch.HalfWidth;

            // Vallas publicitarias (colores de patrocinadores ficticios).
            Color[] boards = { new Color(0.1f, 0.35f, 0.8f), new Color(0.95f, 0.75f, 0.1f), new Color(0.85f, 0.15f, 0.2f), new Color(0.1f, 0.65f, 0.4f), Color.white };
            int k = 0;
            for (float x = -L; x < L; x += 10f)
            {
                Box(root, new Vector3(x + 5f, 0.5f, W + 4f), new Vector3(9.6f, 1f, 0.2f), boards[k++ % boards.Length]);
                Box(root, new Vector3(x + 5f, 0.5f, -W - 4f), new Vector3(9.6f, 1f, 0.2f), boards[k++ % boards.Length]);
            }
            for (float z = -W; z < W; z += 10f)
            {
                Box(root, new Vector3(L + 4f, 0.5f, z + 5f), new Vector3(0.2f, 1f, 9.6f), boards[k++ % boards.Length]);
                Box(root, new Vector3(-L - 4f, 0.5f, z + 5f), new Vector3(0.2f, 1f, 9.6f), boards[k++ % boards.Length]);
            }

            if (quality == 0) return; // calidad baja: solo vallas

            int tiers = quality >= 2 ? 4 : 3;
            for (int t = 0; t < tiers; t++)
            {
                float off = 7f + t * 3.2f, h = 1.6f + t * 2.4f;
                Stand(root, new Vector3(0f, h / 2f, W + off), new Vector3(2 * L + 14f + t * 6f, h, 3.2f), crowd, new Vector2(8f, 1f));
                Stand(root, new Vector3(0f, h / 2f, -W - off), new Vector3(2 * L + 14f + t * 6f, h, 3.2f), crowd, new Vector2(8f, 1f));
                Stand(root, new Vector3(L + off, h / 2f, 0f), new Vector3(3.2f, h, 2 * W + 14f + t * 6f), crowd, new Vector2(5f, 1f));
                Stand(root, new Vector3(-L - off, h / 2f, 0f), new Vector3(3.2f, h, 2 * W + 14f + t * 6f), crowd, new Vector2(5f, 1f));
            }

            // Torres de focos en las esquinas.
            foreach (var corner in new[] { new Vector3(L + 18f, 0, W + 16f), new Vector3(-L - 18f, 0, W + 16f), new Vector3(L + 18f, 0, -W - 16f), new Vector3(-L - 18f, 0, -W - 16f) })
            {
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Torre";
                Object.Destroy(pole.GetComponent<Collider>());
                pole.transform.SetParent(root, false);
                pole.transform.localPosition = corner + new Vector3(0, 15f, 0);
                pole.transform.localScale = new Vector3(0.8f, 15f, 0.8f);
                pole.GetComponent<Renderer>().sharedMaterial = MaterialCache.Get(new Color(0.55f, 0.57f, 0.6f));
                Box(root, corner + new Vector3(0, 30.5f, 0), new Vector3(5f, 2.5f, 1f), new Color(1f, 0.98f, 0.85f));
            }
        }

        private static void Stand(Transform parent, Vector3 pos, Vector3 size, Texture2D crowd, Vector2 tiling)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Grada";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            // Material propio con la textura del público (instancia única compartida por las gradas).
            if (_crowdMaterial == null)
            {
                _crowdMaterial = new Material(MaterialCache.Get(Color.white)) { mainTexture = crowd, color = Color.white };
                _crowdMaterial.mainTextureScale = tiling;
            }
            r.sharedMaterial = _crowdMaterial;
        }

        private static Material _crowdMaterial;

        private static void Box(Transform parent, Vector3 pos, Vector3 size, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Valla";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = MaterialCache.Get(c);
        }

        /// <summary>Público: píxeles de colores (con predominio del color local) sobre fondo de asientos.</summary>
        private static Texture2D CrowdTexture(int seed, Color home, int size)
        {
            var rnd = new System.Random(seed);
            var tex = new Texture2D(size, size / 4, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var seat = new Color(0.22f, 0.24f, 0.3f);
            var pixels = new Color[tex.width * tex.height];
            for (int i = 0; i < pixels.Length; i++)
            {
                double r = rnd.NextDouble();
                Color c;
                if (r < 0.12) c = seat;
                else if (r < 0.55) c = Color.Lerp(home, Color.white, (float)rnd.NextDouble() * 0.3f);
                else if (r < 0.75) c = MaterialCache.SkinTones[rnd.Next(MaterialCache.SkinTones.Length)];
                else c = new Color((float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble()) * 0.8f;
                pixels[i] = c;
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }
    }
}
