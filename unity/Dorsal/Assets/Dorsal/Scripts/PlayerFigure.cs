using System.Collections.Generic;
using Dorsal.Core.Match;
using UnityEngine;

namespace Dorsal.Unity
{
    /// <summary>Aspecto de un futbolista (colores de equipación, piel, pelo, dorsal, altura).</summary>
    public struct FigureLook
    {
        public Color Shirt;
        public Color Shorts;
        public Color Socks;
        public Color Skin;
        public Color Hair;
        public int Number;
        public float HeightScale;
        public bool Keeper;
    }

    /// <summary>Caché de materiales por color: pocos materiales distintos para que el renderizado agrupe bien.</summary>
    public static class MaterialCache
    {
        private static Material _base;
        private static readonly Dictionary<int, Material> Cache = new Dictionary<int, Material>();

        public static Material Get(Color c)
        {
            if (_base == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _base = tmp.GetComponent<Renderer>().sharedMaterial;
                Object.Destroy(tmp);
            }
            Color32 c32 = c;
            int key = (c32.r << 24) | (c32.g << 16) | (c32.b << 8) | c32.a;
            if (!Cache.TryGetValue(key, out var m) || m == null)
            {
                m = new Material(_base) { color = c };
                Cache[key] = m;
            }
            return m;
        }

        public static readonly Color[] SkinTones =
        {
            new Color(0.98f, 0.84f, 0.72f), new Color(0.93f, 0.75f, 0.6f), new Color(0.82f, 0.62f, 0.45f),
            new Color(0.66f, 0.46f, 0.31f), new Color(0.48f, 0.32f, 0.21f), new Color(0.33f, 0.22f, 0.15f),
        };

        public static readonly Color[] HairColors =
        {
            new Color(0.08f, 0.06f, 0.05f), new Color(0.25f, 0.16f, 0.09f), new Color(0.45f, 0.3f, 0.15f),
            new Color(0.82f, 0.68f, 0.38f), new Color(0.55f, 0.2f, 0.1f), new Color(0.75f, 0.75f, 0.75f),
        };
    }

    /// <summary>
    /// Futbolista humanoide construido con primitivas y animado por código (carrera, golpeo, cabezazo,
    /// entrada, estirada, blocaje, saque y celebración). No usa modelos ni animaciones de terceros.
    /// En M3 puede sustituirse por un modelo con esqueleto manteniendo la misma interfaz.
    /// </summary>
    public sealed class PlayerFigure : MonoBehaviour
    {
        private Transform _body, _pelvis, _spine, _head, _legL, _legR, _armL, _armR;
        private float _phase;
        private bool _keeper;

        public static PlayerFigure Create(Transform parent, FigureLook look, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var fig = root.AddComponent<PlayerFigure>();
            fig.Build(look);
            return fig;
        }

        private void Build(FigureLook look)
        {
            _keeper = look.Keeper;
            float s = Mathf.Clamp(look.HeightScale, 0.88f, 1.14f);

            Part(PrimitiveType.Cylinder, "Sombra", transform, new Vector3(0f, 0.01f, 0f), new Vector3(0.75f, 0.005f, 0.75f), new Color(0.05f, 0.12f, 0.05f));

            _body = new GameObject("Cuerpo").transform;
            _body.SetParent(transform, false);
            _body.localScale = Vector3.one * s;

            _pelvis = Pivot("Pelvis", _body, new Vector3(0f, 0.95f, 0f));
            Part(PrimitiveType.Cube, "Pantalon", _pelvis, new Vector3(0f, -0.02f, 0f), new Vector3(0.36f, 0.24f, 0.22f), look.Shorts);

            _legL = Pivot("PiernaI", _pelvis, new Vector3(-0.1f, -0.08f, 0f));
            _legR = Pivot("PiernaD", _pelvis, new Vector3(0.1f, -0.08f, 0f));
            foreach (var leg in new[] { _legL, _legR })
            {
                Part(PrimitiveType.Capsule, "Muslo", leg, new Vector3(0f, -0.2f, 0f), new Vector3(0.15f, 0.22f, 0.15f), look.Skin);
                Part(PrimitiveType.Capsule, "Media", leg, new Vector3(0f, -0.62f, 0f), new Vector3(0.13f, 0.22f, 0.13f), look.Socks);
                Part(PrimitiveType.Cube, "Bota", leg, new Vector3(0f, -0.84f, 0.05f), new Vector3(0.11f, 0.07f, 0.24f), new Color(0.1f, 0.1f, 0.1f));
            }

            _spine = Pivot("Columna", _pelvis, new Vector3(0f, 0.08f, 0f));
            Part(PrimitiveType.Cube, "Camiseta", _spine, new Vector3(0f, 0.27f, 0f), new Vector3(0.42f, 0.52f, 0.24f), look.Shirt);

            // Dorsal en la espalda (texto propio, sin tipografías de terceros).
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var num = new GameObject("Dorsal");
            num.transform.SetParent(_spine, false);
            num.transform.localPosition = new Vector3(0f, 0.3f, -0.125f);
            num.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var tm = num.AddComponent<TextMesh>();
            tm.text = look.Number > 0 ? look.Number.ToString() : "";
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = 0.05f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = look.Shorts.grayscale > 0.5f && look.Shirt.grayscale > 0.5f ? Color.black : Color.white;
            num.GetComponent<MeshRenderer>().sharedMaterial = font.material;

            _armL = Pivot("BrazoI", _spine, new Vector3(-0.27f, 0.48f, 0f));
            _armR = Pivot("BrazoD", _spine, new Vector3(0.27f, 0.48f, 0f));
            foreach (var arm in new[] { _armL, _armR })
            {
                Part(PrimitiveType.Capsule, "Manga", arm, new Vector3(0f, -0.16f, 0f), new Vector3(0.12f, 0.17f, 0.12f), look.Shirt);
                Part(PrimitiveType.Capsule, "Antebrazo", arm, new Vector3(0f, -0.45f, 0f), new Vector3(0.1f, 0.15f, 0.1f), look.Keeper ? look.Shirt : look.Skin);
                if (look.Keeper) Part(PrimitiveType.Sphere, "Guante", arm, new Vector3(0f, -0.63f, 0f), new Vector3(0.13f, 0.13f, 0.13f), new Color(0.95f, 0.95f, 0.95f));
            }

            _head = Pivot("Cabeza", _spine, new Vector3(0f, 0.66f, 0f));
            Part(PrimitiveType.Sphere, "Cara", _head, Vector3.zero, new Vector3(0.24f, 0.27f, 0.25f), look.Skin);
            Part(PrimitiveType.Sphere, "Pelo", _head, new Vector3(0f, 0.07f, -0.02f), new Vector3(0.26f, 0.16f, 0.26f), look.Hair);
        }

        private static Transform Pivot(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        private static void Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = MaterialCache.Get(color);
        }

        /// <summary>
        /// Anima la figura. <paramref name="speed"/> en m/s; <paramref name="diveSide"/>: −1 izquierda, +1 derecha, 0 blocaje.
        /// </summary>
        public void Animate(float dt, float speed, AgentAction action, float actionTime, float diveTime, int diveSide)
        {
            float amp = Mathf.Clamp01(speed / 7f);
            _phase += dt * (2.2f + speed * 1.7f);
            float swing = Mathf.Sin(_phase) * 48f * amp;

            // Base: carrera / reposo.
            Quaternion legL = Quaternion.Euler(swing, 0f, 0f);
            Quaternion legR = Quaternion.Euler(-swing, 0f, 0f);
            Quaternion armL = Quaternion.Euler(-swing * 0.8f, 0f, -8f);
            Quaternion armR = Quaternion.Euler(swing * 0.8f, 0f, 8f);
            Quaternion spine = Quaternion.Euler(4f + speed * 1.6f, 0f, 0f);
            Quaternion body = Quaternion.identity;
            float bob = Mathf.Abs(Mathf.Sin(_phase)) * 0.05f * amp + Mathf.Sin(Time.time * 2f) * 0.005f;
            Vector3 bodyPos = Vector3.zero;
            if (_keeper && speed < 1f)
            {
                // Posición de espera del portero: brazos abiertos y algo agachado.
                armL = Quaternion.Euler(-30f, 0f, -35f);
                armR = Quaternion.Euler(-30f, 0f, 35f);
                bodyPos.y -= 0.06f;
            }

            float t = actionTime;
            switch (action)
            {
                case AgentAction.Pass:
                case AgentAction.Shoot:
                case AgentAction.Cross:
                {
                    float power = action == AgentAction.Shoot ? 85f : 60f;
                    float k = t < 0.1f ? -t / 0.1f * 0.5f : t < 0.25f ? (t - 0.1f) / 0.15f : Mathf.Clamp01(1f - (t - 0.25f) / 0.3f);
                    legR = Quaternion.Euler(-power * k, 0f, 0f);
                    armL = Quaternion.Euler(-20f, 0f, -45f * Mathf.Abs(k));
                    spine = Quaternion.Euler(-8f * k, 0f, 0f);
                    break;
                }
                case AgentAction.Header:
                {
                    float k = Mathf.Sin(Mathf.Clamp01(t / 0.5f) * Mathf.PI);
                    bodyPos.y += 0.45f * k;
                    spine = Quaternion.Euler(-15f + 35f * Mathf.Clamp01(t / 0.35f), 0f, 0f);
                    armL = Quaternion.Euler(-40f * k, 0f, -30f);
                    armR = Quaternion.Euler(-40f * k, 0f, 30f);
                    break;
                }
                case AgentAction.Tackle:
                {
                    float k = t < 0.15f ? t / 0.15f : t < 0.55f ? 1f : Mathf.Clamp01(1f - (t - 0.55f) / 0.25f);
                    body = Quaternion.Euler(-55f * k, 0f, 0f);
                    bodyPos.y -= 0.45f * k;
                    legR = Quaternion.Euler(-80f * k, 0f, 0f);
                    legL = Quaternion.Euler(-30f * k, 0f, 0f);
                    break;
                }
                case AgentAction.Dive:
                {
                    float d = diveTime >= 0f ? diveTime : t;
                    float down = Mathf.Clamp01(d / 0.3f) * (1f - Mathf.Clamp01((d - 1.0f) / 0.25f));
                    if (diveSide != 0)
                    {
                        body = Quaternion.Euler(0f, 0f, -82f * diveSide * down);
                        bodyPos.y += 0.35f * Mathf.Sin(Mathf.Clamp01(d / 0.45f) * Mathf.PI) * down;
                        armL = Quaternion.Euler(0f, 0f, -170f * down);
                        armR = Quaternion.Euler(0f, 0f, 170f * down);
                        legL = Quaternion.Euler(0f, 0f, -12f * down);
                        legR = Quaternion.Euler(0f, 0f, 12f * down);
                    }
                    else
                    {
                        // Blocaje: agachado, brazos al frente.
                        bodyPos.y -= 0.25f * down;
                        spine = Quaternion.Euler(25f * down, 0f, 0f);
                        armL = Quaternion.Euler(-80f * down, 0f, -10f);
                        armR = Quaternion.Euler(-80f * down, 0f, 10f);
                    }
                    break;
                }
                case AgentAction.Catch:
                {
                    float k = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01(1f - (t - 0.6f) / 0.2f);
                    armL = Quaternion.Euler(-85f * k, 0f, -12f);
                    armR = Quaternion.Euler(-85f * k, 0f, 12f);
                    spine = Quaternion.Euler(15f * k, 0f, 0f);
                    break;
                }
                case AgentAction.Throw:
                {
                    float k = Mathf.Clamp01(t / 0.35f);
                    armL = Quaternion.Euler(-170f + 120f * k, 0f, -5f);
                    armR = Quaternion.Euler(-170f + 120f * k, 0f, 5f);
                    break;
                }
                case AgentAction.Celebrate:
                {
                    bodyPos.y += Mathf.Abs(Mathf.Sin(t * 7f)) * 0.18f;
                    armL = Quaternion.Euler(0f, 0f, -150f);
                    armR = Quaternion.Euler(0f, 0f, 150f);
                    break;
                }
                case AgentAction.Stumble:
                {
                    float k = Mathf.Sin(Mathf.Clamp01(t / 0.7f) * Mathf.PI);
                    spine = Quaternion.Euler(30f * k, 0f, 0f);
                    armL = Quaternion.Euler(-50f * k, 0f, -30f);
                    armR = Quaternion.Euler(-50f * k, 0f, 30f);
                    break;
                }
            }

            float blend = 1f - Mathf.Exp(-18f * dt);
            _legL.localRotation = Quaternion.Slerp(_legL.localRotation, legL, blend);
            _legR.localRotation = Quaternion.Slerp(_legR.localRotation, legR, blend);
            _armL.localRotation = Quaternion.Slerp(_armL.localRotation, armL, blend);
            _armR.localRotation = Quaternion.Slerp(_armR.localRotation, armR, blend);
            _spine.localRotation = Quaternion.Slerp(_spine.localRotation, spine, blend);
            _body.localRotation = Quaternion.Slerp(_body.localRotation, body, blend);
            _body.localPosition = Vector3.Lerp(_body.localPosition, bodyPos + new Vector3(0f, bob, 0f), blend);
        }
    }
}
