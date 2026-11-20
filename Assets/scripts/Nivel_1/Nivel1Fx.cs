using UnityEngine;

// Efectos visuales del Nivel 1 generados por codigo (humo de contaminacion, chispas)
// y sprites suaves para la interfaz. No necesita prefabs ni imagenes.
public static class Nivel1Fx
{
    private static Sprite blob, anillo, disco, vineta;
    private static Material matSuave, matSolido;

    // ------------------------------------------------------------------ sprites
    public static Sprite Blob()   // mancha suave
    {
        if (blob != null) return blob;
        const int n = 128;
        Color32[] c = new Color32[n * n];
        float m = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f - m) / m, dy = (y + 0.5f - m) / m;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                c[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Pow(Mathf.Clamp01(1f - d), 2f) * 255f));
            }
        blob = Crear(c, n);
        return blob;
    }

    public static Sprite Anillo()   // circulo hueco
    {
        if (anillo != null) return anillo;
        const int n = 128;
        Color32[] c = new Color32[n * n];
        float m = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f - m) / m, dy = (y + 0.5f - m) / m;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((0.09f - Mathf.Abs(d - 0.88f)) * m * 0.5f);
                c[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        anillo = Crear(c, n);
        return anillo;
    }

    public static Sprite Disco()   // circulo macizo
    {
        if (disco != null) return disco;
        const int n = 64;
        Color32[] c = new Color32[n * n];
        float m = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - m, dy = y + 0.5f - m;
                float a = Mathf.Clamp01(m - 1f - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                c[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        disco = Crear(c, n);
        return disco;
    }

    public static Sprite Vineta()   // bordes oscuros
    {
        if (vineta != null) return vineta;
        const int n = 128;
        Color32[] c = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float t = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.45f) / 0.55f);
                c[y * n + x] = new Color32(255, 255, 255, (byte)(t * t * (3f - 2f * t) * 255f));
            }
        vineta = Crear(c, n);
        return vineta;
    }

    private static Sprite Crear(Color32[] c, int n)
    {
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels32(c);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    // ------------------------------------------------------------------ materiales de particulas
    private static Material MatSuave()
    {
        if (matSuave != null) return matSuave;
        Shader s = Shader.Find("Sprites/Default");
        if (s == null) return null;
        matSuave = new Material(s);
        matSuave.mainTexture = Blob().texture;
        return matSuave;
    }

    private static Material MatSolido()
    {
        if (matSolido != null) return matSolido;
        Shader s = Shader.Find("Sprites/Default");
        if (s == null) return null;
        matSolido = new Material(s);
        return matSolido;
    }

    // ------------------------------------------------------------------ efectos
    // Bocanada de humo contaminante: lo que queda cuando la basura toca el suelo y se desvanece
    public static void Humo(Vector3 pos, Color color)
    {
        GameObject go = new GameObject("HumoContaminacion");
        go.transform.position = pos;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
        main.startColor = color;
        main.gravityModifier = -0.04f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)12) });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.25f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.2f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 2.2f)));

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        Material m = MatSuave();
        if (m != null) r.sharedMaterial = m;

        ps.Play();
        Object.Destroy(go, 3.5f);
    }

    public static void Chispas(Vector3 pos, Color color, int cantidad = 20)
    {
        GameObject go = new GameObject("Chispas");
        go.transform.position = pos;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.duration = 0.4f;
        main.startLifetime = 0.6f;
        main.startSpeed = 3.5f;
        main.startSize = 0.08f;
        main.startColor = color;
        main.gravityModifier = 0.5f;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)cantidad) });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.1f;

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        Material m = MatSolido();
        if (m != null) r.sharedMaterial = m;

        ps.Play();
        Object.Destroy(go, 2f);
    }
}
